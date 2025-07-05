using Dalamud.Plugin;

namespace DCTravelerX;

public sealed class Plugin : IDalamudPlugin
{
    public Plugin(IDalamudPluginInterface pi) => 
        Service.Init(pi);

    public void Dispose() =>
        Service.Uninit();
}
