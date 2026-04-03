using System;
using System.Drawing;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace DCTravelerX.Windows.Style;

internal static class WindowStyles
{
    public const ImGuiWindowFlags DEFAULT_WINDOW_FLAGS =
        ImGuiWindowFlags.NoCollapse      |
        ImGuiWindowFlags.NoSavedSettings |
        ImGuiWindowFlags.NoResize        |
        ImGuiWindowFlags.NoScrollbar     |
        ImGuiWindowFlags.NoScrollWithMouse;

    public static readonly Vector2 ItemSpacingScale          = new(1.1f, 1.05f);
    public static readonly Vector2 SettingsFramePaddingScale = new(1.1f, 1.15f);
    public static readonly Vector2 WindowPaddingScale        = new(1.15f, 1.1f);

    public static WindowStyleScope PushWindowStyle() =>
        new(ImGui.GetStyle());

    public static bool DrawActionButton(string text, Vector2 size, ButtonVariant variant)
    {
        var (buttonColor, hoveredColor, activeColor) = variant switch
        {
            ButtonVariant.Primary => (KnownColor.Orange, KnownColor.Gold, KnownColor.DarkOrange),
            _                     => (KnownColor.Gray, KnownColor.LightGray, KnownColor.SteelBlue)
        };
        var buttonAlpha  = variant == ButtonVariant.Primary ? 1f : 0.72f;
        var hoveredAlpha = variant == ButtonVariant.Primary ? 1f : 0.84f;
        var activeAlpha  = variant == ButtonVariant.Primary ? 1f : 0.80f;

        using var button        = ImRaii.PushColor(ImGuiCol.Button,        WithAlpha(buttonColor,           buttonAlpha));
        using var buttonHovered = ImRaii.PushColor(ImGuiCol.ButtonHovered, WithAlpha(hoveredColor,          hoveredAlpha));
        using var buttonActive  = ImRaii.PushColor(ImGuiCol.ButtonActive,  WithAlpha(activeColor,           activeAlpha));
        using var textColor     = ImRaii.PushColor(ImGuiCol.Text,          WithAlpha(KnownColor.WhiteSmoke, 1f));
        using var rounding      = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, GetCardRounding());

        return ImGui.Button(text, size);
    }

    public static bool DrawSelectableCard(string id, string title, string state, bool selected, Vector4 stateColor, float height)
    {
        var style      = ImGui.GetStyle();
        var size       = new Vector2(ImGui.GetContentRegionAvail().X, height);
        var rounding   = GetCardRounding();
        var borderSize = GetCardBorderSize(style);
        var cursor     = ImGui.GetCursorScreenPos();
        var clicked    = ImGui.InvisibleButton(id, size);
        var hovered    = ImGui.IsItemHovered();
        var held       = ImGui.IsItemActive();
        var drawList   = ImGui.GetWindowDrawList();
        var max        = cursor + size;
        var padding    = style.FramePadding * new Vector2(1.35f, 1.2f);
        var titleSize  = ImGui.CalcTextSize(title);
        var stateSize  = ImGui.CalcTextSize(state);
        var titlePos   = new Vector2(cursor.X + padding.X, cursor.Y + (size.Y - titleSize.Y) * 0.5f);
        var stateRight = max.X - padding.X;
        var dotRadius  = style.ItemSpacing.Y;
        var dotX       = stateRight - stateSize.X - style.ItemInnerSpacing.X - dotRadius * 2f;
        var dotCenter  = new Vector2(dotX        + dotRadius, cursor.Y + size.Y                                                      * 0.5f);
        var statePos   = new Vector2(dotCenter.X + dotRadius           + style.ItemInnerSpacing.X, cursor.Y + (size.Y - stateSize.Y) * 0.5f);
        var backgroundColor = selected
                                  ? WithAlpha(KnownColor.SteelBlue, 0.78f)
                                  : hovered
                                      ? WithAlpha(KnownColor.LightSteelBlue, 0.82f)
                                      : GetWindowBackgroundColor();
        var borderColor = selected
                              ? WithAlpha(KnownColor.DeepSkyBlue, 1f)
                              : hovered
                                  ? WithAlpha(KnownColor.SteelBlue,      0.92f)
                                  : WithAlpha(KnownColor.LightSlateGray, 0.55f);

        drawList.AddRectFilled(cursor, max, ImGui.GetColorU32(backgroundColor), rounding);
        drawList.AddRect(cursor, max, ImGui.GetColorU32(borderColor), rounding, 0, borderSize);
        drawList.AddText(titlePos, GetColorU32(KnownColor.WhiteSmoke, held ? 0.88f : 1f), title);
        drawList.AddCircleFilled(dotCenter, dotRadius, ImGui.GetColorU32(stateColor));
        drawList.AddText(statePos, GetColorU32(KnownColor.Gainsboro, 0.92f), state);

        return clicked;
    }

    public static float GetCardBorderSize(ImGuiStylePtr style) =>
        MathF.Max(style.FrameBorderSize, style.WindowBorderSize + style.ChildBorderSize);

    public static float GetCardRounding()
    {
        var style = ImGui.GetStyle();
        return MathF.Max(style.FrameRounding, style.GrabRounding) * 2f;
    }

    public static uint GetColorU32(KnownColor knownColor, float alpha) =>
        ImGui.GetColorU32(WithAlpha(knownColor, alpha));

    public static SelectorStatus GetAreaStatus(int state) =>
        state switch
        {
            0 => new("通畅", WithAlpha(KnownColor.MediumSeaGreen, 1f)),
            1 => new("热门", WithAlpha(KnownColor.DarkOrange,     1f)),
            2 => new("火爆", WithAlpha(KnownColor.IndianRed,      1f)),
            _ => new("繁忙", WithAlpha(KnownColor.IndianRed,      1f))
        };

    public static SelectorStatus GetQueueStatus(int? queueTime) =>
        queueTime switch
        {
            0             => new("通畅", WithAlpha(KnownColor.MediumSeaGreen,           1f)),
            < 0           => new("火爆", WithAlpha(KnownColor.IndianRed,                1f)),
            > 0 and <= 15 => new($"{queueTime} 分钟", WithAlpha(KnownColor.DeepSkyBlue, 1f)),
            > 15          => new($"{queueTime} 分钟", WithAlpha(KnownColor.Goldenrod,   1f)),
            _             => new("读取中", WithAlpha(KnownColor.LightSlateGray,          1f))
        };

    public static Vector4 GetWindowBackgroundColor() =>
        ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(ImGuiCol.WindowBg));

    public static Vector4 WithAlpha(KnownColor knownColor, float alpha)
    {
        var color = Color.FromKnownColor(knownColor);
        return new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, alpha);
    }
}
