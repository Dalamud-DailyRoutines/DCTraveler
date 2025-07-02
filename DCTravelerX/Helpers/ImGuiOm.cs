using System;
using System.Numerics;
using ImGuiNET;

namespace DCTravelerX.Helpers;

public static class ImGuiOm
{
    public static bool ButtonSelectable(string text)
    {
        var style    = ImGui.GetStyle();
        var padding  = style.FramePadding;
        var colors   = style.Colors;
        var textSize = ImGui.CalcTextSize(text);

        var size = new Vector2(Math.Max(ImGui.GetContentRegionAvail().X, textSize.X + (2 * padding.X)),
                               textSize.Y + (2 * padding.Y));

        ImGui.PushStyleColor(ImGuiCol.ButtonActive,  colors[(int)ImGuiCol.HeaderActive]);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, colors[(int)ImGuiCol.HeaderHovered]);
        ImGui.PushStyleColor(ImGuiCol.Button,        0);
        var result = ImGui.Button(text, size);
        ImGui.PopStyleColor(3);

        return result;
    }
}
