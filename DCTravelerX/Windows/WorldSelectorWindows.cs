using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using DCTravelerX.Infos;
using ImGuiNET;

namespace DCTravelerX.Windows;

internal class WorldSelectorWindows() : Window("超域旅行", ImGuiWindowFlags.NoCollapse), IDisposable
{
    private          bool           showSourceWorld = true;
    private          bool           showTargetWorld = true;
    private          bool           isBack;
    private          int            currentDcIndex;
    private          int            currentWorldIndex;
    private          string[]       dc    = [];
    private readonly List<string[]> world = [];
    private          int            targetDcIndex;
    private          int            targetWorldIndex;
    private          List<Area>     areas = [];

    public override void PreDraw()
    {
        var viewport = ImGui.GetMainViewport();
        var center   = viewport.GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        base.PreDraw();
    }

    public override void Draw()
    {
        if (showSourceWorld)
        {
            using (var table = ImRaii.Table("##TableCurrent", 2))
            {
                if (table)
                {
                    ImGui.TableSetupColumn("当前大区",  ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("当前服务器", ImGuiTableColumnFlags.WidthStretch);
                    
                    ImGui.TableHeadersRow();

                    ImGui.TableNextRow();
                    
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1f);
                    ImGui.ListBox("##CurrentDc", ref currentDcIndex, dc, dc.Length, 4);
                    
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1f);
                    ImGui.ListBox("##CurrentServer", ref currentWorldIndex, world[currentDcIndex], world[targetDcIndex].Length, 7);
                }
            }
        }

        if (showTargetWorld)
        {
            using (var table = ImRaii.Table("##TableCurrent", 2))
            {
                if (table)
                {
                    ImGui.TableSetupColumn("目标大区",  ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("目标服务器", ImGuiTableColumnFlags.WidthStretch);
                    
                    ImGui.TableHeadersRow();

                    ImGui.TableNextRow();
                    
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1f);
                    ImGui.ListBox("##TargetDC", ref targetDcIndex, dc, dc.Length, 4);
                    
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1f);
                    ImGui.ListBox("##TargetServer", ref targetWorldIndex, world[targetDcIndex], world[targetDcIndex].Length, 7);
                }
            }
        }

        var sameDc = currentDcIndex == targetDcIndex;
        using (ImRaii.Disabled(sameDc))
        {
            if (ImGui.Button(isBack ? "返回" : "传送"))
            {
                selectWorldTaskCompletionSource?.SetResult(
                    new SelectWorldResult
                    {
                        Source = areas[currentDcIndex].GroupList[currentWorldIndex],
                        Target = areas[targetDcIndex].GroupList[targetWorldIndex]
                    });
                IsOpen = false;
            }
        }
        
        ImGui.SameLine();
        if (ImGui.Button("取消"))
        {
            selectWorldTaskCompletionSource?.SetResult(null!);
            IsOpen = false;
        }
    }

    private TaskCompletionSource<SelectWorldResult>? selectWorldTaskCompletionSource;

    public Task<SelectWorldResult> OpenTravelWindow(
        bool    showSource,     bool    showTarget, bool isBackHome, List<Area> areasData, string? currentDcName = null, string? currentWorldCode = null,
        string? targetDcName = null, string? targetWorldCode = null)
    {
        selectWorldTaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        
        areas           = areasData;
        showSourceWorld = showSource;
        showTargetWorld = showTarget;
        isBack          = isBackHome;
        dc              = new string[areasData.Count];
        
        for (var i = 0; i < areasData.Count; i++)
        {
            dc[i] = areasData[i].AreaName;
            world.Add(new string[areasData[i].GroupList.Count]);
            if (currentDcName == areasData[i].AreaName)
                currentDcIndex = i;
            else if (targetDcName == areasData[i].AreaName)
                targetDcIndex = i;
            for (var j = 0; j < areasData[i].GroupList.Count; j++)
            {
                if (currentDcName == areasData[i].AreaName && areasData[i].GroupList[j].GroupCode == currentWorldCode)
                    currentWorldIndex = j;
                else if (targetDcName == areasData[i].AreaName && areasData[i].GroupList[j].GroupCode == targetWorldCode)
                    targetWorldIndex = j;
                world[i][j] = areasData[i].GroupList[j].GroupName;
            }
        }

        IsOpen = true;
        return selectWorldTaskCompletionSource.Task;
    }

    public void Dispose() { }
}
