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
    private static readonly Dictionary<MigrationStatus, string> StatusText = new()
    {
        [MigrationStatus.Failed]          = "超域旅行失败",
        [MigrationStatus.InPrepare]       = "检查目标大区角色信息中...",
        [MigrationStatus.InQueue]         = "超域旅行排队中...",
        [MigrationStatus.Completed]       = "超域旅行完成",
        [MigrationStatus.UnkownCompleted] = "超域旅行完成",
    };
    
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
        
        if (currentCharacterEntry->LoginFlags == CharaSelectCharacterEntryLoginFlags.DCTraveling || 
            currentCharacterEntry->LoginFlags == CharaSelectCharacterEntryLoginFlags.DCTraveling)
        {
            args.AddMenuItem(new MenuItem
            {
                Name        = "返回至原始大区",
                OnClicked   = _ => Travel(homeWorldID, currentWorldID, selectedCharacterCID, true, currentCharacterName),
                Prefix      = SeIconChar.CrossWorld,
                PrefixColor = 48,
                IsEnabled   = true
            });
        }
        else
        {
            args.AddMenuItem(new MenuItem
            {
                Name        = "超域旅行",
                OnClicked   = _ => Travel(0, currentWorldID, selectedCharacterCID, false, currentCharacterName),
                Prefix      = SeIconChar.CrossWorld,
                PrefixColor = 48,
                IsEnabled   = currentWorldID == homeWorldID
            });
        }
    }

    private static void Travel(int targetWorldId, int currentWorldId, ulong contentId, bool isBack, string currentCharacterName)
    {
        var title = isBack ? "返回至原始大区" : "超域旅行";

        if (Plugin.LastErrorMessage != null)
        {
            MessageBoxWindow.Show(WindowManager.WindowSystem, title, Plugin.LastErrorMessage!);
            return;
        }

        if (DCTravelClient.Instance() is not { IsValid: true } instance)
        {
            MessageBoxWindow.Show(WindowManager.WindowSystem, title, "无法连接至超域旅行 API, 请检查 XIVLauncherCN 设置状态");
            Service.Log.Error("无法连接至 XIVLauncherCN 提供的超域旅行 API 服务");
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                var worldSheet         = Service.DataManager.GetExcelSheet<World>();
                var currentWorld       = worldSheet.GetRow((uint)currentWorldId);
                var currentDcGroupName = currentWorld.DataCenter.Value.Name.ExtractText();
                var currentGroup = instance.CachedAreas
                                           .First(x => x.AreaName  == currentDcGroupName).GroupList
                                           .First(x => x.GroupCode == currentWorld.InternalName.ExtractText());

                var orderID           = string.Empty;
                var targetDcGroupName = string.Empty;
                if (isBack)
                {
                    var targetWorld = worldSheet.GetRow((uint)targetWorldId);
                    targetDcGroupName = targetWorld.DataCenter.Value.Name.ToString();

                    var order = GetTravelingOrder(contentId);
                    Service.Log.Information($"找到返回原始大区订单: {order.OrderId}");

                    await Service.Framework.RunOnFrameworkThread(GameFunctions.ReturnToTitle);
                    await Service.Framework.RunOnFrameworkThread(() => GameFunctions.OpenWaitAddon($"正在返回原始大区: {targetDcGroupName}"));
                    orderID = await instance.TravelBack(order.OrderId, currentGroup.GroupId, currentGroup.GroupCode, currentGroup.GroupName);
                    Service.Log.Information($"获取到订单号为: {orderID}");
                }
                else
                {
                    var areas = await instance.QueryGroupListTravelTarget(7, 5);
                    var selectedResult =
                        await WindowManager.Get<WorldSelectorWindows>()
                                           .OpenTravelWindow(false, true, false, areas, currentDcGroupName, currentWorld.InternalName.ToString());
                    if (selectedResult == null)
                    {
                        Service.Log.Info("取消传送");
                        return;
                    }

                    var chara = new Character { ContentId = contentId.ToString(), Name = currentCharacterName };
                    Service.Log.Info($"超域旅行: {selectedResult.Target.AreaName}@{selectedResult.Target.GroupName}");

                    targetDcGroupName = selectedResult.Target.AreaName;
                    var waitTime = await instance.QueryTravelQueueTime(selectedResult.Target.AreaId, selectedResult.Target.GroupId);
                    Service.Log.Info($"预计花费时间: {waitTime} 分钟");

                    var costMsgBox = await MessageBoxWindow.Show(WindowManager.WindowSystem, title, $"预计等待时间: {waitTime} 分钟", MessageBoxType.YesNo);
                    if (costMsgBox == MessageBoxResult.Yes)
                    {
                        await Service.Framework.RunOnFrameworkThread(GameFunctions.ReturnToTitle);
                        await Service.Framework.RunOnFrameworkThread(() => GameFunctions.OpenWaitAddon($"正在前往目标大区: {targetDcGroupName}\n预计等待时间: {waitTime} 分钟"));
                        orderID = await instance.TravelOrder(selectedResult.Target, selectedResult.Source, chara);
                        Service.Log.Information($"获取到订单号为: {orderID}");
                    }
                    else
                    {
                        Service.Log.Info("取消传送");
                        return;
                    }
                }

                Service.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "_TitleLogo", OnAddonTitleLogo);
                await WaitingForOrder(orderID);
                await Plugin.SelectDcAndLogin(targetDcGroupName);
            }
            catch (Exception ex)
            {
                await MessageBoxWindow.Show(WindowManager.WindowSystem, title, $"{title} 失败:\n{ex}", showWebsite: true);
                Service.Log.Error(ex.ToString());
            } 
            finally
            {
                GameFunctions.CloseWaitAddon();
                Service.AddonLifecycle.UnregisterListener(OnAddonTitleLogo);
            }
        });
    }

    private static async Task WaitingForOrder(string orderID)
    {
        OrderStatus status;
        
        GameFunctions.CloseTitleLogoAddon();
        while (true)
        {
            status = await DCTravelClient.Instance().QueryOrderStatus(orderID);
            Service.Log.Information($"当前订单状态: {status.Status}");
            
            GameFunctions.UpdateWaitAddon(StatusText.GetValueOrDefault(status.Status, "未知状态"));
            
            if (status.Status is not (MigrationStatus.InPrepare or MigrationStatus.InQueue)) break;
            await Task.Delay(2_000);
        }

        if (status.Status == MigrationStatus.Failed) 
            throw new Exception(status.CheckMessage);
    }

    private static MigrationOrder GetTravelingOrder(ulong contentId)
    {
        var contentIdStr   = contentId.ToString();
        var currentPageNum = 1;
        while (true)
        {
            var orders = DCTravelClient.Instance()!.QueryMigrationOrders(currentPageNum).Result;
            var order  = orders.Orders.First(x => x.Status == TravelStatus.Arrival && x.ContentId == contentIdStr);
            if (order == null)
            {
                var maxPageNum     = orders.TotalPageNum;
                currentPageNum++;
                if (currentPageNum > maxPageNum)
                {
                    Service.Log.Error($"未能找到返回订单 {contentId}");
                    throw new Exception("未能找到返回订单");
                }
            }
            else
                return order;
        }
    }
    
    private static void OnAddonTitleLogo(AddonEvent type, AddonArgs args) => 
        GameFunctions.CloseTitleLogoAddon();
} 
