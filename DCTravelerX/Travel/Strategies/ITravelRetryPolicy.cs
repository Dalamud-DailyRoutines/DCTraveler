using System;
using System.Threading;
using System.Threading.Tasks;
using DCTravelerX.Travel.Models;

namespace DCTravelerX.Travel.Strategies;

internal interface ITravelRetryPolicy
{
    TravelRetrySettings CreateSettings(TravelRequest request);

    bool ShouldCancelAfterCurrentAttempt(TravelResolution resolution);

    bool CanRetry(TravelResolution resolution, Exception exception, int retryCount);

    Task WaitForRetryAsync(Exception exception, int retryCount, TravelResolution resolution, CancellationToken cancellationToken);
}
