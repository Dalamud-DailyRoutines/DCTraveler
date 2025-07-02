using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using DCTravelerX.Helpers;
using DCTravelerX.Infos;
using ImGuiNET;

namespace DCTravelerX.Windows;

internal class WorldSelectorWindows() : Window("超域旅行", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize), IDisposable
{
    private TaskCompletionSource<SelectWorldResult>? selectWorldTaskCompletionSource;
    
    private          bool           showSourceWorld = true;
    private          bool           showTargetWorld = true;
    private          bool           isBack;
    private          int            currentDCIndex;
    private          int            currentWorldIndex;
    private          string[]       dc    = [];
    private readonly List<string[]> world = [];
    private          int            targetDCIndex;
    private          int            targetWorldIndex;
    private          List<Area>     areas = [];

    public override void PreDraw()
    {
        var viewport = ImGui.GetMainViewport();
        var center   = viewport.GetCenter();
        
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new(4f));
        base.PreDraw();
    }

    public override void Draw()
    {
        var windowBackground = ImGui.GetColorU32(ImGuiCol.WindowBg);
        var columnWidth      = ImGui.CalcTextSize("一二三四五六七八九十").X * 2;
        
        if (showSourceWorld)
        {
            using (var table = ImRaii.Table("##TableCurrent", 2, ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersInnerV))
            {
                if (table)
                {
                    ImGui.TableSetupColumn("当前大区",  ImGuiTableColumnFlags.WidthFixed, columnWidth);
                    ImGui.TableSetupColumn("当前服务器", ImGuiTableColumnFlags.WidthFixed, columnWidth);
                    
                    ImGui.TableHeadersRow();

                    ImGui.TableNextRow();
                    
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1f);
                    using (ImRaii.PushColor(ImGuiCol.FrameBg, windowBackground))
                        ImGui.ListBox("##CurrentDc", ref currentDCIndex, dc, dc.Length, 8);
                    
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1f);
                    using (ImRaii.PushColor(ImGuiCol.FrameBg, windowBackground))
                        ImGui.ListBox("##CurrentServer", ref currentWorldIndex, world[currentDCIndex], world[targetDCIndex].Length, 8);
                }
            }
        }

        if (showTargetWorld)
        {
            using (var table = ImRaii.Table("##TableCurrent", 2, ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersInnerV))
            {
                if (table)
                {
                    ImGui.TableSetupColumn("目标大区",  ImGuiTableColumnFlags.WidthFixed, columnWidth);
                    ImGui.TableSetupColumn("目标服务器", ImGuiTableColumnFlags.WidthFixed, columnWidth);
                    
                    ImGui.TableHeadersRow();

                    ImGui.TableNextRow();
                    
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1f);
                    using (ImRaii.PushColor(ImGuiCol.FrameBg, windowBackground))
                        ImGui.ListBox("##TargetDC", ref targetDCIndex, dc, dc.Length, 8);
                    
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1f);
                    using (ImRaii.PushColor(ImGuiCol.FrameBg, windowBackground))
                        ImGui.ListBox("##TargetServer", ref targetWorldIndex, world[targetDCIndex], world[targetDCIndex].Length, 8);
                }
            }
        }
        
        var sameDC = currentDCIndex == targetDCIndex;
        using (ImRaii.Disabled(sameDC))
        {
            if (ImGuiOm.ButtonSelectable(isBack ? "返回" : "传送"))
            {
                selectWorldTaskCompletionSource?.SetResult(
                    new SelectWorldResult
                    {
                        Source = areas[currentDCIndex].GroupList[currentWorldIndex],
                        Target = areas[targetDCIndex].GroupList[targetWorldIndex]
                    });
                IsOpen = false;
            }
        }
        
        if (ImGuiOm.ButtonSelectable("取消"))
        {
            selectWorldTaskCompletionSource?.SetResult(null!);
            IsOpen = false;
        }
    }
    
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
                currentDCIndex = i;
            else if (targetDcName == areasData[i].AreaName)
                targetDCIndex = i;
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
