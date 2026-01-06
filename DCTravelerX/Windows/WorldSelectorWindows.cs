using System;
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

    private bool showSourceWorld = true;
    private bool showTargetWorld = true;

    private bool isBack;
    
    private uint   selectedSourceAreaID;
    private string selectedSourceGroupName = string.Empty;
    private uint   selectedTargetAreaID;
    private string selectedTargetGroupName = string.Empty;

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
                        foreach (var (_, (area, _)) in DCTravelClient.Areas)
                        {
                            var selected = selectedSourceAreaID == area.AreaId;
                            var stateText = area.State switch
                            {
                                0 => "通畅",
                                1 => "热门",
                                2 => "火爆",
                                _ => "火爆"
                            };
                            
                            if (ImGui.Selectable($"{area.AreaName} ({stateText})", selected))
                            {
                                selectedSourceAreaID    = (uint)area.AreaId;
                                selectedSourceGroupName = string.Empty;
                            }
                        }
                    }
                }
                    
                ImGui.TableNextColumn();
                using (var child = ImRaii.Child("##SourceServerChild", new(-1f, childHeight)))
                {
                    if (child)
                    {
                        if (selectedSourceAreaID != 0)
                        {
                            foreach (var (groupID, group) in DCTravelClient.Areas[selectedSourceAreaID].Groups)
                            {
                                var queueTime  = group.QueueTime ?? -1;
                                var waitTimeMessage = queueTime switch
                                {
                                    0    => "即刻完成",
                                    -1   => "禁止传送",
                                    -999 => "繁忙",
                                    _    => $"{queueTime} 分钟"
                                };
                                var label    = $"{group.GroupName} ({waitTimeMessage})";
                                var selected = selectedSourceGroupName == group.GroupName;
                                if (ImGui.Selectable(label, selected))
                                    selectedSourceGroupName = group.GroupName;
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
                        foreach (var (_, (area, _)) in DCTravelClient.Areas)
                        {
                            if (selectedSourceAreaID == area.AreaId) continue;
                            
                            var selected = selectedTargetAreaID == area.AreaId;
                            var stateText = area.State switch
                            {
                                0 => "通畅",
                                1 => "热门",
                                2 => "火爆",
                                _ => "火爆"
                            };
                            
                            if (ImGui.Selectable($"{area.AreaName} ({stateText})", selected))
                            {
                                selectedTargetAreaID    = (uint)area.AreaId;
                                selectedTargetGroupName = area.GroupList.FirstOrDefault().GroupName;
                            }
                        }
                    }
                }
                    
                ImGui.TableNextColumn();
                using (var child = ImRaii.Child("##TargetServerChild", new(-1f, childHeight)))
                {
                    if (child)
                    {
                        if (selectedTargetAreaID != 0)
                        {
                            foreach (var (_, group) in DCTravelClient.Areas[selectedTargetAreaID].Groups)
                            {
                                var queueTime = group.QueueTime ?? -1;
                                var waitTimeMessage = queueTime switch
                                {
                                    0          => "即刻完成",
                                    -999 or -1 => "繁忙",
                                    _          => $"{queueTime} 分钟"
                                };
                                var label    = $"{group.GroupName} ({waitTimeMessage})";
                                var selected = selectedTargetGroupName == group.GroupName;
                                if (ImGui.Selectable(label, selected))
                                    selectedTargetGroupName = group.GroupName;
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

        var disableAction = string.IsNullOrEmpty(selectedSourceGroupName) || 
                            string.IsNullOrEmpty(selectedTargetGroupName) ||
                            selectedSourceAreaID == selectedTargetAreaID;
        
        using (ImRaii.Disabled(disableAction))
        {
            if (ImGui.Button(isBack ? "返回" : "传送", new(-1, ImGui.GetTextLineHeightWithSpacing() * 1.5f)))
            {
                
                
                selectWorldTaskCompletionSource?.TrySetResult(
                    new SelectWorldResult
                    {
                        Source = selectedSourceGroupName,
                        Target = selectedTargetGroupName
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
        bool    showSource,
        bool    showTarget,
        bool    isBackHome,
        string? currentDCName    = null,
        string? currentWorldCode = null,
        string? targetDCName     = null,
        string? targetWorldCode  = null)
    {
        selectWorldTaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        showSourceWorld = showSource;
        showTargetWorld = showTarget;
        isBack          = isBackHome;

        selectedSourceAreaID    = 0;
        selectedTargetAreaID    = 0;
        selectedSourceGroupName = string.Empty;
        selectedTargetGroupName = string.Empty;

        selectedSourceAreaID = currentDCName != null
                                   ? DCTravelClient.Areas.FirstOrDefault(a => a.Value.Area.AreaName == currentDCName).Key
                                   : DCTravelClient.Areas.FirstOrDefault().Key;
        if (selectedSourceAreaID != 0)
        {
            selectedSourceGroupName = currentWorldCode != null
                                          ? DCTravelClient.Areas[selectedSourceAreaID].Area.GroupList.FirstOrDefault(g => g.GroupCode == currentWorldCode).GroupName
                                          : DCTravelClient.Areas[selectedSourceAreaID].Area.GroupList.FirstOrDefault().GroupName;
        }

        selectedTargetAreaID = targetDCName != null
                                   ? DCTravelClient.Areas.FirstOrDefault(a => a.Value.Area.AreaName == targetDCName).Key
                                   : DCTravelClient.Areas.FirstOrDefault().Key;
        if (selectedTargetAreaID != 0)
        {
            selectedTargetGroupName = targetWorldCode != null
                                          ? DCTravelClient.Areas[selectedTargetAreaID].Area.GroupList.FirstOrDefault(g => g.GroupCode == targetWorldCode).GroupName
                                          : DCTravelClient.Areas[selectedTargetAreaID].Area.GroupList.FirstOrDefault().GroupName;
        }

        IsOpen = true;
        return selectWorldTaskCompletionSource.Task;
    }

    public void Dispose() { }
}
