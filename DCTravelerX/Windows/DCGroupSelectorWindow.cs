using System;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using DCTravelerX.Helpers;
using DCTravelerX.Infos;
using DCTravelerX.Managers;
using DCTravelerX.Windows.MessageBox;
using DCTravelerX.Windows.Style;

namespace DCTravelerX.Windows;

internal class DCGroupSelectorWindow() : Window
                                         (
                                             "选择大区",
                                             WindowStyles.DEFAULT_WINDOW_FLAGS
                                         ), IDisposable
{
    private const float MINIMUM_PANEL_WIDTH = 220f;

    private TaskCompletionSource<string?> areaNameTaskCompletionSource = null!;
    private string?                       currentAreaName;

    public void Dispose() { }

    public override void PreDraw()
    {
        if (Service.GameGui.GetAddonByName("_TitleMenu").IsNull)
        {
            IsOpen = false;
            return;
        }

        var style    = ImGui.GetStyle();
        var viewport = ImGui.GetMainViewport();
        var areas    = GetOrderedAreas().ToArray();
        var width    = MathF.Min(CalculateDesiredWidth(4, style),         viewport.WorkSize.X - style.WindowPadding.X * 6f);
        var height   = MathF.Min(CalculateDesiredHeight(areas, 4, style), viewport.WorkSize.Y - style.WindowPadding.Y * 6f);

        ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Appearing, new(0.5f));
        ImGui.SetNextWindowSize(new Vector2(width, height), ImGuiCond.Always);
    }

    public override void Draw()
    {
        if (ServerDataManager.SdoAreas is not { Length: > 0 })
        {
            ImGui.TextUnformatted("服务器信息加载失败");
            return;
        }

        var areas = GetOrderedAreas().ToArray();

        if (areas.Length == 0)
        {
            ImGui.TextUnformatted("当前没有可用的大区信息");
            return;
        }

        using var selectorStyle = WindowStyles.PushWindowStyle();

        using var table = ImRaii.Table
        (
            "##DCGroupSelectorTable",
            4,
            ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoSavedSettings
        );

        if (!table)
            return;

        foreach (var area in areas)
        {
            ImGui.TableNextColumn();
            DrawAreaPanel(area, currentAreaName);
        }
    }

    private void DrawAreaPanel(Area area, string? areaName)
    {
        var style          = ImGui.GetStyle();
        var groupCount     = Math.Max(1, area.GroupList.Count);
        var size           = new Vector2(ImGui.GetContentRegionAvail().X, GetPanelHeight(groupCount));
        var cursor         = ImGui.GetCursorScreenPos();
        var clicked        = ImGui.InvisibleButton($"##{area.AreaId}_{area.AreaName}", size);
        var hovered        = ImGui.IsItemHovered();
        var held           = ImGui.IsItemActive();
        var drawList       = ImGui.GetWindowDrawList();
        var max            = cursor + size;
        var rounding       = WindowStyles.GetCardRounding();
        var borderSize     = WindowStyles.GetCardBorderSize(style);
        var panelPadding   = style.FramePadding * new Vector2(1.45f, 1.35f);
        var panelWidth     = size.X - panelPadding.X * 2f;
        var dotRadius      = style.ItemSpacing.Y;
        var title          = area.AreaName;
        var isCurrentArea  = string.Equals(area.AreaName, areaName, StringComparison.Ordinal);
        var checkMark      = "✓";
        var areaStatus     = WindowStyles.GetAreaStatus(area.State);
        var state          = areaStatus.Text;
        var titleSize      = ImGui.CalcTextSize(title);
        var checkMarkSize  = isCurrentArea ? ImGui.CalcTextSize(checkMark) : Vector2.Zero;
        var stateSize      = ImGui.CalcTextSize(state);
        var headerHeight   = MathF.Max(titleSize.Y, MathF.Max(stateSize.Y, dotRadius * 2f));
        var headerCenterY  = cursor.Y + panelPadding.Y + headerHeight * 0.5f;
        var titlePos       = new Vector2(cursor.X   + panelPadding.X, headerCenterY - titleSize.Y              * 0.5f);
        var checkMarkPos   = new Vector2(titlePos.X + titleSize.X                   + style.ItemInnerSpacing.X * 0.85f, headerCenterY - checkMarkSize.Y * 0.5f);
        var stateRight     = max.X - panelPadding.X;
        var dotCenter      = new Vector2(stateRight  - stateSize.X - style.ItemInnerSpacing.X - dotRadius, headerCenterY);
        var statePos       = new Vector2(dotCenter.X + dotRadius   + style.ItemInnerSpacing.X,             headerCenterY - stateSize.Y * 0.5f);
        var dividerInset   = panelPadding.X + style.FramePadding.X                                * 0.9f;
        var dividerY       = cursor.Y       + panelPadding.Y + headerHeight + style.ItemSpacing.Y * 1.15f;
        var groupStartY    = dividerY       + style.ItemSpacing.Y                                 * 1.05f;
        var groupHeight    = GetGroupCardHeight();
        var groupRounding  = MathF.Max(style.FrameRounding, style.GrabRounding) * 1.45f;
        var groupTextColor = WindowStyles.GetColorU32(KnownColor.Gainsboro,  0.94f);
        var titleColor     = WindowStyles.GetColorU32(KnownColor.WhiteSmoke, held ? 0.88f : 1f);
        var background = hovered
                             ? WindowStyles.WithAlpha(held ? KnownColor.SteelBlue : KnownColor.LightSteelBlue, held ? 0.84f : 0.72f)
                             : WindowStyles.GetWindowBackgroundColor();
        var borderColor = hovered
                              ? WindowStyles.WithAlpha(KnownColor.DeepSkyBlue,    0.95f)
                              : WindowStyles.WithAlpha(KnownColor.LightSlateGray, 0.55f);

        drawList.AddRectFilled(cursor, max, ImGui.GetColorU32(background), rounding);
        drawList.AddRect(cursor, max, ImGui.GetColorU32(borderColor), rounding, 0, borderSize);
        drawList.AddText(titlePos, titleColor, title);
        if (isCurrentArea)
            drawList.AddText(checkMarkPos, WindowStyles.GetColorU32(KnownColor.MediumSeaGreen, held ? 0.92f : 1f), checkMark);
        drawList.AddCircleFilled(dotCenter, dotRadius, ImGui.GetColorU32(areaStatus.Color));
        drawList.AddText(statePos, WindowStyles.GetColorU32(KnownColor.Gainsboro, 0.92f), state);
        drawList.AddLine
        (
            new Vector2(cursor.X + dividerInset, dividerY),
            new Vector2(max.X    - dividerInset, dividerY),
            WindowStyles.GetColorU32(KnownColor.SlateGray, 0.55f),
            MathF.Max(1f, borderSize)
        );

        if (area.GroupList.Count == 0)
        {
            DrawGroupLabel
            (
                drawList,
                new Vector2(cursor.X + panelPadding.X, groupStartY),
                panelWidth,
                groupHeight,
                groupRounding,
                "暂无分组数据",
                null,
                WindowStyles.WithAlpha(KnownColor.LightSlateGray, 1f),
                groupTextColor
            );
        }
        else
        {
            for (var i = 0; i < area.GroupList.Count; i++)
            {
                var group       = area.GroupList[i];
                var queueStatus = WindowStyles.GetQueueStatus(group.QueueTime);
                DrawGroupLabel
                (
                    drawList,
                    new Vector2(cursor.X + panelPadding.X, groupStartY + i * (groupHeight + style.ItemSpacing.Y)),
                    panelWidth,
                    groupHeight,
                    groupRounding,
                    group.GroupName,
                    queueStatus.Text,
                    queueStatus.Color,
                    groupTextColor
                );
            }
        }

        if (!clicked)
            return;

        Service.Framework.Run
        (async () =>
            {
                try
                {
                    await GameFunctions.SelectDCAndLogin(area.AreaName, true);
                }
                catch (Exception ex)
                {
                    await MessageBoxWindow.Show(WindowManager.WindowSystem, "选择大区", $"大区切换失败:\n{ex}", showWebsite: false);
                }
            }
        );
        IsOpen = false;
    }

    private static void DrawGroupLabel
    (
        ImDrawListPtr drawList,
        Vector2       position,
        float         width,
        float         height,
        float         rounding,
        string        text,
        string?       state,
        Vector4       stateColor,
        uint          textColor
    )
    {
        var style     = ImGui.GetStyle();
        var max       = position + new Vector2(width, height);
        var textSize  = ImGui.CalcTextSize(text);
        var textPos   = new Vector2(position.X + style.FramePadding.X * 1.25f, position.Y + (height - textSize.Y) * 0.5f);
        var fillColor = WindowStyles.GetColorU32(KnownColor.LightSlateGray, 0.22f);
        var lineColor = WindowStyles.GetColorU32(KnownColor.SteelBlue,      0.38f);

        drawList.AddRectFilled(position, max, fillColor, rounding);
        drawList.AddRect(position, max, lineColor, rounding);
        drawList.AddText(textPos, textColor, text);

        if (string.IsNullOrWhiteSpace(state))
            return;

        var stateSize  = ImGui.CalcTextSize(state);
        var dotRadius  = style.ItemSpacing.Y * 0.72f;
        var stateRight = max.X - style.FramePadding.X * 1.25f;
        var dotCenter  = new Vector2(stateRight  - stateSize.X - style.ItemInnerSpacing.X - dotRadius, position.Y + height                 * 0.5f);
        var statePos   = new Vector2(dotCenter.X + dotRadius   + style.ItemInnerSpacing.X,             position.Y + (height - stateSize.Y) * 0.5f);

        drawList.AddCircleFilled(dotCenter, dotRadius, ImGui.GetColorU32(stateColor));
        drawList.AddText(statePos, WindowStyles.GetColorU32(KnownColor.Gainsboro, 0.9f), state);
    }

    private static float CalculateDesiredWidth(int columns, ImGuiStylePtr style)
    {
        var clampedColumns = Math.Max(1, columns);
        return style.WindowPadding.X           * 2f                     +
               clampedColumns                  * GetMinimumPanelWidth() +
               Math.Max(0, clampedColumns - 1) * style.ItemSpacing.X;
    }

    private static float CalculateDesiredHeight(Area[] areas, int columns, ImGuiStylePtr style)
    {
        if (areas.Length == 0)
            return style.WindowPadding.Y * 2f + GetPanelHeight(1);

        var rowCount    = (int)Math.Ceiling(areas.Length / (float)Math.Max(1, columns));
        var totalHeight = 0f;

        for (var row = 0; row < rowCount; row++)
        {
            var maxGroups = areas.Skip(row * columns)
                                 .Take(columns)
                                 .Select(area => Math.Max(1, area.GroupList.Count))
                                 .DefaultIfEmpty(1)
                                 .Max() +
                            1;

            totalHeight += GetPanelHeight(maxGroups);
        }

        totalHeight += Math.Max(0, rowCount - 1) * style.ItemSpacing.Y;
        return style.WindowPadding.Y             * 2f + totalHeight;
    }

    private static float GetMinimumPanelWidth() =>
        MINIMUM_PANEL_WIDTH * ImGuiHelpers.GlobalScale;

    private static float GetPanelHeight(int groupCount)
    {
        var style         = ImGui.GetStyle();
        var panelPaddingY = style.FramePadding.Y * 1.35f;
        var headerHeight  = ImGui.GetTextLineHeight();
        var dividerSpace  = style.ItemSpacing.Y * 2.2f;
        var contentHeight = groupCount * GetGroupCardHeight() + Math.Max(0, groupCount - 1) * style.ItemSpacing.Y;

        return panelPaddingY * 2f + headerHeight + dividerSpace + contentHeight;
    }

    private static float GetGroupCardHeight() =>
        ImGui.GetFrameHeightWithSpacing() * 1.15f;

    private static Area[] GetOrderedAreas() =>
        DCTravelClient.Areas
                      .OrderBy(x => x.Key)
                      .Select(x => x.Value.Area)
                      .ToArray();

    public async Task<string?> Open(SdoArea[] areas)
    {
        IsOpen                       = true;
        areaNameTaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        currentAreaName              = GameFunctions.GetCurrentSdoAreaName();

        return await areaNameTaskCompletionSource.Task;
    }
}
