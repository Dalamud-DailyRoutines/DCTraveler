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

namespace DCTravelerX.Windows;

internal class DCGroupSelectorWindow() : Window
                                         (
                                             "选择大区",
                                             ImGuiWindowFlags.NoCollapse      |
                                             ImGuiWindowFlags.NoSavedSettings |
                                             ImGuiWindowFlags.NoResize        |
                                             ImGuiWindowFlags.NoScrollbar     |
                                             ImGuiWindowFlags.NoScrollWithMouse
                                         ), IDisposable
{
    private const float MINIMUM_PANEL_WIDTH = 220f;

    private TaskCompletionSource<string?> areaNameTaskCompletionSource = null!;

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

        var style = ImGui.GetStyle();

        using var windowPadding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, style.WindowPadding * new Vector2(1.15f, 1.1f));
        using var itemSpacing   = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing,   style.ItemSpacing   * new Vector2(1.1f,  1.05f));
        
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
            DrawAreaPanel(area);
        }
    }

    private void DrawAreaPanel(Area area)
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
        var rounding       = MathF.Max(style.FrameRounding, style.GrabRounding) * 2f;
        var borderSize     = MathF.Max(style.FrameBorderSize, style.WindowBorderSize + style.ChildBorderSize);
        var panelPadding   = style.FramePadding * new Vector2(1.45f, 1.35f);
        var panelWidth     = size.X - panelPadding.X * 2f;
        var dotRadius      = style.ItemSpacing.Y;
        var title          = area.AreaName;
        var state          = GetAreaStateText(area.State);
        var titleSize      = ImGui.CalcTextSize(title);
        var stateSize      = ImGui.CalcTextSize(state);
        var headerHeight   = MathF.Max(titleSize.Y, MathF.Max(stateSize.Y, dotRadius * 2f));
        var headerCenterY  = cursor.Y + panelPadding.Y + headerHeight * 0.5f;
        var titlePos       = new Vector2(cursor.X + panelPadding.X, headerCenterY - titleSize.Y * 0.5f);
        var stateRight     = max.X - panelPadding.X;
        var dotCenter      = new Vector2(stateRight  - stateSize.X - style.ItemInnerSpacing.X - dotRadius, headerCenterY);
        var statePos       = new Vector2(dotCenter.X + dotRadius   + style.ItemInnerSpacing.X,             headerCenterY - stateSize.Y * 0.5f);
        var dividerInset   = panelPadding.X + style.FramePadding.X * 0.9f;
        var dividerY       = cursor.Y + panelPadding.Y + headerHeight + style.ItemSpacing.Y * 1.15f;
        var groupStartY    = dividerY + style.ItemSpacing.Y                                 * 1.05f;
        var groupHeight    = GetGroupCardHeight();
        var groupRounding  = MathF.Max(style.FrameRounding, style.GrabRounding) * 1.45f;
        var groupTextColor = ImGui.GetColorU32(WithAlpha(KnownColor.Gainsboro,  0.94f));
        var titleColor     = ImGui.GetColorU32(WithAlpha(KnownColor.WhiteSmoke, held ? 0.88f : 1f));
        var background = hovered
                             ? WithAlpha(held ? KnownColor.SteelBlue : KnownColor.LightSteelBlue, held ? 0.84f : 0.72f)
                             : ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(ImGuiCol.WindowBg));
        var borderColor = hovered
                              ? WithAlpha(KnownColor.DeepSkyBlue,    0.95f)
                              : WithAlpha(KnownColor.LightSlateGray, 0.55f);

        drawList.AddRectFilled(cursor, max, ImGui.GetColorU32(background), rounding);
        drawList.AddRect(cursor, max, ImGui.GetColorU32(borderColor), rounding, 0, borderSize);
        drawList.AddText(titlePos, titleColor, title);
        drawList.AddCircleFilled(dotCenter, dotRadius, ImGui.GetColorU32(GetAreaStateColor(area.State)));
        drawList.AddText(statePos, ImGui.GetColorU32(WithAlpha(KnownColor.Gainsboro, 0.92f)), state);
        drawList.AddLine
        (
            new Vector2(cursor.X + dividerInset, dividerY),
            new Vector2(max.X    - dividerInset, dividerY),
            ImGui.GetColorU32(WithAlpha(KnownColor.SlateGray, 0.55f)),
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
                WithAlpha(KnownColor.LightSlateGray, 1f),
                groupTextColor
            );
        }
        else
        {
            for (var i = 0; i < area.GroupList.Count; i++)
            {
                var group = area.GroupList[i];
                DrawGroupLabel
                (
                    drawList,
                    new Vector2(cursor.X + panelPadding.X, groupStartY + i * (groupHeight + style.ItemSpacing.Y)),
                    panelWidth,
                    groupHeight,
                    groupRounding,
                    group.GroupName,
                    GetQueueStateText(group.QueueTime),
                    GetQueueStateColor(group.QueueTime),
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
        var fillColor = ImGui.GetColorU32(WithAlpha(KnownColor.LightSlateGray, 0.22f));
        var lineColor = ImGui.GetColorU32(WithAlpha(KnownColor.SteelBlue,      0.38f));

        drawList.AddRectFilled(position, max, fillColor, rounding);
        drawList.AddRect(position, max, lineColor, rounding);
        drawList.AddText(textPos, textColor, text);

        if (string.IsNullOrWhiteSpace(state))
            return;

        var stateSize  = ImGui.CalcTextSize(state);
        var dotRadius  = style.ItemSpacing.Y * 0.72f;
        var stateRight = max.X - style.FramePadding.X * 1.25f;
        var dotCenter  = new Vector2(stateRight - stateSize.X - style.ItemInnerSpacing.X - dotRadius, position.Y + height * 0.5f);
        var statePos   = new Vector2(dotCenter.X + dotRadius + style.ItemInnerSpacing.X, position.Y + (height - stateSize.Y) * 0.5f);

        drawList.AddCircleFilled(dotCenter, dotRadius, ImGui.GetColorU32(stateColor));
        drawList.AddText(statePos, ImGui.GetColorU32(WithAlpha(KnownColor.Gainsboro, 0.9f)), state);
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
                                 .Max() + 1;

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

    private static Vector4 WithAlpha(KnownColor knownColor, float alpha)
    {
        var color = Color.FromKnownColor(knownColor);
        return new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, alpha);
    }

    private static string GetAreaStateText(int state) =>
        state switch
        {
            0 => "通畅",
            1 => "热门",
            2 => "火爆",
            _ => "繁忙"
        };

    private static Vector4 GetAreaStateColor(int state) =>
        state switch
        {
            0 => WithAlpha(KnownColor.MediumSeaGreen, 1f),
            1 => WithAlpha(KnownColor.DarkOrange,     1f),
            _ => WithAlpha(KnownColor.IndianRed,      1f)
        };

    private static string GetQueueStateText(int? queueTime) =>
        queueTime switch
        {
            0   => "通畅",
            < 0 => "火爆",
            > 0 => $"{queueTime} 分钟",
            _   => "读取中"
        };

    private static Vector4 GetQueueStateColor(int? queueTime) =>
        queueTime switch
        {
            0             => WithAlpha(KnownColor.MediumSeaGreen, 1f),
            < 0           => WithAlpha(KnownColor.IndianRed,      1f),
            > 0 and <= 15 => WithAlpha(KnownColor.DeepSkyBlue,    1f),
            > 15          => WithAlpha(KnownColor.Goldenrod,      1f),
            _             => WithAlpha(KnownColor.LightSlateGray, 1f)
        };

    private static Area[] GetOrderedAreas() =>
        DCTravelClient.Areas
                      .OrderBy(x => x.Key)
                      .Select(x => x.Value.Area)
                      .ToArray();

    public async Task<string?> Open(SdoArea[] areas)
    {
        IsOpen                       = true;
        areaNameTaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        return await areaNameTaskCompletionSource.Task;
    }
}
