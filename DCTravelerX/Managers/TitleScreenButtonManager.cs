using System.IO;
using Dalamud.Interface;

namespace DCTravelerX.Managers;

public static class TitleScreenButtonManager
{
    internal static IReadOnlyTitleScreenMenuEntry? Button { get; private set; }

    internal static void Init() =>
        AddEntry();

    private static void AddEntry()
    {
        var icon = Service.TextureProvider.GetFromFile(Path.Combine(Service.PI.AssemblyLocation.DirectoryName!, "Assets", "TitleScreenButton.png"));
        Button = Service.TitleScreenMenu.AddEntry("大区选择", icon, WindowManager.OpenDcSelectWindow);

        Service.UIBuilder.Draw -= AddEntry;
    }

    internal static void Uninit()
    {
        if (Button == null) return;

        Service.TitleScreenMenu.RemoveEntry(Button);
        Button = null;
    }
}
