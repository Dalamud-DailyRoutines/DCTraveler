using System;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace DCTravelerX.Windows;

internal class TravelCancelWindow : Window, IDisposable
{
    public bool IsCancelled { get; private set; }

    public TravelCancelWindow() : base(
        "超域旅行等待中",
        ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar)
    {
        IsCancelled = false;
        ShowCloseButton = false;
        AllowPinning = false;
        AllowClickthrough = false;
    }

    public override void PreDraw()
    {
        var center = ImGui.GetIO().DisplaySize * 0.5f;
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new(0.5f));
        base.PreDraw();
    }

    public override void Draw()
    {
        // 增大字体和间距（相当于增大20%）
        ImGui.Dummy(new(0, 10));
        ImGui.TextWrapped("正在等待传送或重试中...");
        ImGui.Dummy(new(0, 5));
        ImGui.TextWrapped("如需取消传送，请点击下方按钮");
        ImGui.Dummy(new(0, 10));

        ImGui.Separator();
        ImGui.Dummy(new(0, 10));

        // 按钮尺寸增大20%：150*1.2=180, 30*1.2=36
        if (ImGui.Button("取消传送", new(180, 36)))
        {
            IsCancelled = true;
            IsOpen = false;
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("取消当前的传送操作");

        ImGui.Dummy(new(0, 10));
    }

    public void Reset()
    {
        IsCancelled = false;
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
