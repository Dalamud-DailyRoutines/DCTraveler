using DCTravelerX.Infos;

namespace DCTravelerX.Travel.Models;

internal sealed record TravelResolution
(
    TravelSourceContext Source,
    string              Title,
    string              TargetDcGroupName,
    Group?              RequestedTargetGroup,
    Group?              PreviewEffectiveTargetGroup,
    TravelRetrySettings RetrySettings,
    string              InitialWaitMessage,
    bool                ShouldContinue,
    bool                ShouldReturnToTitleBeforeSubmit,
    string?             ReturnOrderId = null
);
