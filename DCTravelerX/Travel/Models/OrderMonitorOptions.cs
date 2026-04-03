using System;

namespace DCTravelerX.Travel.Models;

internal sealed record OrderMonitorOptions
(
    string?              TargetDcGroupName,
    bool                 IsIpcCall,
    bool                 ShowStatus,
    bool                 LoginAfterCompletion,
    bool                 PlayCompletionSound,
    Action<TravelState>? OnStateChanged = null
);
