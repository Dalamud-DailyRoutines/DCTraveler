using System;
using Dalamud.Interface;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DCTravelerX.Helpers;
using DCTravelerX.Infos;
using DCTravelerX.Managers;

namespace DCTravelerX;

public class Service
{
    [PluginService]
    internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;

    [PluginService]
    internal static IClientState ClientState { get; private set; } = null!;

    [PluginService]
    internal static ITextureProvider TextureProvider { get; private set; } = null!;

    [PluginService]
    internal static IDataManager DataManager { get; private set; } = null!;

    [PluginService]
    internal static IPluginLog Log { get; private set; } = null!;

    [PluginService]
    internal static IContextMenu ContextMenu { get; private set; } = null!;

    [PluginService]
    internal static IGameGui GameGui { get; private set; } = null!;

    [PluginService]
    internal static ISigScanner SigScanner { get; private set; } = null!;

    [PluginService]
    internal static IFramework Framework { get; private set; } = null!;

    [PluginService]
    internal static ITitleScreenMenu TitleScreenMenu { get; private set; } = null!;

    [PluginService]
    internal static IKeyState KeyState { get; private set; } = null!;

    internal static IDalamudPluginInterface PI        { get; private set; } = null!;
    internal static IUiBuilder              UIBuilder { get; private set; } = null!;
    internal static Configuration           Config    { get; private set; } = null!;

    public static void Init(IDalamudPluginInterface pi)
    {
        PI        = pi;
        UIBuilder = PI.UiBuilder;
        Config    = PI.GetPluginConfig() as Configuration ?? new Configuration();

        PI.Create<Service>();

        try
        {
            ServerDataManager.Init();
            FontManager.Init();
            WindowManager.Init();
            TitleScreenButtonManager.Init();
            ContextMenuManager.Init();
            IPCManager.Init();

            _ = DCTravelClient.Instance(GameFunctions.GetLauncherDCTravelPort());
        }
        catch (Exception ex)
        {
            Log.Error($"服务初始化失败: {ex}");
        }
    }

    public static void Uninit()
    {
        IPCManager.Uninit();
        DCTravelClient.Instance().IsDisposed = true;
        ContextMenuManager.Uninit();
        TitleScreenButtonManager.Uninit();
        WindowManager.Uninit();
        FontManager.Uninit();
        ServerDataManager.Uninit();
    }
}
