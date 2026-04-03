using System.Threading;
using System.Threading.Tasks;
using DCTravelerX.Infos;
using DCTravelerX.Travel.Models;

namespace DCTravelerX.Travel.Services;

internal interface ITravelContextResolver
{
    Task<TravelResolution> ResolveAsync(TravelRequest request, CancellationToken cancellationToken);

    Group GetTargetGroupByWorldId(int targetWorldId, string errorPrefix = "");

    Group RefreshTargetGroup(Group requestedTargetGroup);

    Group ResolveEffectiveTargetGroup(Group requestedTargetGroup, bool allowSwitchToAvailableWorld);

    Task<MigrationOrder> GetTravelingOrderAsync(ulong contentId);
}
