using System;

namespace DCTravelerX.Travel.Models;

internal sealed record TravelRetrySettings
(
    bool     EnableRetry,
    bool     AllowSwitchToAvailableWorld,
    int      MaxRetryCount,
    TimeSpan RetryDelay
);
