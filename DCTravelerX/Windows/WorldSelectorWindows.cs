using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using DCTravelerX.Infos;
using DCTravelerX.Managers;
using DCTravelerX.Windows.MessageBox;
using DCTravelerX.Windows.Style;
using Lumina.Excel.Sheets;

namespace DCTravelerX.Windows;

internal class WorldSelectorWindows() : Window
                                        (
                                            "超域旅行",
                                            WindowStyles.DEFAULT_WINDOW_FLAGS
                                        ), IDisposable
{
    private enum SelectorViewMode
    {
        Travel,
        Settings
    }

    private bool                                     isBack;
    private bool                                     isConfirmDialogOpen;
    private bool                                     isTemporarilyHiddenForConfirmation;
    private uint                                     selectedSourceAreaID;
    private string                                   selectedSourceGroupName = string.Empty;
    private uint                                     selectedTargetAreaID;
    private string                                   selectedTargetGroupName = string.Empty;
    private TaskCompletionSource<SelectWorldResult>? selectWorldTaskCompletionSource;
    private bool                                     showSourceWorld = true;
    private bool                                     showTargetWorld = true;
    private SelectorViewMode                         viewMode        = SelectorViewMode.Travel;

    public void Dispose() { }

    public override void OnClose()
    {
        var isTemporarilyHidden = isTemporarilyHiddenForConfirmation;
        isTemporarilyHiddenForConfirmation = false;

        if (isTemporarilyHidden)
        {
            base.OnClose();
            return;
        }

        viewMode = SelectorViewMode.Travel;
        isConfirmDialogOpen = false;
        selectWorldTaskCompletionSource?.TrySetResult(null!);
        base.OnClose();
    }

    public override void PreDraw()
    {
        if (Service.GameGui.GetAddonByName("_CharaSelectListMenu").IsNull)
        {
            IsOpen = false;
            return;
        }

        var style    = ImGui.GetStyle();
        var viewport = ImGui.GetMainViewport();

        ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Appearing, new(0.5f));

        switch (viewMode)
        {
            case SelectorViewMode.Travel:
                Flags &= ~ImGuiWindowFlags.AlwaysAutoResize;

                var width  = 400f * ImGuiHelpers.GlobalScale;
                var height = MathF.Min(CalculateDesiredHeight(style), viewport.WorkSize.Y - style.WindowPadding.Y * 6f);
                ImGui.SetNextWindowSize(new Vector2(width, height), ImGuiCond.Always);
                break;
            case SelectorViewMode.Settings:
                Flags |= ImGuiWindowFlags.AlwaysAutoResize;
                break;
        }
    }

    public override void Draw()
    {
        EnsureValidSelectionState();

        using var selectorStyle = WindowStyles.PushWindowStyle();

        if (viewMode == SelectorViewMode.Settings)
            DrawSettingsView();
        else
            DrawTravelView();
    }

    private void DrawTravelView()
    {
        var hasPreviousSection = false;

        if (showSourceWorld)
        {
            DrawSelectorSection(ref selectedSourceAreaID, ref selectedSourceGroupName, 0);
            hasPreviousSection = true;
        }

        if (showTargetWorld)
        {
            if (hasPreviousSection)
                ImGui.Spacing();

            DrawSelectorSection(ref selectedTargetAreaID, ref selectedTargetGroupName, selectedSourceAreaID);
        }

        DrawTravelActions();
    }

    private void DrawSettingsView()
    {
        using var framePadding = ImRaii.PushStyle
        (
            ImGuiStyleVar.FramePadding,
            ImGui.GetStyle().FramePadding * WindowStyles.SettingsFramePaddingScale
        );

        var enableRetry = Service.Config.EnableAutoRetry;

        if (ImGui.Checkbox("跨区失败时自动重试", ref enableRetry))
        {
            Service.Config.EnableAutoRetry = enableRetry;
            Service.Config.Save();
        }

        using (ImRaii.Disabled(!enableRetry))
        {
            var allowSwitchToAvailableWorld = Service.Config.AllowSwitchToAvailableWorld;

            if (ImGui.Checkbox("目标繁忙时自动切换到同大区其他畅通服务器 (若存在)", ref allowSwitchToAvailableWorld))
            {
                Service.Config.AllowSwitchToAvailableWorld = allowSwitchToAvailableWorld;
                Service.Config.Save();
            }

            var maxRetryCount = Service.Config.MaxRetryCount;
            ImGui.SetNextItemWidth(GetSettingWidth());

            if (ImGui.InputInt("最大重试次数", ref maxRetryCount))
            {
                Service.Config.MaxRetryCount = Math.Clamp(maxRetryCount, 1, 999);
                Service.Config.Save();
            }

            var retryDelaySeconds = Service.Config.RetryDelaySeconds;
            ImGui.SetNextItemWidth(GetSettingWidth());

            if (ImGui.InputInt("重试间隔（秒）", ref retryDelaySeconds))
            {
                Service.Config.RetryDelaySeconds = Math.Clamp(retryDelaySeconds, 5, 3_600);
                Service.Config.Save();
            }
        }

        DrawSettingsActions();
    }

    private static void DrawSelectorSection(ref uint selectedAreaId, ref string selectedGroupName, uint excludeAreaId)
    {
        using var table = ImRaii.Table("##SelectorTable", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings);
        if (!table)
            return;

        var leftWidth = GetAreaColumnWidth();

        ImGui.TableSetupColumn("##AreaColumn",  ImGuiTableColumnFlags.WidthFixed, leftWidth);
        ImGui.TableSetupColumn("##GroupColumn", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        DrawAreaCards(ref selectedAreaId, ref selectedGroupName, excludeAreaId);

        ImGui.TableNextColumn();
        DrawGroupCards(selectedAreaId, ref selectedGroupName);
    }

    private static void DrawAreaCards(ref uint selectedAreaId, ref string selectedGroupName, uint excludeAreaId)
    {
        foreach (var area in GetOrderedAreas(excludeAreaId))
        {
            var areaStatus = WindowStyles.GetAreaStatus(area.State);

            if (WindowStyles.DrawSelectableCard
                (
                    $"##Area_{area.AreaId}",
                    area.AreaName,
                    areaStatus.Text,
                    selectedAreaId == (uint)area.AreaId,
                    areaStatus.Color,
                    GetCardHeight()
                ))
            {
                selectedAreaId    = (uint)area.AreaId;
                selectedGroupName = ResolveGroupName(selectedAreaId, null);
            }
        }
    }

    private static void DrawGroupCards(uint selectedAreaId, ref string selectedGroupName)
    {
        if (!TryGetArea(selectedAreaId, out var area))
            return;

        foreach (var group in area.GroupList)
        {
            var queueStatus = WindowStyles.GetQueueStatus(group.QueueTime);
            if (WindowStyles.DrawSelectableCard
                (
                    $"##Group_{selectedAreaId}_{group.GroupCode}_{group.GroupName}",
                    group.GroupName,
                    queueStatus.Text,
                    string.Equals(selectedGroupName, group.GroupName, StringComparison.Ordinal),
                    queueStatus.Color,
                    GetCardHeight()
                ))
                selectedGroupName = group.GroupName;
        }
    }

    private void DrawTravelActions()
    {
        var style        = ImGui.GetStyle();
        var buttonHeight = GetButtonHeight();
        var cancelWidth  = GetActionButtonWidth("取消");
        var spacing      = style.ItemSpacing.X;
        var actionWidth  = MathF.Max(0f, ImGui.GetContentRegionAvail().X - 2 * (cancelWidth + spacing));

        ImGui.Dummy(style.ItemSpacing with { X = 0f });

        if (WindowStyles.DrawActionButton("取消", new Vector2(cancelWidth, buttonHeight), ButtonVariant.Ghost))
        {
            selectWorldTaskCompletionSource?.TrySetResult(null!);
            IsOpen = false;
        }

        ImGui.SameLine(0f, spacing);
        if (WindowStyles.DrawActionButton("设置", new Vector2(cancelWidth, buttonHeight), ButtonVariant.Ghost))
            viewMode = SelectorViewMode.Settings;

        ImGui.SameLine(0f, spacing);

        using (ImRaii.Disabled(IsPrimaryActionDisabled()))
        {
            if (WindowStyles.DrawActionButton(GetPrimaryActionText(), new Vector2(actionWidth, buttonHeight), ButtonVariant.Primary))
                RequestPrimaryActionConfirmation();
        }
    }

    private void DrawSettingsActions()
    {
        var style        = ImGui.GetStyle();
        var buttonHeight = GetButtonHeight();
        var closeWidth   = GetActionButtonWidth("关闭");
        var backWidth    = MathF.Max(0f, ImGui.GetContentRegionAvail().X - closeWidth - style.ItemSpacing.X);

        ImGui.Dummy(style.ItemSpacing with { X = 0f });

        if (WindowStyles.DrawActionButton("关闭", new Vector2(closeWidth, buttonHeight), ButtonVariant.Ghost))
        {
            selectWorldTaskCompletionSource?.TrySetResult(null!);
            IsOpen = false;
        }

        ImGui.SameLine(0f, style.ItemSpacing.X);

        if (WindowStyles.DrawActionButton("返回跨区页面", new Vector2(backWidth, buttonHeight), ButtonVariant.Primary))
            viewMode = SelectorViewMode.Travel;
    }

    private static float GetCardHeight() =>
        ImGui.GetFrameHeightWithSpacing() * 1.5f;

    private static float GetButtonHeight() =>
        ImGui.GetFrameHeightWithSpacing() * 1.4f;

    private static float GetActionButtonWidth(string text)
    {
        var style    = ImGui.GetStyle();
        var textSize = ImGui.CalcTextSize(text);
        return textSize.X + style.FramePadding.X * 6f;
    }

    private static float GetSettingWidth()
    {
        var style = ImGui.GetStyle();
        return ImGui.CalcTextSize("0000000000").X + style.FramePadding.X * 4f;
    }

    private static float GetAreaColumnWidth()
    {
        var style = ImGui.GetStyle();
        return ImGui.CalcTextSize("目标大区测试").X + style.FramePadding.X * 6f + style.ItemInnerSpacing.X * 4f;
    }

    private float CalculateDesiredHeight(ImGuiStylePtr style)
    {
        var visibleSectionCount = (showSourceWorld ? 1 : 0) + (showTargetWorld ? 1 : 0);
        var sectionHeight       = GetSectionHeight(selectedSourceAreaID, 0);
        var targetSectionHeight = GetSectionHeight(selectedTargetAreaID, showSourceWorld ? selectedSourceAreaID : 0);
        var topHeight           = GetButtonHeight() + style.ItemSpacing.Y;
        var footerHeight        = GetButtonHeight() + style.ItemSpacing.Y * 2f;
        var sectionsHeight      = 0f;

        if (showSourceWorld)
            sectionsHeight += sectionHeight;

        if (showTargetWorld)
            sectionsHeight += targetSectionHeight;

        if (visibleSectionCount > 1)
            sectionsHeight += style.ItemSpacing.Y;

        if (viewMode == SelectorViewMode.Settings)
            sectionsHeight = GetButtonHeight() * 3f + style.ItemSpacing.Y * 3f;

        return style.WindowPadding.Y * 2f + topHeight + sectionsHeight + footerHeight;
    }

    private static float GetSectionHeight(uint selectedAreaId, uint excludeAreaId)
    {
        var style      = ImGui.GetStyle();
        var areaCount  = GetOrderedAreas(excludeAreaId).Count();
        var groupCount = TryGetArea(selectedAreaId, out var area) ? area.GroupList.Count : 0;
        var rowCount   = Math.Max(areaCount, groupCount);

        if (rowCount == 0)
            rowCount = 1;

        return rowCount * GetCardHeight() + Math.Max(0, rowCount - 1) * style.ItemSpacing.Y;
    }

    private string GetPrimaryActionText()
    {
        if (isBack && !showTargetWorld)
            return "开始返回";

        if (showTargetWorld)
            return isBack ? "确认并返回" : "开始跨区";

        return "确认选择";
    }

    private void RequestPrimaryActionConfirmation()
    {
        if (isConfirmDialogOpen)
            return;

        if (!TryBuildPrimaryConfirmation(out var title, out var message))
        {
            CompleteSelection();
            return;
        }

        isConfirmDialogOpen = true;
        isTemporarilyHiddenForConfirmation = true;
        IsOpen = false;

        _ = MessageBoxWindow.Show
        (
            WindowManager.WindowSystem,
            title,
            message,
            this,
            static (box, state) =>
            {
                if (state is not WorldSelectorWindows window)
                    return;

                window.isConfirmDialogOpen = false;

                if (box.Result is MessageBoxResult.Yes or MessageBoxResult.Ok)
                {
                    window.CompleteSelection();
                    return;
                }

                window.isTemporarilyHiddenForConfirmation = false;
                window.IsOpen = true;
            },
            MessageBoxType.YesNo
        );
    }

    private bool TryBuildPrimaryConfirmation(out string title, out string message)
    {
        if (showTargetWorld && TryGetSelectedTargetGroup(out var targetGroup))
        {
            var effectiveTargetGroup = ResolveEffectiveTargetGroup(targetGroup);
            var waitTimeMessage      = GetWaitTimeMessage(effectiveTargetGroup.QueueTime ?? 0);

            title   = "超域旅行";
            message = BuildTravelStatusMessage(targetGroup, effectiveTargetGroup, waitTimeMessage);
            return true;
        }

        if (isBack)
        {
            title   = "返回至原始大区";
            message = BuildReturnConfirmationMessage();
            return true;
        }

        title   = string.Empty;
        message = string.Empty;
        return false;
    }

    private void CompleteSelection()
    {
        isTemporarilyHiddenForConfirmation = false;
        selectWorldTaskCompletionSource?.TrySetResult
        (
            new SelectWorldResult
            {
                Source = selectedSourceGroupName,
                Target = selectedTargetGroupName
            }
        );
        IsOpen = false;
    }

    private bool IsPrimaryActionDisabled()
    {
        if (showSourceWorld && string.IsNullOrWhiteSpace(selectedSourceGroupName))
            return true;

        if (showTargetWorld && string.IsNullOrWhiteSpace(selectedTargetGroupName))
            return true;

        return showSourceWorld && showTargetWorld && selectedSourceAreaID == selectedTargetAreaID;
    }

    private static bool TryGetArea(uint areaId, out Area area)
    {
        if (areaId != 0 && DCTravelClient.Areas.TryGetValue(areaId, out var info))
        {
            area = info.Area;
            return true;
        }

        area = null!;
        return false;
    }

    private static IEnumerable<Area> GetOrderedAreas(uint excludeAreaId = 0) =>
        DCTravelClient.Areas
                      .OrderBy(x => x.Key)
                      .Select(x => x.Value.Area)
                      .Where(area => (uint)area.AreaId != excludeAreaId);

    private void EnsureValidSelectionState()
    {
        selectedSourceAreaID    = ResolveAreaID(selectedSourceAreaID, null, 0);
        selectedSourceGroupName = ResolveGroupName(selectedSourceAreaID, selectedSourceGroupName);

        selectedTargetAreaID    = ResolveAreaID(selectedTargetAreaID, null, showSourceWorld ? selectedSourceAreaID : 0);
        selectedTargetGroupName = ResolveGroupName(selectedTargetAreaID, selectedTargetGroupName);
    }

    private static uint ResolveAreaID(uint currentAreaId, string? preferredAreaName, uint excludeAreaId)
    {
        if (currentAreaId != 0             &&
            currentAreaId != excludeAreaId &&
            DCTravelClient.Areas.ContainsKey(currentAreaId))
            return currentAreaId;

        if (!string.IsNullOrWhiteSpace(preferredAreaName))
        {
            var matchedAreaId = DCTravelClient.Areas
                                              .FirstOrDefault
                                              (x => x.Key != excludeAreaId &&
                                                    string.Equals(x.Value.Area.AreaName, preferredAreaName, StringComparison.Ordinal)
                                              )
                                              .Key;

            if (matchedAreaId != 0)
                return matchedAreaId;
        }

        var fallbackArea = GetOrderedAreas(excludeAreaId).FirstOrDefault();
        return fallbackArea != null ? (uint)fallbackArea.AreaId : 0;
    }

    private static string ResolveGroupName(uint areaId, string? preferredGroupName)
    {
        if (!TryGetArea(areaId, out var area) || area.GroupList is not { Count: > 0 })
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(preferredGroupName))
        {
            var matchedGroup = area.GroupList.FirstOrDefault
            (group =>
                 string.Equals(group.GroupName, preferredGroupName, StringComparison.Ordinal) ||
                 string.Equals(group.GroupCode, preferredGroupName, StringComparison.Ordinal)
            );

            if (matchedGroup != null)
                return matchedGroup.GroupName;
        }

        return area.GroupList.First().GroupName;
    }

    private bool TryGetSelectedTargetGroup(out Group targetGroup)
    {
        if (selectedTargetAreaID != 0 &&
            DCTravelClient.Areas.TryGetValue(selectedTargetAreaID, out var targetArea) &&
            targetArea.Groups.TryGetValue(selectedTargetGroupName, out var foundGroup) &&
            foundGroup != null)
        {
            targetGroup = foundGroup;
            return true;
        }

        targetGroup = null!;
        return false;
    }

    private static Group ResolveEffectiveTargetGroup(Group requestedTargetGroup)
    {
        var allowSwitchToAvailableWorld = Service.Config.EnableAutoRetry && Service.Config.AllowSwitchToAvailableWorld;

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

    private string BuildReturnConfirmationMessage()
    {
        if (showTargetWorld && TryGetSelectedTargetGroup(out var targetGroup))
            return $"是否确认要返回原始大区: {targetGroup.AreaName}";

        if (selectedTargetAreaID != 0 && TryGetArea(selectedTargetAreaID, out var targetArea))
            return $"是否确认要返回原始大区: {targetArea.AreaName}";

        return "是否确认要返回原始大区";
    }

    public static void ConfirmReturnTravelFromContextMenu
    (
        int    targetWorldId,
        int    currentWorldId,
        ulong  contentId,
        bool   needSelectCurrentWorld,
        string currentCharacterName
    )
    {
        if (needSelectCurrentWorld)
        {
            TravelManager.Travel(targetWorldId, currentWorldId, contentId, true, true, currentCharacterName);
            return;
        }

        var message = BuildContextMenuReturnConfirmationMessage(targetWorldId);

        _ = MessageBoxWindow.Show
        (
            WindowManager.WindowSystem,
            "返回至原始大区",
            message,
            new ReturnTravelRequest(targetWorldId, currentWorldId, contentId, currentCharacterName),
            static (box, state) =>
            {
                if (box.Result is not MessageBoxResult.Yes or MessageBoxResult.Ok ||
                    state is not ReturnTravelRequest request)
                    return;

                TravelManager.Travel
                (
                    request.TargetWorldId,
                    request.CurrentWorldId,
                    request.ContentId,
                    true,
                    false,
                    request.CurrentCharacterName
                );
            },
            MessageBoxType.YesNo
        );
    }

    private static string BuildContextMenuReturnConfirmationMessage(int targetWorldId)
    {
        if (Service.DataManager.GetExcelSheet<World>().TryGetRow((uint)targetWorldId, out var targetWorld))
        {
            var targetDcName = targetWorld.DataCenter.Value.Name.ExtractText();
            return $"是否确认要返回原始大区: {targetDcName}";
        }

        return "是否确认要返回原始大区";
    }

    private sealed record ReturnTravelRequest(int TargetWorldId, int CurrentWorldId, ulong ContentId, string CurrentCharacterName);

    public Task<SelectWorldResult> OpenTravelWindow
    (
        bool    showSource,
        bool    showTarget,
        bool    isBackHome,
        string? currentDCName    = null,
        string? currentWorldCode = null,
        string? targetDCName     = null,
        string? targetWorldCode  = null
    )
    {
        selectWorldTaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        showSourceWorld = showSource;
        showTargetWorld = showTarget;
        isBack          = isBackHome;
        viewMode        = SelectorViewMode.Travel;

        selectedSourceAreaID    = ResolveAreaID(0, currentDCName, 0);
        selectedSourceGroupName = ResolveGroupName(selectedSourceAreaID, currentWorldCode);
        selectedTargetAreaID    = ResolveAreaID(0, targetDCName, showTarget ? selectedSourceAreaID : 0);
        selectedTargetGroupName = ResolveGroupName(selectedTargetAreaID, targetWorldCode);

        IsOpen = true;
        return selectWorldTaskCompletionSource.Task;
    }
}
