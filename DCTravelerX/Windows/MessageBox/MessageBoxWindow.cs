using System;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using DCTravelerX.Windows.Style;

namespace DCTravelerX.Windows.MessageBox;

internal class MessageBoxWindow : Window, IDisposable
{
    private readonly record struct MessageAction
    (
        string           Label,
        MessageBoxResult Result,
        ButtonVariant    Variant
    );

    private const float MINIMUM_PANEL_WIDTH = 360f;
    private const float MAXIMUM_PANEL_WIDTH = 640f;

    public readonly  Action<MessageBoxWindow, object?>?     Callback;
    public readonly  string                                 Message;
    private readonly TaskCompletionSource<MessageBoxResult> messageTaskCompletionSource;
    public readonly  string                                 Title;
    public readonly  MessageBoxType                         Type;
    public readonly  object?                                Userdata;
    public readonly  WindowSystem                           WindowSystem;
    public           MessageBoxResult                       Result;

    public bool ShowWebsite;

    public MessageBoxWindow
    (
        WindowSystem                       windowSystem,
        string                             title,
        string                             message,
        MessageBoxType                     type,
        object?                            userdata = null,
        Action<MessageBoxWindow, object?>? callback = null
    ) : base
    (
        title,
        WindowStyles.DEFAULT_WINDOW_FLAGS |
        ImGuiWindowFlags.AlwaysAutoResize |
        ImGuiWindowFlags.Popup,
        true
    )
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

    public void Dispose() => Close();

    public static Task<MessageBoxResult> Show
    (
        WindowSystem   windowSystem,
        string         title,
        string         message,
        MessageBoxType type        = MessageBoxType.Ok,
        bool           showWebsite = false
    )
    {
        var              guid = Guid.NewGuid();
        MessageBoxWindow box  = new(windowSystem, $"{title}##{guid}", message, type);
        box.ShowWebsite = showWebsite;
        box.IsOpen      = true;
        windowSystem.AddWindow(box);
        return box.messageTaskCompletionSource.Task;
    }

    public static Task<MessageBoxResult> Show
    (
        WindowSystem                      windowSystem,
        string                            title,
        string                            message,
        object?                           userdata,
        Action<MessageBoxWindow, object?> callback,
        MessageBoxType                    type = MessageBoxType.Ok
    )
    {
        var              guid = Guid.NewGuid();
        MessageBoxWindow box  = new(windowSystem, $"{title}##{guid}", message, type, userdata, callback);
        box.IsOpen = true;
        box.IsOpen = true;
        windowSystem.AddWindow(box);
        return box.messageTaskCompletionSource.Task;
    }

    public override void PreDraw()
    {
        ImGui.OpenPopup(Title);
        var displaySize = ImGui.GetIO().DisplaySize;
        var center      = displaySize         * 0.5f;
        var minWidth    = MINIMUM_PANEL_WIDTH * ImGuiHelpers.GlobalScale;
        var maxWidth    = MathF.Min(MAXIMUM_PANEL_WIDTH * ImGuiHelpers.GlobalScale, displaySize.X * 0.82f);

        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new(0.5f));
        ImGui.SetNextWindowSizeConstraints(new Vector2(minWidth, 0f), new Vector2(maxWidth, displaySize.Y * 0.8f));
    }

    private static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    public override void Draw()
    {
        DrawMessagePanel();

        if (ShowWebsite)
        {
            ImGui.Spacing();
            DrawWebsiteActions();
        }

        ImGui.Dummy(new(ImGui.GetTextLineHeight() * 0.3f));
        DrawMessageActions();

        if (!IsOpen)
        {
            Callback?.Invoke(this, Userdata);
            messageTaskCompletionSource.SetResult(Result);
            Close();
        }
    }

    private void Close() => WindowSystem.RemoveWindow(this);

    private void DrawMessagePanel()
    {
        var style          = ImGui.GetStyle();
        var panelWidth     = GetPanelWidth();
        var panelPadding   = style.FramePadding * new Vector2(1.45f, 1.35f);
        var headerStatus   = GetHeaderStatus();
        var headerText     = GetHeaderText();
        var headerTextSize = ImGui.CalcTextSize(headerText);
        var dotRadius      = style.ItemSpacing.Y * 0.92f;
        var textWrapWidth  = MathF.Max(0f, panelWidth - panelPadding.X * 2f);
        var messageSize    = ImGui.CalcTextSize(Message, false, textWrapWidth);
        var headerHeight   = MathF.Max(headerTextSize.Y, dotRadius * 2f);
        var dividerY       = panelPadding.Y + headerHeight + style.ItemSpacing.Y * 1.15f;
        var bodyTop        = dividerY       + style.ItemSpacing.Y                * 1.05f;
        var panelHeight    = bodyTop        + messageSize.Y + panelPadding.Y;
        var cursor         = ImGui.GetCursorScreenPos();
        var max            = cursor + new Vector2(panelWidth, panelHeight);
        var rounding       = WindowStyles.GetCardRounding();
        var borderSize     = WindowStyles.GetCardBorderSize(style);
        var drawList       = ImGui.GetWindowDrawList();
        var dividerInset   = panelPadding.X + style.FramePadding.X          * 0.9f;
        var headerCenterY  = cursor.Y       + panelPadding.Y + headerHeight * 0.5f;
        var dotCenter      = new Vector2(cursor.X    + 2 * panelPadding.X + dotRadius,                headerCenterY);
        var headerPos      = new Vector2(dotCenter.X + dotRadius          + style.ItemInnerSpacing.X, headerCenterY - headerTextSize.Y * 0.5f);
        var bodyPos        = new Vector2(cursor.X    + panelPadding.X,                                cursor.Y      + bodyTop);

        ImGui.InvisibleButton("##MessagePanel", new Vector2(panelWidth, panelHeight));

        drawList.AddRectFilled(cursor, max, WindowStyles.GetColorU32(KnownColor.LightSlateGray, 0.16f), rounding);
        drawList.AddRect(cursor, max, WindowStyles.GetColorU32(KnownColor.SteelBlue,            0.42f), rounding, 0, borderSize);
        drawList.AddCircleFilled(dotCenter, dotRadius, ImGui.GetColorU32(headerStatus.Color));
        drawList.AddText(headerPos, WindowStyles.GetColorU32(KnownColor.WhiteSmoke, 1f), headerText);
        drawList.AddLine
        (
            new Vector2(cursor.X + dividerInset, cursor.Y + dividerY),
            new Vector2(max.X    - dividerInset, cursor.Y + dividerY),
            WindowStyles.GetColorU32(KnownColor.SlateGray, 0.55f),
            MathF.Max(1f, borderSize)
        );

        ImGui.SetCursorScreenPos(bodyPos);
        using var wrapPos   = ImRaii.TextWrapPos(ImGui.GetCursorPosX() + textWrapWidth);
        using var textColor = ImRaii.PushColor(ImGuiCol.Text, WindowStyles.WithAlpha(KnownColor.Gainsboro, 0.96f));
        ImGui.TextUnformatted(Message);
        ImGui.SetCursorScreenPos(new Vector2(cursor.X, max.Y));
    }

    private static void DrawWebsiteActions()
    {
        ImGui.Spacing();

        var style        = ImGui.GetStyle();
        var buttonHeight = GetButtonHeight();
        var spacing      = style.ItemSpacing.X;
        var buttonWidth  = MathF.Max(0f, (ImGui.GetContentRegionAvail().X - spacing) * 0.5f);

        if (WindowStyles.DrawActionButton("打开 [超域旅行]", new Vector2(buttonWidth, buttonHeight), ButtonVariant.Ghost))
            OpenUrl("https://ff14bjz.sdo.com/RegionKanTelepo?");

        ImGui.SameLine(0f, spacing);

        if (WindowStyles.DrawActionButton("打开 [订单列表]", new Vector2(buttonWidth, buttonHeight), ButtonVariant.Ghost))
            OpenUrl("https://ff14bjz.sdo.com/orderList");
    }

    private void DrawMessageActions()
    {
        var actions = GetActions();
        var style   = ImGui.GetStyle();

        if (actions.Length == 0)
            return;

        var buttonHeight = GetButtonHeight();
        var spacing      = style.ItemSpacing.X;
        var buttonWidth  = MathF.Max(0f, (ImGui.GetContentRegionAvail().X - spacing * (actions.Length - 1)) / actions.Length);

        for (var i = 0; i < actions.Length; i++)
        {
            if (i > 0)
                ImGui.SameLine(0f, spacing);

            var action = actions[i];
            if (WindowStyles.DrawActionButton(action.Label, new Vector2(buttonWidth, buttonHeight), action.Variant))
                CloseWithResult(action.Result);
        }
    }

    private void CloseWithResult(MessageBoxResult result)
    {
        Result = result;
        IsOpen = false;
    }

    private MessageAction[] GetActions() =>
        Type switch
        {
            MessageBoxType.Ok        => [new("确定", MessageBoxResult.Ok, ButtonVariant.Primary)],
            MessageBoxType.OkCancel  => [new("取消", MessageBoxResult.Cancel, ButtonVariant.Ghost), new("确定", MessageBoxResult.Ok, ButtonVariant.Primary)],
            MessageBoxType.YesCancel => [new("取消", MessageBoxResult.Cancel, ButtonVariant.Ghost), new("确定", MessageBoxResult.Yes, ButtonVariant.Primary)],
            MessageBoxType.YesNo     => [new("取消", MessageBoxResult.No, ButtonVariant.Ghost), new("确认", MessageBoxResult.Yes, ButtonVariant.Primary)],
            MessageBoxType.YesNoCancel =>
            [
                new("取消", MessageBoxResult.Cancel, ButtonVariant.Ghost), new("拒绝", MessageBoxResult.No, ButtonVariant.Ghost),
                new("确认", MessageBoxResult.Yes, ButtonVariant.Primary)
            ],
            _ => []
        };

    private SelectorStatus GetHeaderStatus() =>
        Type switch
        {
            MessageBoxType.Ok          => new("提示", WindowStyles.WithAlpha(KnownColor.DeepSkyBlue, 1f)),
            MessageBoxType.OkCancel    => new("请确认", WindowStyles.WithAlpha(KnownColor.Goldenrod,  1f)),
            MessageBoxType.YesCancel   => new("请确认", WindowStyles.WithAlpha(KnownColor.Goldenrod,  1f)),
            MessageBoxType.YesNo       => new("请确认", WindowStyles.WithAlpha(KnownColor.Goldenrod,  1f)),
            MessageBoxType.YesNoCancel => new("请确认", WindowStyles.WithAlpha(KnownColor.IndianRed,  1f)),
            _                          => new("提示", WindowStyles.WithAlpha(KnownColor.DeepSkyBlue, 1f))
        };

    private string GetHeaderText() =>
        ShowWebsite ? "错误信息" : GetHeaderStatus().Text;

    private static float GetButtonHeight() =>
        ImGui.GetFrameHeightWithSpacing() * 1.4f;

    private static float GetPanelWidth()
    {
        var scaledMinWidth = MINIMUM_PANEL_WIDTH * ImGuiHelpers.GlobalScale;
        var scaledMaxWidth = MAXIMUM_PANEL_WIDTH * ImGuiHelpers.GlobalScale;
        var availableWidth = ImGui.GetContentRegionAvail().X;
        return Math.Clamp(MathF.Max(availableWidth, scaledMinWidth), scaledMinWidth, scaledMaxWidth);
    }
}
