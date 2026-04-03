using DCTravelerX.Infos;
using Lumina.Excel.Sheets;

namespace DCTravelerX.Travel.Models;

internal sealed record TravelSourceContext
(
    World  CurrentWorld,
    string CurrentDcName,
    Group  CurrentGroup
);
