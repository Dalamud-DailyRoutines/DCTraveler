using System;
using System.Threading;
using System.Threading.Tasks;
using DCTravelerX.Infos;
using DCTravelerX.Travel.Models;

namespace DCTravelerX.Travel.Strategies;

internal sealed class ReturnTravelExecutionStrategy : ITravelExecutionStrategy
{
    public bool CanHandle(TravelRequest request) =>
        request.IsBack;

    public async Task<TravelSubmission> SubmitAsync(TravelRequest request, TravelResolution resolution, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resolution.ReturnOrderId);

        var currentGroup = resolution.Source.CurrentGroup;
        var orderId = await DCTravelClient.Instance()
                                          .TravelBack(resolution.ReturnOrderId, currentGroup.GroupID, currentGroup.GroupCode, currentGroup.GroupName);

        Service.Log.Information($"返回订单号: {orderId}");
        return new(orderId, resolution.TargetDcGroupName);
    }
}
