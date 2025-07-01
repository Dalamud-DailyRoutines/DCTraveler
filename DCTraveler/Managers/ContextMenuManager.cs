using System;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text;
using DCTraveler.Infos;
using DCTraveler.Windows;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace DCTraveler.Managers;

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

        if (Plugin.DcTravelClient is not { IsValid: true })
        {
            MessageBoxWindow.Show(WindowManager.WindowSystem, title, "无法连接超域API服务,请检查XL。");
            Service.Log.Error("Can not connect to XL");
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                var worldSheet         = Service.DataManager.GetExcelSheet<World>();
                var currentWorld       = worldSheet.GetRow((uint)currentWorldId);
                var currentDcGroupName = currentWorld.DataCenter.Value.Name.ExtractText();
                var currentGroup = Plugin.DcTravelClient.CachedAreas
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
                    orderID = await Plugin.DcTravelClient.TravelBack(order.OrderId, currentGroup.GroupId, currentGroup.GroupCode, currentGroup.GroupName);
                    Service.Log.Information($"获取到订单号为: {orderID}");
                }
                else
                {
                    var areas = await Plugin.DcTravelClient.QueryGroupListTravelTarget(7, 5);
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
                    var waitTime = await Plugin.DcTravelClient.QueryTravelQueueTime(selectedResult.Target.AreaId, selectedResult.Target.GroupId);
                    Service.Log.Info($"预计花费时间: {waitTime} 分钟");
                    
                    var costMsgBox = await MessageBoxWindow.Show(WindowManager.WindowSystem, title, $"预计花费时间: {waitTime} 分钟", MessageBoxType.YesNo);
                    if (costMsgBox == MessageBoxResult.Yes)
                    {
                        await Service.Framework.RunOnFrameworkThread(GameFunctions.ReturnToTitle);
                        orderID = await Plugin.DcTravelClient.TravelOrder(selectedResult.Target, selectedResult.Source, chara);
                        Service.Log.Information($"获取到订单号为: {orderID}");
                    }
                    else
                    {
                        Service.Log.Info("取消传送");
                        return;
                    }
                }

                await WaitingForOrder(orderID);
                await Plugin.SelectDcAndLogin(targetDcGroupName);
            }
            catch (Exception ex)
            {
                await MessageBoxWindow.Show(WindowManager.WindowSystem, title, $"{title} 失败:\n{ex}", showWebsite: true);
                Service.Log.Error(ex.ToString());
            } finally { WindowManager.Get<WaitingWindow>().IsOpen = false; }
        });
    }

    private static async Task WaitingForOrder(string orderId)
    {
        WindowManager.Get<WaitingWindow>().Open();
                                OrderStatus status;
        while (true)
        {
            status = await Plugin.DcTravelClient!.QueryOrderStatus(orderId);
            Service.Log.Information($"Current status:{status.Status}");
            WindowManager.Get<WaitingWindow>().Status = status.Status;
            if (!(status.Status is MigrationStatus.InPrepare or MigrationStatus.InQueue)) break;
            await Task.Delay(2000);
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
            var orders = Plugin.DcTravelClient!.QueryMigrationOrders(currentPageNum).Result;
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
} 
