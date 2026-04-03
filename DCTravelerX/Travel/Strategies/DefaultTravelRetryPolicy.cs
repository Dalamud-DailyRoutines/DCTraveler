using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Keys;
using DCTravelerX.Travel.Exceptions;
using DCTravelerX.Travel.Interaction;
using DCTravelerX.Travel.Models;

namespace DCTravelerX.Travel.Strategies;

internal sealed class DefaultTravelRetryPolicy
(
    ITravelInteraction interaction
) : ITravelRetryPolicy
{
    public TravelRetrySettings CreateSettings(TravelRequest request)
    {
        var enableRetry = !request.IsBack && !request.IsIpcCall && Service.Config.EnableAutoRetry;
        return new
        (
            enableRetry,
            enableRetry && Service.Config.AllowSwitchToAvailableWorld,
            Service.Config.MaxRetryCount,
            TimeSpan.FromSeconds(Service.Config.RetryDelaySeconds)
        );
    }

    public bool ShouldCancelAfterCurrentAttempt(TravelResolution resolution)
    {
        if (!resolution.RetrySettings.EnableRetry || !Service.KeyState[VirtualKey.SHIFT])
            return false;

        Service.Log.Info("检测到 Shift 键按下，等待当前订单完成后取消");
        return true;
    }

    public bool CanRetry(TravelResolution resolution, Exception exception, int retryCount)
    {
        if (!resolution.RetrySettings.EnableRetry)
            return false;

        if (retryCount >= resolution.RetrySettings.MaxRetryCount)
            return false;

        var message = exception.Message;
        return message.Contains("传送失败")     ||
               message.Contains("繁忙")       ||
               message.Contains("请您稍晚再次尝试") ||
               message.Contains("稍晚再次尝试")   ||
               message.Contains("用户数量较多");
    }

    public async Task WaitForRetryAsync(Exception exception, int retryCount, TravelResolution resolution, CancellationToken cancellationToken)
    {
        var retryDelay = resolution.RetrySettings.RetryDelay;
        var waitStart  = DateTime.UtcNow;

        while (DateTime.UtcNow - waitStart < retryDelay)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (resolution.RetrySettings.EnableRetry && Service.KeyState[VirtualKey.SHIFT])
                throw new TravelUserCancelledException("取消了传送操作");

            var remaining    = retryDelay - (DateTime.UtcNow - waitStart);
            var errorMessage = ExtractErrorMessage(exception);
            await interaction.ShowWaitAsync
            (
                $"错误: {errorMessage}\n第 {retryCount}/{resolution.RetrySettings.MaxRetryCount} 次重试 / 等待 {remaining.TotalSeconds:F0} 秒后重试 (按住 Shift 取消传送)",
                cancellationToken
            );
            await Task.Delay(1_000, cancellationToken);
        }
    }

    private static string ExtractErrorMessage(Exception exception)
    {
        const string Prefix = "消息:";

        var message = exception.Message;
        var parts   = message.Split(Prefix, 2);

        if (parts.Length == 2)
            message = parts[1].Trim();

        message = message.Split("\nat ",      2)[0].Trim();
        message = message.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[0].Trim();

        return message.Length > 150
                   ? message[..150] + "..."
                   : message;
    }
}
