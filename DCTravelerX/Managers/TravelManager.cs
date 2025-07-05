using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using DCTravelerX.Helpers;
using DCTravelerX.Infos;
using DCTravelerX.Windows;
using Lumina.Excel.Sheets;

namespace DCTravelerX.Managers;

public static class TravelManager
{
    private static readonly Dictionary<MigrationStatus, string> StatusText = new()
    {
        [MigrationStatus.TeleportFailed] = "超域旅行传送失败",
        [MigrationStatus.PreCheckFailed] = "超域旅行预检查失败",
        [MigrationStatus.InPrepare0] = "检查目标大区角色信息中...",
        [MigrationStatus.InPrepare1] = "检查目标大区角色信息中...",
        [MigrationStatus.NeedConfirm] = "需要确认传送",
        [MigrationStatus.Processing3] = "超域旅行排队中...",
        [MigrationStatus.Processing4] = "超域旅行排队中...",
        [MigrationStatus.Completed] = "超域旅行完成"
    };

    public static void Travel(
        int targetWorldId, int currentWorldId, ulong contentId, bool isBack, string currentCharacterName)
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
                var worldSheet = Service.DataManager.GetExcelSheet<World>();
                var currentWorld = worldSheet.GetRow((uint)currentWorldId);
                var currentDcGroupName = currentWorld.DataCenter.Value.Name.ExtractText();
                var currentGroup = instance.CachedAreas
                                           .First(x => x.AreaName == currentDcGroupName).GroupList
                                           .First(x => x.GroupCode == currentWorld.InternalName.ExtractText());
                var orderID = string.Empty;
                var targetDCGroupName = string.Empty;
                if (isBack)
                {
                    var targetWorld = worldSheet.GetRow((uint)targetWorldId);
                    targetDCGroupName = targetWorld.DataCenter.Value.Name.ToString();

                    var order = await GetTravelingOrder(contentId);
                    Service.Log.Information($"找到返回原始大区订单: {order.OrderId}");

                    await Service.Framework.RunOnFrameworkThread(GameFunctions.ReturnToTitle);
                    await Service.Framework.RunOnFrameworkThread(
                        () => GameFunctions.OpenWaitAddon($"正在返回原始大区: {targetDCGroupName}"));
                    orderID = await instance.TravelBack(order.OrderId, currentGroup.GroupId, currentGroup.GroupCode,
                                                        currentGroup.GroupName);
                    Service.Log.Information($"获取到订单号为: {orderID}");
                }
                else
                {
                    var areas = await instance.QueryGroupListTravelTarget(7, 5);

                    var selectedResult =
                        await WindowManager.Get<WorldSelectorWindows>()
                                           .OpenTravelWindow(false, true, false, areas, currentDcGroupName,
                                                             currentWorld.InternalName.ToString());
                    if (selectedResult == null)
                    {
                        Service.Log.Info("取消传送");
                        return;
                    }

                    var chara = new Character { ContentId = contentId.ToString(), Name = currentCharacterName };
                    Service.Log.Info($"超域旅行: {selectedResult.Target.AreaName}@{selectedResult.Target.GroupName}");

                    targetDCGroupName = selectedResult.Target.AreaName;
                    var waitTime =
                        await instance.QueryTravelQueueTime(selectedResult.Target.AreaId,
                                                            selectedResult.Target.GroupId);
                    Service.Log.Info($"预计花费时间: {waitTime} 分钟");

                    var costMsgBox = await MessageBoxWindow.Show(WindowManager.WindowSystem, title,
                                                                 $"预计等待时间: {waitTime} 分钟", MessageBoxType.YesNo);
                    if (costMsgBox == MessageBoxResult.Yes)
                    {
                        await Service.Framework.RunOnFrameworkThread(GameFunctions.ReturnToTitle);
                        await Service.Framework.RunOnFrameworkThread(
                            () => GameFunctions.OpenWaitAddon($"正在前往目标大区: {targetDCGroupName}\n预计等待时间: {waitTime} 分钟"));
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
                await WaitingForOrder(orderID, targetDCGroupName);
            }
            catch (Exception ex)
            {
                await MessageBoxWindow.Show(WindowManager.WindowSystem, title, $"{title} 失败:\n{ex}", showWebsite: true);
                Service.Log.Error(ex.ToString());
            } finally
            {
                GameFunctions.CloseWaitAddon();
                Service.AddonLifecycle.UnregisterListener(OnAddonTitleLogo);
            }
        });
    }

    public static async Task<MigrationOrder> GetTravelingOrder(ulong contentId)
    {
        var contentIdStr = contentId.ToString();
        var currentPageNum = 1;
        while (true)
        {
            var orders = await DCTravelClient.Instance().QueryMigrationOrders(currentPageNum);
            if (orders is not { Orders.Length: > 0 } ||
                orders.Orders.First(x => x.ContentId == contentIdStr) is not { } order)
            {
                var maxPageNum = orders.TotalPageNum;
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

    private static async Task WaitingForOrder(string orderID, string targetDCGroupName)
    {
        GameFunctions.CloseTitleLogoAddon();
        while (true)
        {
            var status = await DCTravelClient.Instance().QueryOrderStatus(orderID);
            Service.Log.Information($"当前订单状态: {status.Status}");

            GameFunctions.UpdateWaitAddon(StatusText.GetValueOrDefault(status.Status, "未知状态"));

            if (status.Status == MigrationStatus.Completed)
                break;

            if (status.Status is MigrationStatus.TeleportFailed or MigrationStatus.PreCheckFailed)
                throw new Exception($"传送失败: {status.CheckMessage} {status.MigrationMessage}");

            if (status.Status == MigrationStatus.NeedConfirm)
            {
                var confirmResult = await MessageBoxWindow.Show(WindowManager.WindowSystem,
                                                                "超域旅行确认",
                                                                $"是否确认要超域旅行至大区: {targetDCGroupName}",
                                                                MessageBoxType.OkCancel);
                await DCTravelClient.Instance().MigrationConfirmOrder(orderID, confirmResult == MessageBoxResult.Ok);
                if (confirmResult != MessageBoxResult.Ok)
                    return;

                continue;
            }

            if (status.Status is not (MigrationStatus.InPrepare0 or MigrationStatus.InPrepare1 or
                MigrationStatus.Processing3 or MigrationStatus.Processing4))
                continue;

            await Task.Delay(2000);
        }

        await Plugin.SelectDCAndLogin(targetDCGroupName);
    }

    private static void OnAddonTitleLogo(AddonEvent type, AddonArgs args) =>
        GameFunctions.CloseTitleLogoAddon();
}
