namespace DCTravelerX.Travel.Models;

internal enum TravelState
{
    Preparing,
    ResolvingContext,
    CoolingDown,
    SubmittingOrder,
    WaitingOrderStatus,
    AwaitingConfirmation,
    RetryWaiting,
    Finalizing,
    Completed,
    Cancelled,
    Failed
}
