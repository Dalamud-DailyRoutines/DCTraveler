using System;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility.Numerics;
using DCTravelerX.Helpers;
using DCTravelerX.Infos;
using DCTravelerX.Managers;

namespace DCTravelerX.Windows;

internal class DCGroupSelectorWindow() : Window
                                         (
                                             "选择大区",
                                             ImGuiWindowFlags.NoCollapse       |
                                             ImGuiWindowFlags.AlwaysAutoResize |
                                             ImGuiWindowFlags.NoSavedSettings
                                         ), IDisposable
{
    private TaskCompletionSource<string?> areaNameTaskCompletionSource = null!;

    public void Dispose() { }

    public override void Draw()
    {
        if (ServerDataManager.SdoAreas is not { Length: > 0 })
        {
            ImGui.Text("服务器信息加载失败");
            return;
        }

        if (Service.GameGui.GetAddonByName("_TitleMenu") == 0)
        {
            ImGui.Text("必须在标题画面打开");
            return;
        }

        var columnWidth = ImGui.CalcTextSize("一二三四五六七八九十").X;

        foreach (var dc in DCTravelClient.Areas)
        {
            DrawDcGroup(dc.Value.Area, columnWidth);
            ImGui.SameLine();
        }
    }

    private void DrawDcGroup(Area area, float width)
    {
        var tableStartPos = ImGui.GetCursorScreenPos();

        using (ImRaii.Group())
        using (var table = ImRaii.Table($"{area.AreaName} Content", 1))
        {
            if (table)
            {
                ImGui.TableSetupColumn(area.AreaName);
                ImGui.TableHeadersRow();

                foreach (var t in area.GroupList)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text(t.GroupName);
                }
            }
        }

        var tableSize = ImGui.GetItemRectSize().WithY(0) + new Vector2(width, 9 * ImGui.GetTextLineHeightWithSpacing());

        ImGui.SetCursorScreenPos(tableStartPos);

        using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0)))
        using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.15f, 0.6f, 1f, 0.30f)))
        using (ImRaii.PushColor(ImGuiCol.ButtonActive, new Vector4(0.1f, 0.35f, 0.8f, 0.50f)))
        {
            if (ImGui.Button($"##{area.AreaName} Click", tableSize))
            {
                Task.Run
                (async () =>
                    {
                        try
                        {
                            await GameFunctions.SelectDCAndLogin(area.AreaName, true);
                        }
                        catch (Exception ex)
                        {
                            await MessageBoxWindow.Show(WindowManager.WindowSystem, "选择大区", $"大区切换失败:\n{ex}", showWebsite: false);
                        }
                    }
                );
                IsOpen = false;
            }
        }
    }

    public async Task<string?> Open(SdoArea[] areas)
    {
        IsOpen                       = true;
        areaNameTaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        return await areaNameTaskCompletionSource.Task;
    }
}
