using System;
using System.Threading;
using System.Threading.Tasks;
using DCTravelerX.Infos;
using DCTravelerX.Travel;
using DCTravelerX.Travel.Interaction;
using DCTravelerX.Travel.Models;
using DCTravelerX.Travel.Runtime;
using DCTravelerX.Travel.Services;
using DCTravelerX.Travel.Strategies;

namespace DCTravelerX.Managers;

public static class TravelManager
{
    private static readonly ITravelInteraction     Interaction     = new DefaultTravelInteraction();
    private static readonly ITravelRetryPolicy     RetryPolicy     = new DefaultTravelRetryPolicy(Interaction);
    private static readonly ITravelContextResolver ContextResolver = new TravelContextResolver(Interaction, RetryPolicy);
    private static readonly ICooldownGate          CooldownGate    = new TravelCooldownGate(Interaction);
    private static readonly IOrderMonitor          OrderMonitor    = new TravelOrderMonitor(Interaction);

    private static readonly ITravelExecutionStrategy[] ExecutionStrategies =
    [
        new ReturnTravelExecutionStrategy(),
        new OutboundTravelExecutionStrategy(ContextResolver)
    ];

    public static void Travel
    (
        int     targetWorldId,
        int     currentWorldId,
        ulong   contentId,
        bool    isBack,
        bool    needSelectCurrentWorld,
        string  currentCharacterName,
        string? errorMessage = null
    ) =>
        Task.Run
        (() => RunAsync
         (
             new
             (
                 targetWorldId,
                 currentWorldId,
                 contentId,
                 isBack,
                 needSelectCurrentWorld,
                 currentCharacterName,
                 false,
                 errorMessage
             )
         )
        );

    internal static Task<TravelOutcome> ExecuteTravelFlow
    (
        int     targetWorldId,
        int     currentWorldId,
        ulong   contentId,
        bool    isBack,
        bool    needSelectCurrentWorld,
        string  currentCharacterName,
        bool    isIpcCall,
        string? errorMessage = null
    ) =>
        RunAsync
        (
            new
            (
                targetWorldId,
                currentWorldId,
                contentId,
                isBack,
                needSelectCurrentWorld,
                currentCharacterName,
                isIpcCall,
                errorMessage
            )
        );

    internal static async Task<bool> WaitForOrderCompletionAsync(string orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            await OrderMonitor.WaitForCompletionAsync
            (
                orderId,
                new
                (
                    null,
                    true,
                    false,
                    false,
                    false
                ),
                cancellationToken
            );
            return true;
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "查询订单状态失败");
            return false;
        }
    }

    public static Task<MigrationOrder> GetTravelingOrder(ulong contentId) =>
        ContextResolver.GetTravelingOrderAsync(contentId);

    internal static void Uninit()
    {
        TravelRuntime.CancelActiveSession();

        try
        {
            Interaction.CleanupImmediately();
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "跨区流程卸载清理时出现异常");
        }
    }

    private static Task<TravelOutcome> RunAsync(TravelRequest request) =>
        new TravelSession
        (
            request,
            Interaction,
            ContextResolver,
            CooldownGate,
            OrderMonitor,
            RetryPolicy,
            ExecutionStrategies
        ).RunAsync();
}
