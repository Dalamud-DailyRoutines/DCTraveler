using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DCTravelerX.Infos;
using DCTravelerX.Travel.Exceptions;
using DCTravelerX.Travel.Interaction;
using DCTravelerX.Travel.Models;
using DCTravelerX.Travel.Runtime;
using DCTravelerX.Travel.Services;
using DCTravelerX.Travel.Strategies;

namespace DCTravelerX.Travel;

internal sealed class TravelSession
{
    private readonly ITravelInteraction                      interaction;
    private readonly ITravelContextResolver                  contextResolver;
    private readonly ICooldownGate                           cooldownGate;
    private readonly IOrderMonitor                           orderMonitor;
    private readonly ITravelRetryPolicy                      retryPolicy;
    private readonly IReadOnlyList<ITravelExecutionStrategy> executionStrategies;

    public TravelSession
    (
        TravelRequest                           request,
        ITravelInteraction                      interaction,
        ITravelContextResolver                  contextResolver,
        ICooldownGate                           cooldownGate,
        IOrderMonitor                           orderMonitor,
        ITravelRetryPolicy                      retryPolicy,
        IReadOnlyList<ITravelExecutionStrategy> executionStrategies
    )
    {
        Request                  = request;
        this.interaction         = interaction;
        this.contextResolver     = contextResolver;
        this.cooldownGate        = cooldownGate;
        this.orderMonitor        = orderMonitor;
        this.retryPolicy         = retryPolicy;
        this.executionStrategies = executionStrategies;
    }

    public TravelRequest Request { get; }

    public TravelState State { get; private set; } = TravelState.Preparing;

    public async Task<TravelOutcome> RunAsync()
    {
        using var sessionCancellation = new CancellationTokenSource();

        TravelRuntime.RegisterSession(sessionCancellation);

        var acquiredGate = false;
        var needReLogin  = false;

        try
        {
            await TravelRuntime.WaitAsync(sessionCancellation.Token);
            acquiredGate = true;
            TravelRuntime.MarkTravelStarted();

            return await RunCoreAsync(sessionCancellation.Token);
        }
        catch (OperationCanceledException) when (sessionCancellation.IsCancellationRequested)
        {
            State = TravelState.Cancelled;
            Service.Log.Information("跨区流程已取消");
            return TravelOutcome.Cancelled();
        }
        catch (Exception ex)
        {
            State       = TravelState.Failed;
            needReLogin = true;

            await interaction.ShowMessageAsync(Request.Title, $"{Request.Title} 失败:\n{ex.Message}", showWebsite: true);
            Service.Log.Error(ex, "跨大区失败");

            return TravelOutcome.Failed(ex);
        }
        finally
        {
            if (acquiredGate)
            {
                State = TravelState.Finalizing;
                await interaction.CleanupSessionAsync(needReLogin);
            }

            TravelRuntime.CompleteSession(sessionCancellation, acquiredGate);
        }
    }

    private async Task<TravelOutcome> RunCoreAsync(CancellationToken cancellationToken)
    {
        State = TravelState.Preparing;
        await interaction.PrepareSessionAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(Request.ErrorMessage))
        {
            await interaction.ShowMessageAsync(Request.Title, Request.ErrorMessage!);
            return TravelOutcome.Failed();
        }

        if (!DCTravelClient.IsValid)
        {
            await interaction.ShowMessageAsync(Request.Title, "无法连接至超域旅行 API, 请从 XIVLauncherCN 重新启动游戏");
            Service.Log.Error("无法连接至 XIVLauncherCN 提供的超域旅行 API 服务");
            return TravelOutcome.Failed();
        }

        State = TravelState.ResolvingContext;
        var resolution = await contextResolver.ResolveAsync(Request, cancellationToken);

        if (!resolution.ShouldContinue)
        {
            State = TravelState.Cancelled;
            return TravelOutcome.Cancelled();
        }

        State = TravelState.CoolingDown;
        await cooldownGate.WaitAsync(cancellationToken);
        TravelRuntime.MarkTravelRequested();

        await interaction.BeginSubmissionAsync(resolution, cancellationToken);

        var executionStrategy = executionStrategies.First(strategy => strategy.CanHandle(Request));
        var retryCount        = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cancelAfterCurrentAttempt = retryPolicy.ShouldCancelAfterCurrentAttempt(resolution);

            try
            {
                State = TravelState.SubmittingOrder;
                var submission = await executionStrategy.SubmitAsync(Request, resolution, cancellationToken);

                Service.Log.Information
                    ($"订单号: {submission.OrderId}，目标大区: {submission.TargetDcGroupName} (尝试 {retryCount + 1}/{resolution.RetrySettings.MaxRetryCount + 1})");

                State = TravelState.WaitingOrderStatus;
                await orderMonitor.WaitForCompletionAsync
                (
                    submission.OrderId,
                    new
                    (
                        submission.TargetDcGroupName,
                        Request.IsIpcCall,
                        true,
                        true,
                        true,
                        state => State = state
                    ),
                    cancellationToken
                );

                if (cancelAfterCurrentAttempt)
                    TravelRuntime.MarkCancelled();

                State = TravelState.Completed;
                return TravelOutcome.Succeeded();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TravelUserCancelledException)
            {
                TravelRuntime.MarkCancelled();
                throw;
            }
            catch (Exception ex)
            {
                Service.Log.Info
                    ($"捕获异常 - EnableRetry: {resolution.RetrySettings.EnableRetry}, RetryCount: {retryCount}, MaxRetries: {resolution.RetrySettings.MaxRetryCount}");
                Service.Log.Info($"异常消息: {ex.Message}");

                if (cancelAfterCurrentAttempt)
                {
                    TravelRuntime.MarkCancelled();
                    throw new TravelUserCancelledException("取消了传送操作");
                }

                if (!retryPolicy.CanRetry(resolution, ex, retryCount))
                {
                    Service.Log.Info("不满足重试条件，直接抛出异常");
                    throw;
                }

                retryCount++;
                State = TravelState.RetryWaiting;
                Service.Log.Warning($"传送失败 (尝试 {retryCount}/{resolution.RetrySettings.MaxRetryCount}): {ex.Message}");
                await retryPolicy.WaitForRetryAsync(ex, retryCount, resolution, cancellationToken);
                Service.Log.Info($"开始第 {retryCount} 次重试...");
            }
        }
    }
}
