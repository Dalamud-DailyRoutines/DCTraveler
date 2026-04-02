using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DCTravelerX.Managers;

internal static class WaitAddonManager
{
    private const string WAIT_ADDON_NAME                  = "LobbyDKT";
    private const int    WAIT_ADDON_CLOSE_POLL_INTERVAL_MS = 50;
    private const int    WAIT_ADDON_CLOSE_TIMEOUT_MS       = 2_000;

    public static async Task Show(string message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Service.Framework.RunOnFrameworkThread(() => UpdateOrOpen(message));
    }

    public static async Task Close(CancellationToken cancellationToken = default)
    {
        await Service.Framework.RunOnFrameworkThread(CloseUnsafe);

        for (var elapsed = 0; elapsed < WAIT_ADDON_CLOSE_TIMEOUT_MS; elapsed += WAIT_ADDON_CLOSE_POLL_INTERVAL_MS)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var hasWaitAddon = await Service.Framework.RunOnFrameworkThread(HasWaitAddonUnsafe);
            if (!hasWaitAddon)
                return;

            await Task.Delay(WAIT_ADDON_CLOSE_POLL_INTERVAL_MS, cancellationToken);
        }
    }

    public static void CloseImmediately() =>
        CloseUnsafe();

    private static unsafe void UpdateOrOpen(string message)
    {
        var addon = GetWaitAddon();
        if (addon != null)
        {
            RefreshWaitAddon(addon, message);
            return;
        }

        var instance = RaptureAtkModule.Instance();
        var row      = instance->AddonNames.Select((name, index) => new { Name = name.ToString(), Index = index })
                               .FirstOrDefault(x => x.Name == WAIT_ADDON_NAME).Index;

        var values = stackalloc AtkValue[3];
        values[0].SetManagedString(message);
        values[1].SetUInt(0);
        instance->OpenAddon((uint)row, 2, values, null, 0, 0, 0);
    }

    private static unsafe void CloseUnsafe()
    {
        var addon = GetWaitAddon();
        if (addon == null) return;

        addon->Close(true);
    }

    private static unsafe bool HasWaitAddonUnsafe() =>
        GetWaitAddon() != null;

    private static unsafe AtkUnitBase* GetWaitAddon() =>
        (AtkUnitBase*)Service.GameGui.GetAddonByName(WAIT_ADDON_NAME).Address;

    private static unsafe void RefreshWaitAddon(AtkUnitBase* addon, string message)
    {
        addon->IsVisible = true;
        addon->AtkValues[0].SetManagedString(message);
        addon->OnRefresh(addon->AtkValuesCount, addon->AtkValues);
    }
}
