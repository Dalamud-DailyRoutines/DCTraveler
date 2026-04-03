using System.Threading;
using System.Threading.Tasks;
using DCTravelerX.Travel.Models;

namespace DCTravelerX.Travel.Strategies;

internal interface ITravelExecutionStrategy
{
    bool CanHandle(TravelRequest request);

    Task<TravelSubmission> SubmitAsync(TravelRequest request, TravelResolution resolution, CancellationToken cancellationToken);
}
