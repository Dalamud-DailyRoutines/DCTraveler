using System;
using System.IO;
using Dalamud.Interface;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace DCTraveler.Managers;

public static class TitleScreenButtonManager
{
    private static IReadOnlyTitleScreenMenuEntry? button;

    internal static void Init()
    {
        Service.UIBuilder.Draw += AddEntry;
    }

    private static void AddEntry()
    {
        var icon = Service.TextureProvider.GetFromFile(Path.Combine(Service.PI.AssemblyLocation.DirectoryName!, "tsm.png"));
        button = Service.TitleScreenMenu.AddEntry("大区选择", icon, () => Plugin.OpenDcSelectWindow());
        Service.UIBuilder.Draw -= AddEntry;
    }

    internal static void Uninit()
    {
        if (button != null) 
        {
            Service.TitleScreenMenu.RemoveEntry(button);
            button = null;
        }
    }
} 