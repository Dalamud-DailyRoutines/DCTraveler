using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using DCTravelerX.Infos;
using Dalamud.Bindings.ImGui;

namespace DCTravelerX.Windows;

internal class MessageBoxWindow : Window, IDisposable
{
    public readonly  string                                 Title;
    public readonly  string                                 Message;
    public readonly  MessageBoxType                         Type;
    public readonly  object?                                Userdata;
    public readonly  Action<MessageBoxWindow, object?>?     Callback;
    private readonly TaskCompletionSource<MessageBoxResult> messageTaskCompletionSource;
    public readonly  WindowSystem                           WindowSystem;
    
    public bool             ShowWebsite;
    public MessageBoxResult Result;

    public MessageBoxWindow(
        WindowSystem                       windowSystem, string title, string message, MessageBoxType type, object? userdata = null,
        Action<MessageBoxWindow, object?>? callback = null) : base(
        title,
        ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.Popup |
        ImGuiWindowFlags.NoCollapse, true)
    {
        Title                       = title;
        Message                     = message;
        Type                        = type;
        Userdata                    = userdata;
        Callback                    = callback;
        WindowSystem                = windowSystem;
        ShowCloseButton             = false;
        AllowPinning                = false;
        AllowClickthrough           = false;
        messageTaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    
    public static Task<MessageBoxResult> Show(
        WindowSystem WindowSystem, string title, string message, MessageBoxType type = MessageBoxType.Ok, bool showWebsite = false)
    {
        var              guid = Guid.NewGuid();
        MessageBoxWindow box  = new(WindowSystem, $"{title}##{guid}", message, type);
        box.ShowWebsite = showWebsite;
        box.IsOpen      = true;
        WindowSystem.AddWindow(box);
        return box.messageTaskCompletionSource.Task;
    }

    public static Task<MessageBoxResult> Show(
        WindowSystem   WindowSystem, string title, string message, object? userdata, Action<MessageBoxWindow, object?> callback,
        MessageBoxType type = MessageBoxType.Ok)
    {
        var              guid = Guid.NewGuid();
        MessageBoxWindow box  = new(WindowSystem, $"{title}##{guid}", message, type, userdata, callback);
        box.IsOpen = true;
        box.IsOpen = true;
        WindowSystem.AddWindow(box);
        return box.messageTaskCompletionSource.Task;
    }

    public override void PreDraw()
    {
        ImGui.OpenPopup(Title);
        var center = ImGui.GetIO().DisplaySize * 0.5f;
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new(0.5f));
    }

    private static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    public override void Draw()
    {
        ImGui.Text(Message);

        ImGui.Separator();

        switch (Type)
        {
            case MessageBoxType.Ok:
                if (ImGui.Button("确定"))
                {
                    IsOpen = false;
                    Result = MessageBoxResult.Ok;
                }

                break;

            case MessageBoxType.OkCancel:
                if (ImGui.Button("确定"))
                {
                    IsOpen = false;
                    Result = MessageBoxResult.Ok;
                }

                ImGui.SameLine();
                if (ImGui.Button("取消"))
                {
                    IsOpen = false;
                    Result = MessageBoxResult.Cancel;
                }

                break;

            case MessageBoxType.YesCancel:
                if (ImGui.Button("确定"))
                {
                    IsOpen = false;
                    Result = MessageBoxResult.Yes;
                }

                ImGui.SameLine();
                if (ImGui.Button("取消"))
                {
                    IsOpen = false;
                    Result = MessageBoxResult.Cancel;
                }

                break;

            case MessageBoxType.YesNo:
                if (ImGui.Button("确认"))
                {
                    IsOpen = false;
                    Result = MessageBoxResult.Yes;
                }

                ImGui.SameLine();
                if (ImGui.Button("取消"))
                {
                    IsOpen = false;
                    Result = MessageBoxResult.No;
                }

                break;

            case MessageBoxType.YesNoCancel:
                if (ImGui.Button("确认"))
                {
                    IsOpen = false;
                    Result = MessageBoxResult.Yes;
                }

                ImGui.SameLine();
                if (ImGui.Button("拒绝"))
                {
                    IsOpen = false;
                    Result = MessageBoxResult.No;
                }

                ImGui.SameLine();
                if (ImGui.Button("取消"))
                {
                    IsOpen = false;
                    Result = MessageBoxResult.Cancel;
                }

                break;
        }

        if (ShowWebsite)
        {
            ImGui.Text("超域旅行失败, 请查看上方报错提供的指引, 若无有效信息, 请去官网处理");
            
            if (ImGui.Button("打开 [超域旅行]")) 
                OpenUrl("https://ff14bjz.sdo.com/RegionKanTelepo?");
            
            ImGui.SameLine();
            if (ImGui.Button("打开 [订单列表]")) 
                OpenUrl("https://ff14bjz.sdo.com/orderList");
        }

        if (!IsOpen)
        {
            Callback?.Invoke(this, Userdata);
            messageTaskCompletionSource.SetResult(Result);
            Close();
        }
    }

    private void Close() => WindowSystem.RemoveWindow(this);

    public void Dispose() => Close();
}
