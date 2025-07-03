using System;
using System.Linq;
using Dalamud.Plugin;
using DCTravelerX.Helpers;
using DCTravelerX.Infos;
using DCTravelerX.Managers;
using DCTravelerX.Windows;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Task = System.Threading.Tasks.Task;

namespace DCTravelerX;

public sealed class Plugin : IDalamudPlugin
{
    internal static SdoArea[]?      sdoAreas;

    internal static string? LastErrorMessage { get; private set; }

    public Plugin(IDalamudPluginInterface pi)
    {
        Service.Init(pi);

        try
        {
            Task.Run(() => { sdoAreas = SdoArea.Get().Result; });
            var port = GameFunctions.GetLauncherDCTravelPort();
            _ = DCTravelClient.Instance(port);
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            Service.Log.Error(ex.ToString());
        }
    }

    internal static void OpenDcSelectWindow() =>
        WindowManager.Get<DCGroupSelectorWindow>()?.Open(sdoAreas);

    public static void ChangeToSdoArea(string groupName)
    {
        var targetArea = sdoAreas!.FirstOrDefault(x => x.AreaName == groupName);
        GameFunctions.ChangeGameServer(targetArea!.AreaLobby, targetArea!.AreaConfigUpload, targetArea!.AreaGm);
        GameFunctions.RefreshGameServer();
    }

    public static unsafe void LoginInGame()
    {
        var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("_TitleMenu");
        if (addon == null) return;
        
        var loginGameButton      = addon->GetComponentButtonById(4);
        var loginGameButtonEvent = loginGameButton->AtkResNode->AtkEventManager.Event;
        Service.Framework.RunOnFrameworkThread(() => addon->ReceiveEvent(AtkEventType.ButtonClick, 1, loginGameButtonEvent));
    }

    public static async Task SelectDCAndLogin(string name)
    {
        var newTicket = await DCTravelClient.Instance().RefreshGameSessionId();
        
        ChangeToSdoArea(name);
        GameFunctions.ChangeDevTestSid(newTicket);
        GameFunctions.CloseWaitAddon();
        LoginInGame();
    }

    public void Dispose() => 
        Service.Uninit();
}
