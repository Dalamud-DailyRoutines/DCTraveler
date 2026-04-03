using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DCTravelerX.Infos;
using DCTravelerX.Managers;
using DCTravelerX.Travel.Interaction;
using DCTravelerX.Travel.Models;
using DCTravelerX.Travel.Strategies;
using DCTravelerX.Windows;
using Lumina.Excel.Sheets;

namespace DCTravelerX.Travel.Services;

internal sealed class TravelContextResolver : ITravelContextResolver
{
    private readonly ITravelInteraction interaction;
    private readonly ITravelRetryPolicy retryPolicy;

    public TravelContextResolver(ITravelInteraction interaction, ITravelRetryPolicy retryPolicy)
    {
        this.interaction = interaction;
        this.retryPolicy = retryPolicy;
    }

    public async Task<TravelResolution> ResolveAsync(TravelRequest request, CancellationToken cancellationToken)
    {
        var sourceContext                   = GetSourceContext(request.CurrentWorldId);
        var retrySettings                   = retryPolicy.CreateSettings(request);
        var shouldReturnToTitleBeforeSubmit = true;

        if (request.IsBack)
        {
            if (!Service.DataManager.GetExcelSheet<World>().TryGetRow((uint)request.TargetWorldId, out var targetWorld))
                throw new Exception("无法获取目标服务器具体信息数据");

            var sourceGroup       = sourceContext.CurrentGroup;
            var targetDcGroupName = targetWorld.DataCenter.Value.Name.ExtractText();

            if (request.NeedSelectCurrentWorld && !request.IsIpcCall)
            {
                var result = await WindowManager.Get<WorldSelectorWindows>()
                                                .OpenTravelWindow
                                                (
                                                    true,
                                                    false,
                                                    true,
                                                    sourceContext.CurrentDcName,
                                                    sourceGroup.GroupCode,
                                                    targetDcGroupName,
                                                    sourceContext.CurrentWorld.Name.ExtractText()
                                                );

                if (result == null ||
                    DCTravelClient.Areas.SelectMany(x => x.Value.Groups.Values).FirstOrDefault(x => x.GroupName == result.Source) is not { } sourceGroupData)
                    return new(sourceContext, request.Title, targetDcGroupName, null, null, retrySettings, string.Empty, false, false);

                sourceContext = sourceContext with { CurrentGroup = sourceGroupData };
            }

            Service.Log.Information($"当前区服: {sourceContext.CurrentWorld.Name}@{sourceContext.CurrentDcName}, 返回目标区服: {targetWorld.Name}@{targetDcGroupName}");

            var order = await GetTravelingOrderAsync(request.ContentId);
            Service.Log.Information($"返回原始大区订单号: {order.OrderId}");

            shouldReturnToTitleBeforeSubmit = Service.GameGui.GetAddonByName("_CharaSelectListMenu") != nint.Zero;

            return new
            (
                sourceContext,
                request.Title,
                targetDcGroupName,
                null,
                null,
                retrySettings,
                $"正在返回原始大区: {targetDcGroupName}",
                true,
                shouldReturnToTitleBeforeSubmit,
                order.OrderId
            );
        }

        await DCTravelClient.Instance().QueryAllTravelTime();

        Group requestedTargetGroup;

        if (request.IsIpcCall) requestedTargetGroup = GetTargetGroupByWorldId(request.TargetWorldId, "[IPC] ");
        else
        {
            var result = await WindowManager.Get<WorldSelectorWindows>()
                                            .OpenTravelWindow(false, true, false, sourceContext.CurrentDcName, sourceContext.CurrentWorld.InternalName.ToString());

            if (result == null ||
                DCTravelClient.Areas.SelectMany(x => x.Value.Groups.Values).FirstOrDefault(x => x.GroupName == result.Target) is not { } targetGroupData)
            {
                Service.Log.Info("取消传送");
                return new(sourceContext, request.Title, string.Empty, null, null, retrySettings, string.Empty, false, false);
            }

            requestedTargetGroup = targetGroupData;
        }

        Service.Log.Info($"超域旅行: {requestedTargetGroup.AreaName}@{requestedTargetGroup.GroupName}");

        var effectiveTargetGroup = ResolveEffectiveTargetGroup(requestedTargetGroup, retrySettings.AllowSwitchToAvailableWorld);
        var waitTime             = effectiveTargetGroup.QueueTime ?? 0;
        var waitTimeMessage = waitTime switch
        {
            0   => "即刻完成",
            < 0 => "繁忙",
            _   => $"{waitTime} 分钟"
        };

        Service.Log.Info($"预计花费时间: {waitTime} 分钟");

        return new
        (
            sourceContext,
            request.Title,
            requestedTargetGroup.AreaName,
            requestedTargetGroup,
            effectiveTargetGroup,
            retrySettings,
            BuildWaitAddonMessage(requestedTargetGroup.AreaName, effectiveTargetGroup, requestedTargetGroup, waitTimeMessage),
            true,
            true
        );
    }

    public Group GetTargetGroupByWorldId(int targetWorldId, string errorPrefix = "")
    {
        if (!Service.DataManager.GetExcelSheet<World>().TryGetRow((uint)targetWorldId, out var targetWorldRow)       ||
            !DCTravelClient.WorldNameToAreaID.TryGetValue(targetWorldRow.Name.ToString(), out var targetWorldAreaId) ||
            !DCTravelClient.Areas.TryGetValue(targetWorldAreaId, out var targetAreaInfo)                             ||
            !targetAreaInfo.Groups.TryGetValue(targetWorldRow.Name.ToString(), out var foundGroup)                   ||
            foundGroup == null)
            throw new Exception($"{errorPrefix}无法找到目标服务器 {targetWorldId} 的信息。");

        return foundGroup;
    }

    public Group RefreshTargetGroup(Group requestedTargetGroup)
    {
        if (!DCTravelClient.Areas.TryGetValue((uint)requestedTargetGroup.AreaId, out var targetAreaInfo)  ||
            !targetAreaInfo.Groups.TryGetValue(requestedTargetGroup.GroupName, out var latestTargetGroup) ||
            latestTargetGroup == null)
            throw new Exception($"无法刷新目标服务器 {requestedTargetGroup.GroupName} 的状态信息。");

        return latestTargetGroup;
    }

    public Group ResolveEffectiveTargetGroup(Group requestedTargetGroup, bool allowSwitchToAvailableWorld)
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

    public async Task<MigrationOrder> GetTravelingOrderAsync(ulong contentId)
    {
        var contentIdText = contentId.ToString();
        var currentPage   = 1;

        while (true)
        {
            var orders = await DCTravelClient.Instance().QueryMigrationOrders(currentPage);

            if (orders is { Orders.Length: > 0 } &&
                orders.Orders.FirstOrDefault(x => x.ContentId == contentIdText) is { } order)
                return order;

            currentPage++;

            if (currentPage > orders.TotalPageNum)
            {
                Service.Log.Error($"未能找到返回订单 {contentId}");
                throw new Exception("未能找到返回订单");
            }
        }
    }

    private static TravelSourceContext GetSourceContext(int currentWorldId)
    {
        if (!Service.DataManager.GetExcelSheet<World>().TryGetRow((uint)currentWorldId, out var currentWorld) ||
            !DCTravelClient.WorldNameToAreaID.TryGetValue(currentWorld.Name.ToString(), out var areaId))
            throw new Exception("无法获取当前服务器具体信息数据");

        var currentDcName = currentWorld.DataCenter.Value.Name.ExtractText();
        var currentGroup  = DCTravelClient.Areas[areaId].Groups[currentWorld.Name.ToString()];

        if (currentGroup == null)
            throw new Exception("无法获取当前区域具体信息数据");

        return new(currentWorld, currentDcName, currentGroup);
    }

    private static string BuildWaitAddonMessage(string targetDcGroupName, Group effectiveTargetGroup, Group requestedTargetGroup, string waitTimeMessage)
    {
        var message = $"正在前往目标大区: {targetDcGroupName}\n预计需要等待: {waitTimeMessage}";

        if (!string.Equals(requestedTargetGroup.GroupName, effectiveTargetGroup.GroupName, StringComparison.Ordinal))
            message += $"\n已自动切换至服务器: {effectiveTargetGroup.GroupName}";

        return message;
    }
}
