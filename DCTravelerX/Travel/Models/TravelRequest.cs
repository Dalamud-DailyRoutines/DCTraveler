namespace DCTravelerX.Travel.Models;

internal sealed record TravelRequest
(
    int     TargetWorldId,
    int     CurrentWorldId,
    ulong   ContentId,
    bool    IsBack,
    bool    NeedSelectCurrentWorld,
    string  CurrentCharacterName,
    bool    IsIpcCall,
    string? ErrorMessage = null
)
{
    public string Title => IsBack ? "返回至原始大区" : "超域旅行";
}
