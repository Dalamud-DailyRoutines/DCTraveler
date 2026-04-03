using System;
using System.Threading;
using System.Threading.Tasks;
using DCTravelerX.Infos;
using DCTravelerX.Travel.Models;
using DCTravelerX.Travel.Services;

namespace DCTravelerX.Travel.Strategies;

internal sealed class OutboundTravelExecutionStrategy
(
    ITravelContextResolver contextResolver
) : ITravelExecutionStrategy
{
    public bool CanHandle(TravelRequest request) =>
        !request.IsBack;

    public async Task<TravelSubmission> SubmitAsync(TravelRequest request, TravelResolution resolution, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolution.RequestedTargetGroup);

        await DCTravelClient.Instance().QueryAllTravelTime();

        var requestedTargetGroup = contextResolver.RefreshTargetGroup(resolution.RequestedTargetGroup);
        var effectiveTargetGroup = contextResolver.ResolveEffectiveTargetGroup(requestedTargetGroup, resolution.RetrySettings.AllowSwitchToAvailableWorld);

        if (!string.Equals(effectiveTargetGroup.GroupName, requestedTargetGroup.GroupName, StringComparison.Ordinal))
            Service.Log.Information($"目标服务器 {requestedTargetGroup.GroupName} 当前繁忙，自动切换至同大区通畅服务器 {effectiveTargetGroup.GroupName}");

        var character = new Character
        {
            ContentId = request.ContentId.ToString(),
            Name      = request.CurrentCharacterName
        };

        var orderId = await DCTravelClient.Instance().TravelOrder(effectiveTargetGroup, resolution.Source.CurrentGroup, character);
        Service.Log.Information($"订单号: {orderId}，目标服务器: {effectiveTargetGroup.GroupName}");

        return new(orderId, resolution.TargetDcGroupName);
    }
}
