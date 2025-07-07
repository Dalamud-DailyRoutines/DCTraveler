using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DCTravelerX.Infos;
using DCTravelerX.Managers;
using FFXIVClientStructs.FFXIV.Application.Network;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Task = System.Threading.Tasks.Task;

namespace DCTravelerX.Helpers;

internal static class GameFunctions
{
    private unsafe delegate void ReturnToTitleDelegate(AgentLobby* agentLobby);

    private unsafe delegate void ReleaseLobbyContextDelegate(NetworkModule* agentLobby);

    private static readonly ReturnToTitleDelegate       ReturnToTitlePtr;
    private static readonly ReleaseLobbyContextDelegate ReleaseLobbyContextPtr;

    static GameFunctions()
    {
        var returnToTitleAddr = Service.SigScanner.ScanText("E8 ?? ?? ?? ?? C6 87 ?? ?? ?? ?? ?? 33 C0 ");
        ReturnToTitlePtr = Marshal.GetDelegateForFunctionPointer<ReturnToTitleDelegate>(returnToTitleAddr);

        var releaseLobbyContextAddr = Service.SigScanner.ScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8B 85 ?? ?? ?? ?? 48 85 C0");
        ReleaseLobbyContextPtr = Marshal.GetDelegateForFunctionPointer<ReleaseLobbyContextDelegate>(releaseLobbyContextAddr);
    }

    public static unsafe void ReturnToTitle()
    {
        ReturnToTitlePtr(AgentLobby.Instance());
        Service.Log.Information("返回标题界面");
    }

    public static unsafe void OpenWaitAddon(string message)
    {
        var instance = RaptureAtkModule.Instance();

        var row = instance->AddonNames.Select((name, index) => new { Name = name.ToString(), Index = index })
                                      .FirstOrDefault(x => x.Name == "LobbyDKT").Index;
        
        var values = stackalloc AtkValue[3];
        values[0].SetManagedString($"{message}");
        values[1].SetUInt(0);
        instance->OpenAddon((uint)row, 2, values, null, 0, 0, 0);

        CloseTitleLogoAddon();
    }
    
    public static unsafe void UpdateWaitAddon(string message)
    {
        var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("LobbyDKT");
        if (addon == null) return;

        CloseTitleLogoAddon();
        
        addon->AtkValues[0].SetManagedString(message);
        addon->OnRefresh(addon->AtkValuesCount, addon->AtkValues);
    }
    
    public static unsafe void ResetTitleIdleTime() => 
        AgentLobby.Instance()->IdleTime = 0;

    public static unsafe void CloseWaitAddon()
    {
        var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("LobbyDKT");
        if (addon == null) return;

        addon->Close(true);
    }

    public static unsafe void CloseTitleLogoAddon()
    {
        var logoAddon = (AtkUnitBase*)Service.GameGui.GetAddonByName("_TitleLogo");
        if (logoAddon == null) return;
        
        logoAddon->IsVisible = false;
    }

    public static unsafe void RefreshGameServer()
    {
        var framework     = Framework.Instance();
        var networkModule = framework->GetNetworkModuleProxy()->NetworkModule;
        ReleaseLobbyContextPtr(networkModule);
        var agentLobby     = AgentLobby.Instance();
        var lobbyUIClient2 = (LobbyUIClientExposed*)Unsafe.AsPointer(ref agentLobby->LobbyData.LobbyUIClient);
        lobbyUIClient2->Context = 0;
        lobbyUIClient2->State   = 0;
        
        Service.Log.Information("刷新大厅信息");
    }

    public static unsafe void ChangeDevTestSid(string sid)
    {
        var agentLobby = AgentLobby.Instance();
        agentLobby->UnkUtf8Strings[0].SetString(sid);
        Service.Log.Information("筛选 Dev.TestSid");
    }

    public static unsafe void ChangeGameServer(string lobbyHost, string saveDataHost, string gmServerHost)
    {
        var framework     = Framework.Instance();
        var networkModule = framework->GetNetworkModuleProxy()->NetworkModule;
        networkModule->ActiveLobbyHost.SetString(lobbyHost);
        networkModule->LobbyHosts[0].SetString(lobbyHost);
        networkModule->SaveDataBankHost.SetString(saveDataHost);

        for (var i = 0; i < framework->DevConfig.ConfigCount; ++i)
        {
            var entry = framework->DevConfig.ConfigEntry[i];
            if (entry.Value.String == null) continue;
            
            var name = entry.Name.ToString();
            switch (name)
            {
                case "GMServerHost":
                    entry.Value.String->SetString(gmServerHost);
                    break;
                case "SaveDataBankHost":
                    entry.Value.String->SetString(saveDataHost);
                    break;
                case "LobbyHost01":
                    entry.Value.String->SetString(lobbyHost);
                    break;
            }
        }

        Service.Log.Information($"修改游戏大厅地址: LobbyHost - {lobbyHost}, SaveDataBankHost - {saveDataHost}, GmHost - {gmServerHost}");
    }

    public static unsafe string GetGameArgument(string key)
    {
        if (!key.EndsWith('='))
            key += "=";
        
        var gameWindow = GameWindow.Instance();
        for (var i = 0UL; i < gameWindow->ArgumentCount; i++)
        {
            var arg = gameWindow->GetArgument(i);
            if (arg.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                return arg[key.Length..];
        }
        
        throw new Exception($"未能从游戏参数中获取 {key}");
    }

    public static int GetLauncherDCTravelPort()
    {
        var portString = GetGameArgument("XL.DcTraveler");
        if (int.TryParse(portString, out var port))
        {
            Service.Log.Debug($"超域旅行用端口: {port}");
            return port;
        }
        
        throw new Exception("未能发现用于超域旅行的端口");
    }
    
    public static unsafe void LoginInGame()
    {
        var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("_TitleMenu");
        if (addon == null) return;

        var loginGameButton      = addon->GetComponentButtonById(4);
        var loginGameButtonEvent = loginGameButton->AtkResNode->AtkEventManager.Event;
        Service.Framework.RunOnFrameworkThread(
            () => addon->ReceiveEvent(AtkEventType.ButtonClick, 1, loginGameButtonEvent));
    }
    
    public static void ChangeToSdoArea(string groupName)
    {
        var targetArea = ServerDataManager.SdoAreas?.FirstOrDefault(x => x.AreaName == groupName);
        if (targetArea == null)
        {
            Service.Log.Error($"未找到大区: {groupName}");
            return;
        }
        ChangeGameServer(targetArea.AreaLobby, targetArea.AreaConfigUpload, targetArea.AreaGm);
        RefreshGameServer();
    }

    public static async Task SelectDCAndLogin(string name)
    {
        var newTicket = await DCTravelClient.Instance().RefreshGameSessionId();

        ChangeToSdoArea(name);
        ChangeDevTestSid(newTicket);
        CloseWaitAddon();
        LoginInGame();
    }
}
