using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Keys;
using DCTravelerX.Helpers;
using DCTravelerX.Infos;
using DCTravelerX.Windows;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;

namespace DCTravelerX.Managers;

public static class TravelManager
{
    internal static SemaphoreSlim TravelSemaphore { get; } = new(1, 1);
    
    private const int COOLDOWN_SECONDS = 60;

    private static           DateTime      LastTravelTime = DateTime.MinValue;
    private static           DateTime      LastCancelTime = DateTime.MinValue;

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

    public static void Travel
    (
        int     targetWorldID,
        int     currentWorldID,
        ulong   contentID,
        bool    isBack,
        bool    needSelectCurrentWorld,
        string  currentCharacterName,
        string? errorMessage = null
    ) =>
        Task.Run
        (() => ExecuteTravelFlow
         (
             targetWorldID,
             currentWorldID,
             contentID,
             isBack,
             needSelectCurrentWorld,
             currentCharacterName,
             false,
             errorMessage
         )
        );

    internal static async Task ExecuteTravelFlow
    (
        int     targetWorldID,
        int     currentWorldID,
        ulong   contentID,
        bool    isBack,
        bool    needSelectCurrentWorld,
        string  currentCharacterName,
        bool    isIPCCall,
        string? errorMessage = null
    )
    {
        await TravelSemaphore.WaitAsync();

        var withAnyException = false;

        try
        {
            IsOnTravelling = true;
            await PrepareForTravel();

            var title = isBack ? "返回至原始大区" : "超域旅行";

            if (errorMessage != null)
            {
                await MessageBoxWindow.Show(WindowManager.WindowSystem, title, errorMessage);
                return;
            }

            if (!DCTravelClient.IsValid)
            {
                await MessageBoxWindow.Show(WindowManager.WindowSystem, title, "无法连接至超域旅行 API, 请从 XIVLauncherCN 重新启动游戏");
                Service.Log.Error("无法连接至 XIVLauncherCN 提供的超域旅行 API 服务");
                return;
            }

            try
            {
                var (currentWorld, currentDCName, currentGroup) = GetSourceContext(currentWorldID);

                var (targetDCGroupName, targetGroup, enableRetry, allowSwitchToAvailableWorld, retryCount, shouldContinue) = await PrepareOrderContext
                                                                                                                             (
                                                                                                                                 isBack,
                                                                                                                                 targetWorldID,
                                                                                                                                 contentID,
                                                                                                                                 currentGroup,
                                                                                                                                 currentDCName,
                                                                                                                                 currentWorld,
                                                                                                                                 isIPCCall,
                                                                                                                                 title,
                                                                                                                                 needSelectCurrentWorld
                                                                                                                             );

                if (!shouldContinue)
                    return;

                await ApplyCooldownBeforeProcessing();

                await ExecuteOrderWithRetry
                (
                    targetDCGroupName,
                    targetGroup,
                    enableRetry,
                    allowSwitchToAvailableWorld,
                    retryCount,
                    isIPCCall,
                    isBack,
                    targetWorldID,
                    contentID,
                    currentGroup,
                    currentCharacterName
                );
            }
            catch (Exception ex)
            {
                await MessageBoxWindow.Show(WindowManager.WindowSystem, title, $"{title} 失败:\n{ex.Message}", showWebsite: true);
                Service.Log.Error(ex, "跨大区失败");

                withAnyException = true;
            }
            finally
            {
                CleanupAfterTravel(withAnyException);
            }
        }
        finally
        {
            TravelSemaphore.Release();
        }
    }

    private static async Task PrepareForTravel()
    {
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
    }

    private static async Task ApplyCooldownBeforeProcessing()
    {
        var timeSinceTravel = DateTime.UtcNow - LastTravelTime;
        var timeSinceCancel = DateTime.UtcNow - LastCancelTime;
        var timeSinceLast   = timeSinceTravel < timeSinceCancel ? timeSinceTravel : timeSinceCancel;

        if (timeSinceLast < TimeSpan.FromSeconds(COOLDOWN_SECONDS))
        {
            var delay = TimeSpan.FromSeconds(COOLDOWN_SECONDS) - timeSinceLast;
            Service.Log.Info($"传送请求过于频繁, 等待 {delay.TotalSeconds} 秒");

            var waitStart = DateTime.UtcNow;

            while (DateTime.UtcNow - waitStart < delay)
            {
                var remaining = delay - (DateTime.UtcNow - waitStart);
                var message   = $"传送请求过于频繁\n等待 {remaining.TotalSeconds:F0} 秒后自动继续……";
                await Service.Framework.RunOnFrameworkThread(() => GameFunctions.OpenWaitAddon(message));
                await Service.Framework.RunOnFrameworkThread(() => GameFunctions.UpdateWaitAddon(message));
                await Task.Delay(500);
            }

            await Service.Framework.RunOnFrameworkThread(GameFunctions.CloseWaitAddon);
        }

        LastTravelTime = DateTime.UtcNow;
    }

    private static (World currentWorld, string currentDCName, Group currentGroup) GetSourceContext(int currentWorldID)
    {
        if (!Service.DataManager.GetExcelSheet<World>().TryGetRow((uint)currentWorldID, out var currentWorld) ||
            !DCTravelClient.WorldNameToAreaID.TryGetValue(currentWorld.Name.ToString(), out var areaID))
            throw new Exception("无法获取当前服务器具体信息数据");

        var currentDCName = currentWorld.DataCenter.Value.Name.ExtractText();
        var foundGroup    = DCTravelClient.Areas[areaID].Groups[currentWorld.Name.ToString()];
        if (foundGroup == null)
            throw new Exception("无法获取当前区域具体信息数据");

        return (currentWorld, currentDCName, foundGroup);
    }

    private static async
        Task<(string targetDCGroupName, Group? targetGroup, bool enableRetry, bool allowSwitchToAvailableWorl, int retryCount, bool shouldContinue)>
        PrepareOrderContext
        (
            bool   isBack,
            int    targetWorldID,
            ulong  contentId,
            Group  sourceGroup,
            string currentDCName,
            World  currentWorld,
            bool   isIPCCall,
            string title,
            bool   needSelectCurrentWorld
        )
    {
        var instance = DCTravelClient.Instance();

        if (isBack)
        {
            if (!Service.DataManager.GetExcelSheet<World>().TryGetRow((uint)targetWorldID, out var targetWorld))
                throw new Exception("无法获取目标服务器具体信息数据");

            var targetDCGroupName = targetWorld.DataCenter.Value.Name.ExtractText();

            if (needSelectCurrentWorld && !isIPCCall)
            {
                var result = await WindowManager.Get<WorldSelectorWindows>()
                                                .OpenTravelWindow
                                                (
                                                    true,
                                                    false,
                                                    true,
                                                    currentDCName,
                                                    sourceGroup.GroupCode,
                                                    targetDCGroupName,
                                                    currentWorld.Name.ExtractText()
                                                );
                if (result == null ||
                    DCTravelClient.Areas.SelectMany(x => x.Value.Groups.Values).FirstOrDefault(x => x.GroupName == result.Source) is not { } sourceGroupData)
                    return (string.Empty, null, false, false, 0, false);

                sourceGroup = sourceGroupData;
            }

            Service.Log.Information($"当前区服: {currentWorld.Name}@{currentDCName}, 返回目标区服: {targetWorld.Name}@{targetDCGroupName}");

            var order = await GetTravelingOrder(contentId);
            Service.Log.Information($"返回原始大区订单号: {order.OrderId}");

            if (Service.GameGui.GetAddonByName("_CharaSelectListMenu") != nint.Zero)
                await Service.Framework.RunOnFrameworkThread(GameFunctions.ReturnToTitle);

            await Service.Framework.RunOnFrameworkThread(() => GameFunctions.OpenWaitAddon($"正在返回原始大区: {targetDCGroupName}"));

            return (targetDCGroupName, null, false, false, 0, true);
        }
        else
        {
            await instance.QueryAllTravelTime();
            Group? targetGroup = null;

            if (isIPCCall)
            {
                targetGroup = GetTargetGroupByWorldId(targetWorldID, "[IPC] ");

                if (targetGroup == null || targetGroup.GroupID == 0)
                    throw new Exception($"[IPC] 无法找到目标服务器 {targetWorldID} 的信息。");
            }
            else
            {
                var result = await WindowManager.Get<WorldSelectorWindows>()
                                                .OpenTravelWindow(false, true, false, currentDCName, currentWorld.InternalName.ToString());

                if (result == null ||
                    DCTravelClient.Areas.SelectMany(x => x.Value.Groups.Values).FirstOrDefault(x => x.GroupName == result.Target) is not { } targetGroupData)
                {
                    Service.Log.Info("取消传送");
                    LastTravelTime = DateTime.MinValue;
                    return (string.Empty, null, false, false, 0, false);
                }

                targetGroup = targetGroupData;
            }

            Service.Log.Info($"超域旅行: {targetGroup.AreaName}@{targetGroup.GroupName}");

            var enableRetry                 = Service.Config.EnableAutoRetry;
            var allowSwitchToAvailableWorld = enableRetry && Service.Config.AllowSwitchToAvailableWorld;
            var effectiveTargetGroup        = GetTravelTargetGroup(targetGroup, allowSwitchToAvailableWorld);
            var targetDCGroupName           = targetGroup.AreaName;
            var waitTime                    = effectiveTargetGroup.QueueTime ?? 0;
            Service.Log.Info($"预计花费时间: {waitTime} 分钟");

            var waitTimeMessage = GetWaitTimeMessage(waitTime);
            var statusMessage   = BuildTravelStatusMessage(targetGroup, effectiveTargetGroup, waitTimeMessage);

            if (!isIPCCall)
            {
                var costMsgBox = await MessageBoxWindow.Show(WindowManager.WindowSystem, title, statusMessage, MessageBoxType.YesNo);

                if (costMsgBox != MessageBoxResult.Yes)
                {
                    Service.Log.Info("取消传送");
                    LastTravelTime = DateTime.MinValue;
                    return (string.Empty, null, false, false, 0, false);
                }
            }

            await Service.Framework.RunOnFrameworkThread(GameFunctions.ReturnToTitle);
            await Service.Framework.RunOnFrameworkThread
                (() => GameFunctions.OpenWaitAddon(BuildWaitAddonMessage(targetDCGroupName, effectiveTargetGroup, targetGroup, waitTimeMessage)));

            var retryCount = Service.Config.MaxRetryCount;

            return (targetDCGroupName, targetGroup, enableRetry, allowSwitchToAvailableWorld, retryCount, true);
        }
    }

    private static async Task ExecuteOrderWithRetry
    (
        string targetDCGroupName,
        Group? requestedTargetGroup,
        bool   enableRetry,
        bool   allowSwitchToAvailableWorld,
        int    maxRetries,
        bool   isIPCCall,
        bool   isBack,
        int    targetWorldID,
        ulong  contentId,
        Group  currentGroup,
        string currentCharacterName
    )
    {
        if (isIPCCall || isBack)
            enableRetry = false;

        allowSwitchToAvailableWorld &= enableRetry;

        var        retryCount    = 0;
        Exception? lastException = null;
        var        userCancelled = false;

        while (retryCount <= maxRetries)
        {
            if (enableRetry && Service.KeyState[VirtualKey.SHIFT])
            {
                Service.Log.Info("检测到 Shift 键按下，等待当前订单完成后取消");
                userCancelled = true;
            }

            try
            {
                string currentOrderID;

                if (isBack)
                {
                    var order    = await GetTravelingOrder(contentId);
                    var instance = DCTravelClient.Instance();
                    currentOrderID = await instance.TravelBack(order.OrderId, currentGroup.GroupID, currentGroup.GroupCode, currentGroup.GroupName);
                    Service.Log.Information($"返回订单号: {currentOrderID}");
                }
                else
                {
                    var instance = DCTravelClient.Instance();

                    if (requestedTargetGroup == null)
                    {
                        if (isIPCCall)
                            requestedTargetGroup = GetTargetGroupByWorldId(targetWorldID, "[IPC] ");
                        else
                            throw new Exception("非 IPC 调用时必须提供目标组信息");
                    }

                    await instance.QueryAllTravelTime();

                    requestedTargetGroup = RefreshTargetGroup(requestedTargetGroup);
                    var effectiveTargetGroup = GetTravelTargetGroup(requestedTargetGroup, allowSwitchToAvailableWorld);

                    if (!string.Equals(effectiveTargetGroup.GroupName, requestedTargetGroup.GroupName, StringComparison.Ordinal))
                        Service.Log.Information($"目标服务器 {requestedTargetGroup.GroupName} 当前繁忙，自动切换至同大区通畅服务器 {effectiveTargetGroup.GroupName}");

                    var chara = new Character { ContentId = contentId.ToString(), Name = currentCharacterName };
                    currentOrderID = await instance.TravelOrder(effectiveTargetGroup, currentGroup, chara);
                    Service.Log.Information($"订单号: {currentOrderID}，目标服务器: {effectiveTargetGroup.GroupName} (尝试 {retryCount + 1}/{maxRetries + 1})");
                }

                await ProcessingOrder(currentOrderID, targetDCGroupName, isIPCCall);

                if (userCancelled)
                    LastCancelTime = DateTime.UtcNow;

                return;
            }
            catch (Exception ex)
            {
                Service.Log.Info($"捕获异常 - EnableRetry: {enableRetry}, RetryCount: {retryCount}, MaxRetries: {maxRetries}");
                Service.Log.Info($"异常消息: {ex.Message}");

                if (userCancelled)
                {
                    LastCancelTime = DateTime.UtcNow;
                    throw new Exception("取消了传送操作");
                }

                if (!enableRetry || retryCount >= maxRetries)
                {
                    Service.Log.Info("不重试,直接抛出异常");
                    throw;
                }

                var isRetryableError = IsRetryableError(ex);
                Service.Log.Info($"是否可重试错误: {isRetryableError}");

                if (!isRetryableError)
                {
                    Service.Log.Info("不可重试错误,抛出异常");
                    throw;
                }

                lastException = ex;
                retryCount++;

                Service.Log.Warning($"传送失败 (尝试 {retryCount}/{maxRetries}): {ex.Message}");

                if (retryCount > maxRetries)
                    break;

                var retryDelay = TimeSpan.FromSeconds(Service.Config.RetryDelaySeconds);
                var waitStart  = DateTime.UtcNow;

                while (DateTime.UtcNow - waitStart < retryDelay)
                {
                    if (Service.KeyState[VirtualKey.SHIFT])
                    {
                        LastCancelTime = DateTime.UtcNow;
                        throw new Exception("取消了传送操作");
                    }

                    var remaining    = retryDelay - (DateTime.UtcNow - waitStart);
                    var errorMsg     = ExtractErrorMessage(ex);
                    var waitAddonMsg = $"错误: {errorMsg}\n第 {retryCount}/{maxRetries} 次重试 / 等待 {remaining.TotalSeconds:F0} 秒后重试 (按住 Shift 取消传送)";
                    await Service.Framework.RunOnFrameworkThread(() => GameFunctions.UpdateWaitAddon(waitAddonMsg));
                    await Task.Delay(1000);
                }

                Service.Log.Info($"开始第 {retryCount} 次重试...");
            }
        }

        if (lastException != null)
        {
            Service.Log.Error($"传送失败,已达到最大重试次数 ({maxRetries})");
            throw lastException;
        }
    }

    private static Group GetTargetGroupByWorldId(int targetWorldID, string errorPrefix = "")
    {
        if (!Service.DataManager.GetExcelSheet<World>().TryGetRow((uint)targetWorldID, out var targetWorldRow)       ||
            !DCTravelClient.WorldNameToAreaID.TryGetValue(targetWorldRow.Name.ToString(), out var targetWorldAreaID) ||
            !DCTravelClient.Areas.TryGetValue(targetWorldAreaID, out var targetAreaInfo)                             ||
            !targetAreaInfo.Groups.TryGetValue(targetWorldRow.Name.ToString(), out var foundGroup)                   ||
            foundGroup == null)
            throw new Exception($"{errorPrefix}无法找到目标服务器 {targetWorldID} 的信息。");

        return foundGroup;
    }

    private static Group RefreshTargetGroup(Group requestedTargetGroup)
    {
        if (!DCTravelClient.Areas.TryGetValue((uint)requestedTargetGroup.AreaId, out var targetAreaInfo)  ||
            !targetAreaInfo.Groups.TryGetValue(requestedTargetGroup.GroupName, out var latestTargetGroup) ||
            latestTargetGroup == null)
            throw new Exception($"无法刷新目标服务器 {requestedTargetGroup.GroupName} 的状态信息。");

        return latestTargetGroup;
    }

    private static Group GetTravelTargetGroup(Group requestedTargetGroup, bool allowSwitchToAvailableWorld)
    {
        if (!allowSwitchToAvailableWorld || requestedTargetGroup.QueueTime is null or >= 0)
            return requestedTargetGroup;

        if (!DCTravelClient.Areas.TryGetValue((uint)requestedTargetGroup.AreaId, out var targetAreaInfo))
            return requestedTargetGroup;

        return targetAreaInfo.Groups.Values
                             .OrderBy(group => group.GroupID)
                             .FirstOrDefault
                             (group => group.QueueTime == 0 &&
                                       !string.Equals(group.GroupName, requestedTargetGroup.GroupName, StringComparison.Ordinal)
                             ) ??
               requestedTargetGroup;
    }

    private static string GetWaitTimeMessage(int waitTime) =>
        waitTime switch
        {
            0   => "即刻完成",
            < 0 => "繁忙",
            _   => $"{waitTime} 分钟"
        };

    private static string BuildTravelStatusMessage(Group requestedTargetGroup, Group effectiveTargetGroup, string waitTimeMessage)
    {
        if (string.Equals(requestedTargetGroup.GroupName, effectiveTargetGroup.GroupName, StringComparison.Ordinal))
            return $"超域传送状态: {waitTimeMessage}";

        return $"超域传送状态: 目标服务器 {requestedTargetGroup.GroupName} 当前繁忙\n将自动切换至同大区通畅服务器 {effectiveTargetGroup.GroupName}\n预计需要等待: {waitTimeMessage}";
    }

    private static string BuildWaitAddonMessage(string targetDCGroupName, Group effectiveTargetGroup, Group requestedTargetGroup, string waitTimeMessage)
    {
        var message = $"正在前往目标大区: {targetDCGroupName}\n预计需要等待: {waitTimeMessage}";

        if (!string.Equals(requestedTargetGroup.GroupName, effectiveTargetGroup.GroupName, StringComparison.Ordinal))
            message += $"\n已自动切换至服务器: {effectiveTargetGroup.GroupName}";

        return message;
    }

    private static bool IsRetryableError(Exception ex)
    {
        var message = ex.Message;
        return message.Contains("传送失败")     ||
               message.Contains("繁忙")       ||
               message.Contains("请您稍晚再次尝试") ||
               message.Contains("稍晚再次尝试")   ||
               message.Contains("用户数量较多");
    }

    private static string ExtractErrorMessage(Exception ex)
    {
        const string PREFIX  = "消息:";

        var message = ex.Message;

        var parts = message.Split(PREFIX, 2);
        if (parts.Length == 2)
            message = parts[1].Trim();

        message = message.Split("\nat ",      2)[0].Trim();
        message = message.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[0].Trim();

        return message.Length > 150
                   ? message[..150] + "..."
                   : message;
    }


    private static void CleanupAfterTravel(bool needReLogin)
    {
        GameFunctions.CloseWaitAddon();
        Service.AddonLifecycle.UnregisterListener(OnAddonTitleLogo);
        Service.AddonLifecycle.UnregisterListener(OnAddonTitleMenu);
        GameFunctions.ToggleTitleMenu(true);
        GameFunctions.ToggleTitleLogo(true);

        if (needReLogin)
            GameFunctions.LoginInGame();
    }

    public static async Task<MigrationOrder> GetTravelingOrder(ulong contentId)
    {
        var contentIdStr   = contentId.ToString();
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

    private static async Task ProcessingOrder(string orderID, string targetDCGroupName, bool isIPCCall)
    {
        while (true)
        {
            var status = await DCTravelClient.Instance().QueryOrderStatus(orderID);
            Service.Log.Information($"当前订单状态: {status.Status}");

            GameFunctions.ResetTitleIdleTime();
            GameFunctions.OpenWaitAddon(StatusText.GetValueOrDefault(status.Status,   "未知状态"));
            GameFunctions.UpdateWaitAddon(StatusText.GetValueOrDefault(status.Status, "未知状态"));

            if (status.Status == MigrationStatus.Completed)
                break;

            if (status.Status is MigrationStatus.TeleportFailed or MigrationStatus.PreCheckFailed)
                throw new Exception($"传送失败: {status.CheckMessage} {status.MigrationMessage}");

            if (status.Status == MigrationStatus.NeedConfirm)
            {
                var confirmResult = MessageBoxResult.Ok;

                if (!isIPCCall)
                {
                    confirmResult = await MessageBoxWindow.Show
                                    (
                                        WindowManager.WindowSystem,
                                        "超域旅行确认",
                                        $"是否确认要超域旅行至大区: {targetDCGroupName}",
                                        MessageBoxType.OkCancel
                                    );
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

        await GameFunctions.SelectDCAndLogin(targetDCGroupName, !isIPCCall);
        UIGlobals.PlaySoundEffect(67);
    }

    private static void OnAddonTitleLogo(AddonEvent type, AddonArgs args) =>
        GameFunctions.ToggleTitleLogo(false);

    private static void OnAddonTitleMenu(AddonEvent type, AddonArgs args) =>
        GameFunctions.ToggleTitleMenu(false);
}
