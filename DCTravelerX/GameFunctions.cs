using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DCTravelerX.Infos;
using FFXIVClientStructs.FFXIV.Application.Network;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace DCTravelerX;

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

    public static int GetXLDcTravelerPort()
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
