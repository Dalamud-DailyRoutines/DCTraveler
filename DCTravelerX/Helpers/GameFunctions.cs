using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DCTravelerX.Infos;
using FFXIVClientStructs.FFXIV.Application.Network;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DCTravelerX.Helpers;

internal static unsafe class GameFunctions
{
    private delegate void ReturnToTitleDelegate(AgentLobby* agentLobby);

    private delegate void ReleaseLobbyContextDelegate(NetworkModule* agentLobby);

    private static readonly ReturnToTitleDelegate       ReturnToTitlePtr;
    private static readonly ReleaseLobbyContextDelegate ReleaseLobbyContextPtr;

    static GameFunctions()
    {
        var returnToTitleAddr = Service.SigScanner.ScanText("E8 ?? ?? ?? ?? C6 87 ?? ?? ?? ?? ?? 33 C0 ");
        ReturnToTitlePtr = Marshal.GetDelegateForFunctionPointer<ReturnToTitleDelegate>(returnToTitleAddr);

        var releaseLobbyContextAddr = Service.SigScanner.ScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8B 85 ?? ?? ?? ?? 48 85 C0");
        ReleaseLobbyContextPtr = Marshal.GetDelegateForFunctionPointer<ReleaseLobbyContextDelegate>(releaseLobbyContextAddr);
    }

    public static void ReturnToTitle()
    {
        ReturnToTitlePtr(AgentLobby.Instance());
        Service.Log.Information("返回标题界面");
    }

    public static void OpenWaitAddon(string message)
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
    
    public static void UpdateWaitAddon(string message)
    {
        var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("LobbyDKT");
        if (addon == null) return;

        CloseTitleLogoAddon();
        
        addon->AtkValues[0].SetManagedString(message);
        addon->OnRefresh(addon->AtkValuesCount, addon->AtkValues);
    }

    public static void CloseWaitAddon()
    {
        var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("LobbyDKT");
        if (addon == null) return;

        addon->Close(true);
    }

    public static void CloseTitleLogoAddon()
    {
        var logoAddon = (AtkUnitBase*)Service.GameGui.GetAddonByName("_TitleLogo");
        if (logoAddon == null) return;
        
        logoAddon->IsVisible = false;
    }

    public static void RefreshGameServer()
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

    public static void ChangeDevTestSid(string sid)
    {
        var agentLobby = AgentLobby.Instance();
        agentLobby->UnkUtf8Strings[0].SetString(sid);
        Service.Log.Information("筛选 Dev.TestSid");
    }

    public static void ChangeGameServer(string lobbyHost, string saveDataHost, string gmServerHost)
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

    public static int GetLauncherDCTravelPort()
    {
        var          port       = 0;
        var          gameWindow = GameWindow.Instance();
        const string key        = "XL.DcTraveler=";
        
        for (var i = 0UL; i < gameWindow->ArgumentCount; i++)
        {
            var arg = gameWindow->GetArgument(i);
            if (arg.StartsWith(key, StringComparison.OrdinalIgnoreCase))
            {
                _ = int.TryParse(arg[key.Length..], out port);
                break;
            }
        }

        if (port == 0)
            throw new Exception("未能发现用于超域旅行的端口");
        
        return port;
    }
}
