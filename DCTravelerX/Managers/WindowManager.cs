using System;
using System.Linq;
using Dalamud.Interface.Windowing;
using DCTravelerX.Managers;
using DCTravelerX.Windows;

namespace DCTravelerX.Managers;

public class WindowManager
{
    public static WindowSystem? WindowSystem { get; private set; }

    internal static void Init()
    {
        WindowSystem ??= new WindowSystem("DCTravelerX");
        WindowSystem.RemoveAllWindows();
        
        InternalWindows.Init();

        Service.UIBuilder.Draw += DrawWindows;
    }

    private static void DrawWindows()
    {
        using var font = FontManager.UIFont.Push();
        
        WindowSystem?.Draw();
    }

    public static bool AddWindow(Window? window)
    {
        if (WindowSystem == null || window == null) return false;

        var addedWindows = WindowSystem.Windows;
        if (addedWindows.Contains(window) || addedWindows.Any(x => x.WindowName == window.WindowName))
            return false;

        WindowSystem.AddWindow(window);
        return true;
    }

    public static bool RemoveWindow(Window? window)
    {
        if (WindowSystem == null || window == null) return false;

        var addedWindows = WindowSystem.Windows;
        if (!addedWindows.Contains(window)) return false;

        WindowSystem.RemoveWindow(window);
        return true;
    }

    public static T? Get<T>() where T : Window
        => WindowSystem?.Windows.FirstOrDefault(x => x.GetType() == typeof(T)) as T;

    public static void OpenDcSelectWindow() =>
        Get<DCGroupSelectorWindow>()?.Open(ServerDataManager.SdoAreas);

    internal static void Uninit()
    {
        Service.UIBuilder.Draw -= DrawWindows;
        
        InternalWindows.Uninit();
        
        WindowSystem?.RemoveAllWindows();
        WindowSystem = null;
    }

    private static class InternalWindows
    {
        internal static void Init()
        {
            AddWindow(new WorldSelectorWindows());
            AddWindow(new DCGroupSelectorWindow());
        }

        internal static void Uninit()
        {
            Get<WorldSelectorWindows>()?.Dispose();
            Get<DCGroupSelectorWindow>()?.Dispose();
        }
    }
}
