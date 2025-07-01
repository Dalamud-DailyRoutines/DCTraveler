using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using DCTraveler.Infos;

namespace DCTraveler.Windows;

internal class WaitingWindow()
    : Window("WaitingOrder",
             ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize |
             ImGuiWindowFlags.NoSavedSettings), IDisposable
{
    private static readonly Dictionary<MigrationStatus, string> statusText = new()
    {
        { MigrationStatus.Failed, "传送失败" },
        { MigrationStatus.InPrepare, "检查角色中..." },
        { MigrationStatus.InQueue, "排队中..." },
        { MigrationStatus.Completed, "传送完成" },
        { MigrationStatus.UnkownCompleted, "传送完成" },
    };
    
    private DateTime        startTime = DateTime.Now;
    public  MigrationStatus Status   = MigrationStatus.InPrepare;
    
    public void Open()
    {
        this.IsOpen    = true;
        this.Status    = MigrationStatus.InPrepare;
        this.startTime = DateTime.Now;
    }

    public override void PreDraw()
    {
        var viewport = ImGui.GetMainViewport();
        var center   = viewport.GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        base.PreDraw();
    }
    public override void Draw()
    {
        ImGui.Text("正在超域旅行中....");
        
        ImGui.Text($"已等待时间:{DateTime.Now - startTime}");
        
        ImGui.Text("目前状态:");
        
        ImGui.SameLine();
        ImGui.Text(statusText[this.Status]);
    }
    
    public void Dispose()
    {
    }
}
