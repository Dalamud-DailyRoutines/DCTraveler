using System;

namespace DCTravelerX.Travel.Models;

internal enum TravelOutcomeKind
{
    Succeeded,
    Cancelled,
    Failed
}

internal sealed record TravelOutcome
(
    TravelOutcomeKind Kind,
    Exception?        Error = null
)
{
    public static TravelOutcome Succeeded() => new(TravelOutcomeKind.Succeeded);

    public static TravelOutcome Cancelled() => new(TravelOutcomeKind.Cancelled);

    public static TravelOutcome Failed(Exception? error = null) => new(TravelOutcomeKind.Failed, error);
}
