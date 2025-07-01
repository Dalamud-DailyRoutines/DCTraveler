using Dalamud.Game;
using Dalamud.Interface;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DcTraveler.Managers;
using DCTraveler.Managers;

namespace DCTraveler;

public class Service
{
    [PluginService] internal static IClientState            ClientState     { get; private set; } = null!;
    [PluginService] internal static ITextureProvider        TextureProvider { get; private set; } = null!;
    [PluginService] internal static IDataManager            DataManager     { get; private set; } = null!;
    [PluginService] internal static IPluginLog              Log             { get; private set; } = null!;
    [PluginService] internal static IContextMenu            ContextMenu     { get; private set; } = null!;
    [PluginService] internal static IGameGui                GameGui         { get; private set; } = null!;
    [PluginService] internal static ISigScanner             SigScanner      { get; private set; } = null!;
    [PluginService] internal static IFramework              Framework       { get; private set; } = null!;
    [PluginService] internal static ITitleScreenMenu        TitleScreenMenu { get; private set; } = null!;
    
    internal static IDalamudPluginInterface PI        { get; private set; } = null!;
    internal static IUiBuilder              UIBuilder { get; private set; } = null!;
    
    public static void Init(IDalamudPluginInterface pi)
    {
        PI        = pi;
        UIBuilder = PI.UiBuilder;
        
        PI.Create<Service>();
        
        FontManager.Init();
        WindowManager.Init();
        TitleScreenButtonManager.Init();
        ContextMenuManager.Init();
    }

    public static void Uninit()
    {
        ContextMenuManager.Uninit();
        TitleScreenButtonManager.Uninit();
        WindowManager.Uninit();
        FontManager.Uninit();
    }
}
