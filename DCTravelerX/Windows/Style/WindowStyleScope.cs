using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace DCTravelerX.Windows.Style;

internal readonly struct WindowStyleScope
(
    ImGuiStylePtr style
) : IDisposable
{
    private readonly ImRaii.Style windowPadding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, style.WindowPadding * WindowStyles.WindowPaddingScale);
    private readonly ImRaii.Style itemSpacing   = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing,   style.ItemSpacing   * WindowStyles.ItemSpacingScale);

    public void Dispose()
    {
        itemSpacing.Dispose();
        windowPadding.Dispose();
    }
}
