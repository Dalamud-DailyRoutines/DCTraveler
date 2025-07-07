using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text;
using DCTravelerX.Helpers;
using DCTravelerX.Infos;
using DCTravelerX.Windows;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace DCTravelerX.Managers;

public static class ContextMenuManager
{
    
    internal static void Init() => 
        Service.ContextMenu.OnMenuOpened += OnContextMenuOpened;

    internal static void Uninit() => 
        Service.ContextMenu.OnMenuOpened -= OnContextMenuOpened;

    private static unsafe void OnContextMenuOpened(IMenuOpenedArgs args)
    {
        if (args.AddonPtr != 0 || args.MenuType != ContextMenuType.Default) return;
        if (Service.GameGui.GetAddonByName("_CharaSelectListMenu") == nint.Zero) return;
        
        var agentLobby            = AgentLobby.Instance();
        var selectedCharacterCID  = agentLobby->SelectedCharacterContentId;
        var currentCharacterEntry = agentLobby->LobbyData.CharaSelectEntries[agentLobby->SelectedCharacterIndex].Value;
        var currentWorldID        = currentCharacterEntry->CurrentWorldId;
        var homeWorldID           = currentCharacterEntry->HomeWorldId;
        var currentCharacterName  = currentCharacterEntry->NameString;
        
        if (currentCharacterEntry->LoginFlags == CharaSelectCharacterEntryLoginFlags.Unk32 || 
            currentCharacterEntry->LoginFlags == CharaSelectCharacterEntryLoginFlags.DCTraveling)
        {
            args.AddMenuItem(new MenuItem
            {
                Name = "返回至原始大区",
                OnClicked = _ => TravelManager.Travel(homeWorldID,
                                                      currentWorldID,
                                                      selectedCharacterCID,
                                                      true,
                                                      currentCharacterEntry->LoginFlags == CharaSelectCharacterEntryLoginFlags.Unk32,
                                                      currentCharacterName),
                Prefix      = SeIconChar.CrossWorld,
                PrefixColor = 34,
                IsEnabled   = true
            });
        }
        else
        {
            args.AddMenuItem(new MenuItem
            {
                Name        = "超域旅行",
                OnClicked   = _ => TravelManager.Travel(0, currentWorldID, selectedCharacterCID, false, false, currentCharacterName),
                Prefix      = SeIconChar.CrossWorld,
                PrefixColor = 34,
                IsEnabled   = currentWorldID == homeWorldID
            });
        }
    }
    
    
} 
