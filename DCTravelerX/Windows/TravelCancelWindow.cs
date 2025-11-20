using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using DCTravelerX.Managers;

namespace DCTravelerX.Windows;

public class TravelCancelWindow : Window, IDisposable
{
    private string statusMessage = "正在等待传送或重试中...";
    private string detailedMessage = "";
    private bool isCancelled;

    public bool IsCancelled => isCancelled;

    public TravelCancelWindow() : base("跨区传送状态", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize)
    {
        Size = new Vector2(400, 200);
        SizeCondition = ImGuiCond.Always;
    }

    public void Reset()
    {
        isCancelled = false;
        statusMessage = "正在等待传送或重试中...";
        detailedMessage = "";
    }

    public void UpdateStatus(string message)
    {
        var lines = message.Split('\n');
        if (lines.Length >= 2)
        {
            statusMessage = lines[0];
            if (lines.Length >= 3)
            {
                detailedMessage = lines[2];
            }
            else
            {
                detailedMessage = "";
            }
        }
        else
        {
            statusMessage = message;
            detailedMessage = "";
        }
    }

    public override void Draw()
    {
        ImGui.TextWrapped(statusMessage);

        if (!string.IsNullOrEmpty(detailedMessage))
        {
            ImGui.Spacing();
            ImGui.TextWrapped(detailedMessage);
        }

        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.6f, 0.0f, 1.0f));
        ImGui.TextWrapped("注意:订单一旦提交成功,跨区服务将立即开始且无法撤回。");
        ImGui.TextWrapped("取消仅关闭等待界面,不会停止已提交的跨区订单。");
        ImGui.PopStyleColor();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var buttonWidth = ImGui.GetContentRegionAvail().X;
        if (ImGui.Button("取消传送", new Vector2(buttonWidth, 30)))
        {
            isCancelled = true;
            IsOpen = false;
        }
    }

    public void Dispose() { }
}
