using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using DCTravelerX.Helpers;
using DCTravelerX.Infos;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using DCTravelerX.Managers;

namespace DCTravelerX.Windows;

internal class WorldSelectorWindows() : Window("超域旅行", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize), IDisposable
{
    private TaskCompletionSource<SelectWorldResult>? selectWorldTaskCompletionSource;

    private bool       showSourceWorld = true;
    private bool       showTargetWorld = true;
    private bool       isBack;
    private List<Area> areas = [];
    private Area?      selectedSourceArea;
    private Group?     selectedSourceGroup;
    private Area?      selectedTargetArea;
    private Group?     selectedTargetGroup;

    public override void OnClose()
    {
        selectWorldTaskCompletionSource?.TrySetResult(null!);
        base.OnClose();
    }

    public override void PreDraw()
    {
        var viewport = ImGui.GetMainViewport();
        var center   = viewport.GetCenter();

        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new(0.45f));
    }

    public override void Draw()
    {
        var columnWidth = ImGui.CalcTextSize("一二三四五六七八九十").X   * 2;
        var childHeight = ImGui.GetTextLineHeightWithSpacing() * (int)(8 * ImGuiHelpers.GlobalScale);
        
        if (showSourceWorld)
        {
            using var table = ImRaii.Table("##TableCurrent", 2, ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersInnerV);
            if (table)
            {
                ImGui.TableSetupColumn("当前大区",  ImGuiTableColumnFlags.WidthFixed, columnWidth);
                ImGui.TableSetupColumn("当前服务器", ImGuiTableColumnFlags.WidthFixed, columnWidth);
                    
                ImGui.TableHeadersRow();

                ImGui.TableNextRow();
                    
                ImGui.TableNextColumn();
                using (var child = ImRaii.Child("##SourceDcChild", new(-1f, childHeight)))
                {
                    if (child)
                    {
                        foreach (var area in areas)
                        {
                            var selected = selectedSourceArea?.AreaId == area.AreaId;
                            var stateText = area.State switch
                            {
                                0 => "通畅",
                                1 => "热门",
                                2 => "火爆",
                                _ => "火爆"
                            };
                            
                            if (ImGui.Selectable($"{area.AreaName} ({stateText})", selected))
                            {
                                selectedSourceArea  = area;
                                selectedSourceGroup = null;
                            }
                        }
                    }
                }
                    
                ImGui.TableNextColumn();
                using (var child = ImRaii.Child("##SourceServerChild", new(-1f, childHeight)))
                {
                    if (child)
                    {
                        if (selectedSourceArea != null)
                        {
                            foreach (var group in selectedSourceArea.GroupList)
                            {
                                var cached = DCTravelClient.CachedAreas
                                                           .SelectMany(x => x.GroupList)
                                                           .FirstOrDefault(x => x.GroupName == group.GroupName);
                                var queueTime = cached?.QueueTime ?? group.QueueTime ?? -1;
                                var waitTimeMessage = queueTime switch
                                {
                                    0    => "即刻完成",
                                    -1   => "禁止传送",
                                    -999 => "繁忙",
                                    _    => $"{queueTime} 分钟"
                                };
                                var label    = $"{group.GroupName} (状态: {waitTimeMessage})";
                                var selected = selectedSourceGroup?.GroupId == group.GroupId;
                                if (ImGui.Selectable(label, selected))
                                    selectedSourceGroup = group;
                            }
                        }
                    }
                }
            }
        }

        if (showTargetWorld)
        {
            using var table = ImRaii.Table("##TableCurrent", 2, ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersInnerV);
            if (table)
            {
                ImGui.TableSetupColumn("目标大区",  ImGuiTableColumnFlags.WidthFixed, columnWidth);
                ImGui.TableSetupColumn("目标服务器", ImGuiTableColumnFlags.WidthFixed, columnWidth);
                    
                ImGui.TableHeadersRow();

                ImGui.TableNextRow();
                    
                ImGui.TableNextColumn();
                using (var child = ImRaii.Child("##TargetDcChild", new(-1f, childHeight)))
                {
                    if (child)
                    {
                        foreach (var area in areas)
                        {
                            if (selectedSourceArea?.AreaId == area.AreaId) continue;
                            
                            var selected = selectedTargetArea?.AreaId == area.AreaId;
                            var stateText = area.State switch
                            {
                                0 => "通畅",
                                1 => "热门",
                                2 => "火爆",
                                _ => "火爆"
                            };
                            
                            if (ImGui.Selectable($"{area.AreaName} ({stateText})", selected))
                            {
                                selectedTargetArea  = area;
                                selectedTargetGroup = area.GroupList.FirstOrDefault();
                            }
                        }
                    }
                }
                    
                ImGui.TableNextColumn();
                using (var child = ImRaii.Child("##TargetServerChild", new(-1f, childHeight)))
                {
                    if (child)
                    {
                        if (selectedTargetArea != null)
                        {
                            foreach (var group in selectedTargetArea.GroupList)
                            {
                                var queueTime = group.QueueTime ?? -1;
                                var waitTimeMessage = queueTime switch
                                {
                                    0          => "即刻完成",
                                    -999 or -1 => "繁忙",
                                    _          => $"{queueTime} 分钟"
                                };
                                var label    = $"{group.GroupName} (状态: {waitTimeMessage})";
                                var selected = selectedTargetGroup?.GroupId == group.GroupId;
                                if (ImGui.Selectable(label, selected))
                                    selectedTargetGroup = group;
                            }
                        }
                    }
                }
            }
        }

        ImGui.Spacing();

        var enableRetry = Service.Config.EnableAutoRetry;
        if (ImGui.Checkbox("跨区失败时自动重试", ref enableRetry))
        {
            Service.Config.EnableAutoRetry = enableRetry;
            Service.Config.Save();
        }

        if (enableRetry)
        {
            var retryCount = Service.Config.MaxRetryCount;
            
            ImGui.SameLine(0, 10f * ImGuiHelpers.GlobalScale);
            ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
            if (ImGui.InputInt("最大重试次数", ref retryCount))
                retryCount = Math.Max(1, retryCount);

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                Service.Config.MaxRetryCount = retryCount;
                Service.Config.Save();
            }
        }

        ImGui.Spacing();

        var disableAction = selectedSourceGroup == null || selectedTargetGroup == null ||
                            (selectedSourceArea != null && selectedTargetArea != null && selectedSourceArea.AreaId == selectedTargetArea.AreaId);
        
        using (ImRaii.Disabled(disableAction))
        {
            if (ImGui.Button(isBack ? "返回" : "传送", new(-1, ImGui.GetTextLineHeightWithSpacing() * 1.5f)))
            {
                selectWorldTaskCompletionSource?.TrySetResult(
                    new SelectWorldResult
                    {
                        Source = selectedSourceGroup!,
                        Target = selectedTargetGroup!
                    });
                IsOpen = false;
            }
        }

        if (ImGui.Button("取消", new(-1, ImGui.GetTextLineHeightWithSpacing() * 1.5f)))
        {
            selectWorldTaskCompletionSource?.TrySetResult(null!);
            IsOpen = false;
        }
    }

    public Task<SelectWorldResult> OpenTravelWindow(
        bool       showSource,
        bool       showTarget,
        bool       isBackHome,
        List<Area> areasData,
        string?    currentDCName    = null,
        string?    currentWorldCode = null,
        string?    targetDCName     = null,
        string?    targetWorldCode  = null)
    {
        selectWorldTaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        areas           = areasData;
        showSourceWorld = showSource;
        showTargetWorld = showTarget;
        isBack          = isBackHome;
        selectedSourceArea  = null;
        selectedSourceGroup = null;
        selectedTargetArea  = null;
        selectedTargetGroup = null;

        selectedSourceArea = currentDCName != null
                                 ? areasData.FirstOrDefault(a => a.AreaName == currentDCName)
                                 : areasData.FirstOrDefault();
        if (selectedSourceArea != null)
        {
            selectedSourceGroup = currentWorldCode != null
                                      ? selectedSourceArea.GroupList.FirstOrDefault(g => g.GroupCode == currentWorldCode)
                                      : selectedSourceArea.GroupList.FirstOrDefault();
        }

        selectedTargetArea = targetDCName != null
                                 ? areasData.FirstOrDefault(a => a.AreaName == targetDCName)
                                 : areasData.FirstOrDefault();
        if (selectedTargetArea != null)
        {
            selectedTargetGroup = targetWorldCode != null
                                      ? selectedTargetArea.GroupList.FirstOrDefault(g => g.GroupCode == targetWorldCode)
                                      : selectedTargetArea.GroupList.FirstOrDefault();
        }

        IsOpen = true;
        return selectWorldTaskCompletionSource.Task;
    }

    public void Dispose() { }
}
