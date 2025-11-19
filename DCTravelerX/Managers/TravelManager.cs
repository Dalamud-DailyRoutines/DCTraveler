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
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;

namespace DCTravelerX.Managers;

public static class TravelManager
{
    private const int CooldownSeconds = 30;
    
    internal static readonly SemaphoreSlim TravelSemaphore = new(1, 1);
    private static           DateTime      lastTravelTime  = DateTime.MinValue;

    private static bool IsOnTravelling;

    private static readonly Dictionary<MigrationStatus, string> StatusText = new()
    {
        [MigrationStatus.TeleportFailed] = "超域旅行传送失败",
        [MigrationStatus.PreCheckFailed] = "超域旅行预检查失败",
        [MigrationStatus.InPrepare0]     = "检查目标大区角色信息中...",
        [MigrationStatus.InPrepare1]     = "检查目标大区角色信息中...",
        [MigrationStatus.NeedConfirm]    = "需要确认传送",
        [MigrationStatus.Processing3]    = "超域旅行排队中...",
        [MigrationStatus.Processing4]    = "超域旅行排队中...",
        [MigrationStatus.Completed]      = "超域旅行完成"
    };

    public static void Travel(
        int     targetWorldID,
        int     currentWorldID,
        ulong   contentID,
        bool    isBack,
        bool    needSelectCurrentWorld,
        string  currentCharacterName,
        string? errorMessage = null)
    {
        Task.Run(() => ExecuteTravelFlow(targetWorldID, currentWorldID, contentID, isBack, needSelectCurrentWorld,
            currentCharacterName, false, errorMessage));
    }

    internal static async Task ExecuteTravelFlow(
        int     targetWorldID,
        int     currentWorldID,
        ulong   contentId,
        bool    isBack,
        bool    needSelectCurrentWorld,
        string  currentCharacterName,
        bool    isIPCCall,
        string? errorMessage = null)
    {
        await TravelSemaphore.WaitAsync();
        
        try
        {
            IsOnTravelling = true;

            var isQueryingBefore = false;
            while (DCTravelClient.Instance().IsUpdatingAllQueryTime)
            {
                isQueryingBefore = true;
                await Task.Delay(100);
            }

            if (isQueryingBefore)
                await Task.Delay(2_000);
            
            Service.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "_TitleLogo", OnAddonTitleLogo);
            Service.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "_TitleMenu", OnAddonTitleMenu);

            var timeSinceLast = DateTime.UtcNow - lastTravelTime;
            if (timeSinceLast < TimeSpan.FromSeconds(CooldownSeconds))
            {
                var delay = TimeSpan.FromSeconds(CooldownSeconds) - timeSinceLast;
                Service.Log.Info($"传送请求过于频繁, 等待 {delay.TotalSeconds} 秒");

                await Service.Framework.RunOnFrameworkThread(() => GameFunctions.OpenWaitAddon(string.Empty));

                var waitStart = DateTime.UtcNow;
                while (DateTime.UtcNow - waitStart < delay)
                {
                    var remaining = delay - (DateTime.UtcNow - waitStart);
                    var message   = $"传送请求过于频繁\n请等待 {remaining.TotalSeconds:F0} 秒后自动继续...";
                    await Service.Framework.RunOnFrameworkThread(() => GameFunctions.UpdateWaitAddon(message));
                    await Task.Delay(500);
                }

                await Service.Framework.RunOnFrameworkThread(GameFunctions.CloseWaitAddon);
            }

            lastTravelTime = DateTime.UtcNow;

            var title = isBack ? "返回至原始大区" : "超域旅行";

            if (errorMessage != null)
            {
                await MessageBoxWindow.Show(WindowManager.WindowSystem, title, errorMessage);
                return;
            }

            if (!DCTravelClient.IsValid)
            {
                await MessageBoxWindow.Show(WindowManager.WindowSystem, title, "无法连接至超域旅行 API, 请检查 XIVLauncherCN 设置状态");
                Service.Log.Error("无法连接至 XIVLauncherCN 提供的超域旅行 API 服务");
                return;
            }

            var instance = DCTravelClient.Instance();

            try
            {
                if (!Service.DataManager.GetExcelSheet<World>().TryGetRow((uint)currentWorldID, out var currentWorld))
                    throw new Exception("无法获取当前服务器具体信息数据");

                var currentDCName = currentWorld.DataCenter.Value.Name.ExtractText();

                var currentGroup = DCTravelClient.CachedAreas
                                                 .FirstOrDefault(x => x.AreaName  == currentDCName)?.GroupList
                                                 .FirstOrDefault(x => x.GroupCode == currentWorld.InternalName.ExtractText());
                if (currentGroup == null)
                    throw new Exception("无法获取当前区域具体信息数据");

                var orderID           = string.Empty;
                var targetDCGroupName = string.Empty;
                if (isBack)
                {
                    if (!Service.DataManager.GetExcelSheet<World>().TryGetRow((uint)targetWorldID, out var targetWorld))
                        throw new Exception("无法获取目标服务器具体信息数据");

                    targetDCGroupName = targetWorld.DataCenter.Value.Name.ExtractText();

                    if (needSelectCurrentWorld && !isIPCCall)
                    {
                        var selectWorld = await WindowManager.Get<WorldSelectorWindows>()
                                                             .OpenTravelWindow(true,
                                                                               false,
                                                                               true,
                                                                               DCTravelClient.CachedAreas,
                                                                               currentDCName,
                                                                               currentGroup.GroupCode,
                                                                               targetDCGroupName,
                                                                               currentWorld.Name.ExtractText());
                        if (selectWorld == null) return;

                        currentGroup = selectWorld.Source;
                    }

                    Service.Log.Information($"当前区服: {currentWorld.Name}@{currentDCName}, 返回目标区服: {targetWorld.Name}@{targetDCGroupName}");

                    var order = await GetTravelingOrder(contentId);
                    Service.Log.Information($"返回原始大区订单号: {order.OrderId}");

                    if (Service.GameGui.GetAddonByName("_CharaSelectListMenu") != nint.Zero)
                        await Service.Framework.RunOnFrameworkThread(GameFunctions.ReturnToTitle);
                    await Service.Framework.RunOnFrameworkThread(() =>
                                                                     GameFunctions.OpenWaitAddon($"正在返回原始大区: {targetDCGroupName}"));

                    orderID = await instance.TravelBack(order.OrderId, currentGroup.GroupId, currentGroup.GroupCode,
                                                        currentGroup.GroupName);
                    Service.Log.Information($"订单: {orderID}");
                }
                else
                {
                    var    areas       = await instance.QueryGroupListTravelTarget(9, 5);
                    Group? targetGroup = null;

                    if (isIPCCall)
                    {
                        if (Service.DataManager.GetExcelSheet<World>()
                                               .TryGetRow((uint)targetWorldID, out var targetWorldIPC))
                        {
                            TryGetGroup(areas, targetWorldIPC.Name.ExtractText(), out var foundGroup);
                            targetGroup = foundGroup;
                        }

                        if (targetGroup == null || targetGroup.GroupId == 0)
                            throw new Exception($"[IPC] 无法找到目标服务器 {targetWorldID} 的信息。");
                    }
                    else
                    {
                        var selectedResult =
                            await WindowManager.Get<WorldSelectorWindows>()
                                               .OpenTravelWindow(false, 
                                                                 true, 
                                                                 false,
                                                                 areas, 
                                                                 currentDCName,
                                                                 currentWorld.InternalName.ToString());
                        if (selectedResult == null)
                        {
                            Service.Log.Info("取消传送");
                            lastTravelTime = DateTime.MinValue;
                            return;
                        }

                        targetGroup = selectedResult.Target;
                    }

                    var chara = new Character { ContentId = contentId.ToString(), Name = currentCharacterName };
                    Service.Log.Info($"超域旅行: {targetGroup.AreaName}@{targetGroup.GroupName}");

                    targetDCGroupName = targetGroup.AreaName;
                    var waitTime = targetGroup.QueueTime ?? 0;
                    Service.Log.Info($"预计花费时间: {waitTime} 分钟");

                    var waitTimeMessage = waitTime switch
                    {
                        0    => "即刻完成",
                        -999 => "繁忙",
                        _    => $"{waitTime} 分钟"
                    };
                    if (!isIPCCall)
                    {
                        
                        var costMsgBox = await MessageBoxWindow.Show(WindowManager.WindowSystem, title,
                                                                     $"超域传送状态: {waitTimeMessage}", MessageBoxType.YesNo);
                        if (costMsgBox != MessageBoxResult.Yes)
                        {
                            Service.Log.Info("取消传送");
                            lastTravelTime = DateTime.MinValue;
                            return;
                        }
                    }

                    await Service.Framework.RunOnFrameworkThread(GameFunctions.ReturnToTitle);
                    await Service.Framework.RunOnFrameworkThread(() => GameFunctions.OpenWaitAddon($"正在前往目标大区: {targetDCGroupName}\n预计需要等待: {waitTimeMessage}"));
                    orderID = await instance.TravelOrder(targetGroup, currentGroup, chara);
                    Service.Log.Information($"获取到订单号为: {orderID}");
                }

                await ProcessingOrder(orderID, targetDCGroupName, isIPCCall);
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
                Service.AddonLifecycle.UnregisterListener(OnAddonTitleMenu);

                GameFunctions.ToggleTitleMenu(true);
                GameFunctions.ToggleTitleLogo(true);
            }
        } 
        finally
        {
            TravelSemaphore.Release();
        }
    }

    public static async Task<MigrationOrder> GetTravelingOrder(ulong contentId)
    {
        var contentIdStr = contentId.ToString();
        var currentPageNum = 1;
        while (true)
        {
            var orders = await DCTravelClient.Instance().QueryMigrationOrders(currentPageNum);
            if (orders is not { Orders.Length: > 0 } ||
                orders.Orders.FirstOrDefault(x => x.ContentId == contentIdStr) is not { } order)
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

    private static async Task ProcessingOrder(string orderID, string targetDCGroupName, bool isIpcCall)
    {
        while (true)
        {
            var status = await DCTravelClient.Instance().QueryOrderStatus(orderID);
            Service.Log.Information($"当前订单状态: {status.Status}");

            GameFunctions.ResetTitleIdleTime();
            GameFunctions.UpdateWaitAddon(StatusText.GetValueOrDefault(status.Status, "未知状态"));

            if (status.Status == MigrationStatus.Completed)
                break;

            if (status.Status is MigrationStatus.TeleportFailed or MigrationStatus.PreCheckFailed)
                throw new Exception($"传送失败: {status.CheckMessage} {status.MigrationMessage}");

            if (status.Status == MigrationStatus.NeedConfirm)
            {
                var confirmResult = MessageBoxResult.Ok;
                if (!isIpcCall)
                {
                    confirmResult = await MessageBoxWindow.Show(WindowManager.WindowSystem,
                        "超域旅行确认",
                        $"是否确认要超域旅行至大区: {targetDCGroupName}",
                        MessageBoxType.OkCancel);
                }

                await DCTravelClient.Instance().MigrationConfirmOrder(orderID, confirmResult == MessageBoxResult.Ok);
                if (confirmResult != MessageBoxResult.Ok)
                    throw new Exception("传送失败: 已自行取消");

                continue;
            }

            if (status.Status is not (MigrationStatus.InPrepare0 or MigrationStatus.InPrepare1 or
                MigrationStatus.Processing3 or MigrationStatus.Processing4))
                continue;

            await Task.Delay(2000);
        }

        await GameFunctions.SelectDCAndLogin(targetDCGroupName);
        UIGlobals.PlaySoundEffect(67);
    }

    private static void OnAddonTitleLogo(AddonEvent type, AddonArgs args) =>
        GameFunctions.ToggleTitleLogo(false);
    
    private static void OnAddonTitleMenu(AddonEvent type, AddonArgs args) => 
        GameFunctions.ToggleTitleMenu(false);

    internal static async Task<string> CreateTravelOrder(int currentWorldID, int targetWorldId, ulong contentId, bool isBack, string currentCharacterName)
    {
        var orderID = string.Empty;
        if (!DCTravelClient.IsValid)
        {
            Service.Log.Error("无法连接至 XIVLauncherCN 提供的超域旅行 API 服务");
            return orderID;
        }

        var instance         = DCTravelClient.Instance();
        var worldSheet       = Service.DataManager.GetExcelSheet<World>();
        var currentWorldName = worldSheet.GetRow((uint)currentWorldID).Name.ExtractText();
        var targetWorldName  = worldSheet.GetRow((uint)targetWorldId).Name.ExtractText();
        var areas            = await instance.QueryGroupListTravelTarget(9, 5); // 获取全部大区信息
        var isGetSourceGroup = TryGetGroup(areas, currentWorldName, out var Source);

        if (isBack && isGetSourceGroup)
        {
            var order = await GetTravelingOrder(contentId);
            orderID = await instance.TravelBack(order.OrderId, Source.GroupId, Source.GroupCode,
                Source.GroupName);
            return orderID;
        }

        var isGetTargetGroup = TryGetGroup(areas, targetWorldName, out var Target);
        if (isGetSourceGroup && isGetTargetGroup)
        {
            var chara = new Character { ContentId = contentId.ToString(), Name = currentCharacterName };
            await instance.QueryTravelQueueTime(Target.AreaId, Target.GroupId);
            orderID = await instance.TravelOrder(Target, Source, chara);
        }

        return orderID;
    }
    
    internal static bool TryGetGroup(IEnumerable<Area> areas, string worldName, out Group t)
    {
        var matchedGroup = areas.SelectMany(area => area.GroupList)
                                .FirstOrDefault(group => group.GroupName == worldName);

        t = matchedGroup ?? new Group();
        return matchedGroup != null;
    }
}
