using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using DCTravelerX.Helpers;
using DCTravelerX.Infos;
using DCTravelerX.Managers;
using DCTravelerX.Travel.Models;
using DCTravelerX.Windows.MessageBox;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace DCTravelerX.Travel.Interaction;

internal sealed class DefaultTravelInteraction : ITravelInteraction
{
    public async Task PrepareSessionAsync(CancellationToken cancellationToken)
    {
        var isQueryingBefore = false;

        while (DCTravelClient.Instance().IsUpdatingAllQueryTime)
        {
            cancellationToken.ThrowIfCancellationRequested();
            isQueryingBefore = true;
            await Task.Delay(100, cancellationToken);
        }

        if (isQueryingBefore)
            await Task.Delay(2_000, cancellationToken);

        Service.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "_TitleLogo", OnAddonTitleLogo);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "_TitleMenu", OnAddonTitleMenu);
    }

    public async Task BeginSubmissionAsync(TravelResolution resolution, CancellationToken cancellationToken)
    {
        if (resolution.ShouldReturnToTitleBeforeSubmit)
            await Service.Framework.RunOnFrameworkThread(GameFunctions.ReturnToTitle);

        await ShowWaitAsync(resolution.InitialWaitMessage, cancellationToken);
    }

    public Task ShowWaitAsync(string message, CancellationToken cancellationToken = default) =>
        WaitAddonManager.Show(message, cancellationToken);

    public Task CloseWaitAsync(CancellationToken cancellationToken = default) =>
        WaitAddonManager.Close(cancellationToken);

    public Task<MessageBoxResult> ShowMessageAsync
    (
        string         title,
        string         message,
        MessageBoxType type        = MessageBoxType.Ok,
        bool           showWebsite = false
    ) =>
        MessageBoxWindow.Show(WindowManager.WindowSystem, title, message, type, showWebsite);

    public async Task<bool> ConfirmMigrationAsync(string targetDcGroupName, bool isIpcCall)
    {
        if (isIpcCall)
            return true;

        var result = await ShowMessageAsync
                     (
                         "超域旅行确认",
                         $"是否确认要超域旅行至大区: {targetDcGroupName}",
                         MessageBoxType.OkCancel
                     );
        return result == MessageBoxResult.Ok;
    }

    public Task ResetTitleIdleTimeAsync() =>
        Service.Framework.RunOnFrameworkThread(GameFunctions.ResetTitleIdleTime);

    public Task SelectDcAndLoginAsync(string targetDcGroupName, bool enterGame) =>
        GameFunctions.SelectDCAndLogin(targetDcGroupName, enterGame);

    public async Task CleanupSessionAsync(bool needReLogin)
    {
        try
        {
            await WaitAddonManager.Close();
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "关闭等待界面时出现异常");
        }

        Service.AddonLifecycle.UnregisterListener(OnAddonTitleLogo);
        Service.AddonLifecycle.UnregisterListener(OnAddonTitleMenu);

        await Service.Framework.RunOnFrameworkThread
        (() =>
            {
                GameFunctions.ToggleTitleMenu(true);
                GameFunctions.ToggleTitleLogo(true);
            }
        );

        if (needReLogin)
            await Service.Framework.RunOnFrameworkThread(GameFunctions.LoginInGame);
    }

    public void CleanupImmediately()
    {
        WaitAddonManager.CloseImmediately();
        Service.AddonLifecycle.UnregisterListener(OnAddonTitleLogo);
        Service.AddonLifecycle.UnregisterListener(OnAddonTitleMenu);
        _ = Service.Framework.RunOnFrameworkThread
        (() =>
            {
                GameFunctions.ToggleTitleMenu(true);
                GameFunctions.ToggleTitleLogo(true);
            }
        );
    }

    public unsafe void PlayCompletionSound() =>
        UIGlobals.PlaySoundEffect(67);

    private static void OnAddonTitleLogo(AddonEvent type, AddonArgs args) =>
        GameFunctions.ToggleTitleLogo(false);

    private static void OnAddonTitleMenu(AddonEvent type, AddonArgs args) =>
        GameFunctions.ToggleTitleMenu(false);
}
