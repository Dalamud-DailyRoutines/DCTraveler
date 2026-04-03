using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;

namespace DCTravelerX.Managers;

public class FontManager
{
    private static readonly unsafe ushort[] DefaultFontRange =
        BuildRange
        (
            null,
            ImGui.GetIO().Fonts.GetGlyphRangesDefault(),
            ImGui.GetIO().Fonts.GetGlyphRangesChineseFull(),
            ImGui.GetIO().Fonts.GetGlyphRangesKorean()
        );

    public static IFontAtlas FontAtlas { get; } = Service.PI.UiBuilder.CreateFontAtlas(FontAtlasAutoRebuildMode.Disable);

    public static IFontHandle UIFont { get; private set; } = FontAtlas.NewGameFontHandle(new(GameFontFamilyAndSize.Axis18));

    internal static void Init() =>
        Task.Run(async () => UIFont = await CreateFontHandleAsync(21f));

    private static async Task<IFontHandle> CreateFontHandleAsync(float size)
    {
        var handle = FontAtlas.NewDelegateFontHandle
        (e =>
            {
                e.OnPreBuild
                (tk =>
                    {
                        var defaultFontPtr = tk.AddDalamudDefaultFont(size, DefaultFontRange);

                        var mixedFontPtr0 = tk.AddGameSymbol
                        (
                            new()
                            {
                                SizePx     = size,
                                PixelSnapH = true,
                                MergeFont  = defaultFontPtr
                            }
                        );

                        tk.AddFontAwesomeIconFont
                        (
                            new()
                            {
                                SizePx     = size,
                                PixelSnapH = true,
                                MergeFont  = mixedFontPtr0
                            }
                        );
                    }
                );
            }
        );

        await FontAtlas.BuildFontsAsync();
        return handle;
    }

    private static unsafe ushort[] BuildRange(ushort[]? extraRanges, params ushort*[] nativeRanges)
    {
        var builder = new ImFontGlyphRangesBuilderPtr(ImGuiNative.ImFontGlyphRangesBuilder());

        try
        {
            foreach (var range in nativeRanges)
                builder.AddRanges(range);

            if (extraRanges is { Length: > 0 })
            {
                fixed (ushort* p = extraRanges)
                    builder.AddRanges(p);
            }

            builder.AddText("ΑαΒβΓγΔδΕεΖζΗηΘθΙιΚκΛλΜμΝνΞξΟοΠπΡρΣσΤτΥυΦφΧχΨψΩω");
            builder.AddText("←→↑↓《》■※☀★★☆♥♡ヅツッシ☀☁☂℃℉°♀♂♠♣♦♣♧®©™€$£♯♭♪✓√◎◆◇♦■□〇●△▽▼▲‹›≤≥<«─＼～⅓½¼⅔¾✓✗");
            builder.AddText("ŒœĂăÂâÎîȘșȚț");
            builder.AddChar('⓪');

            Span<ushort> specificRange = [0x2460, 0x24B5, 0];
            fixed (ushort* p = specificRange)
                builder.AddRanges(p);

            return builder.BuildRangesToArray();
        }
        finally
        {
            builder.Destroy();
        }
    }

    internal static void Uninit() { }
}
