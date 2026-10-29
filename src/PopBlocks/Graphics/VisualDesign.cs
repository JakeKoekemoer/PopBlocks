using Microsoft.Maui.Graphics;
using PopBlocks.Engine.Models;
using PopBlocks.Engine.Services;

namespace PopBlocks.Graphics;

/// <summary>
/// Every colour and drawing constant used by the game, in one place.
/// Sizes expressed as fractions are fractions of the cell size.
/// </summary>
public static class VisualDesign
{
    // ---- Core palette (mirrors App.xaml) ----
    public static readonly Color Background = Color.FromArgb("#070A16");
    public static readonly Color BoardTop = Color.FromArgb("#161F3F");
    public static readonly Color BoardBottom = Color.FromArgb("#0B1128");
    public static readonly Color BoardRim = Color.FromArgb("#7C8CFF");
    public static readonly Color EmptyCell = Color.FromArgb("#1B2547");
    public static readonly Color GridLine = Color.FromArgb("#2A3560");
    public static readonly Color PrimaryText = Color.FromArgb("#F8FAFC");
    public static readonly Color SecondaryText = Color.FromArgb("#9AA6C8");
    public static readonly Color Gold = Color.FromArgb("#FFC83D");

    public static readonly Color Cyan = Color.FromArgb("#38BDF8");
    public static readonly Color Coral = Color.FromArgb("#FB7185");
    public static readonly Color Yellow = Color.FromArgb("#FBBF24");
    public static readonly Color Purple = Color.FromArgb("#A78BFA");
    public static readonly Color Lime = Color.FromArgb("#A3E635");

    // Borders and highlights
    private static readonly Color CyanBorder = Color.FromArgb("#0284C7");
    private static readonly Color CoralBorder = Color.FromArgb("#E11D48");
    private static readonly Color YellowBorder = Color.FromArgb("#D97706");
    private static readonly Color PurpleBorder = Color.FromArgb("#7C3AED");
    private static readonly Color LimeBorder = Color.FromArgb("#65A30D");

    private static readonly Color CyanHighlight = Color.FromArgb("#BAE6FD");
    private static readonly Color CoralHighlight = Color.FromArgb("#FFE4E6");
    private static readonly Color YellowHighlight = Color.FromArgb("#FEF3C7");
    private static readonly Color PurpleHighlight = Color.FromArgb("#EDE9FE");
    private static readonly Color LimeHighlight = Color.FromArgb("#ECFCCB");

    // Solid neon versions used while a match is about to pop.
    private static readonly Color CyanNeon = Color.FromArgb("#00E5FF");
    private static readonly Color CoralNeon = Color.FromArgb("#FF2D78");
    private static readonly Color YellowNeon = Color.FromArgb("#FFE600");
    private static readonly Color PurpleNeon = Color.FromArgb("#C040FF");
    private static readonly Color LimeNeon = Color.FromArgb("#39FF14");

    /// <summary>Symbols are drawn in a dark tone so they read on all five bright fills.</summary>
    public static readonly Color Symbol = Color.FromArgb("#0B1220").WithAlpha(0.72f);

    // ---- The active skin ----
    // A skin is chosen once, when a game starts (and by the wardrobe's previews), never mid-level. The shipped look keeps
    // its hand-tuned colours below; any other skin fills these tables once, so nothing is allocated while drawing.
    private static SkinSpec? _skin;
    private static readonly Color[] SkinFace = new Color[6];
    private static readonly Color[] SkinBorder = new Color[6];
    private static readonly Color[] SkinHighlight = new Color[6];
    private static readonly Color[] SkinNeon = new Color[6];
    private static readonly CellColour[] Five =
        { CellColour.Cyan, CellColour.Coral, CellColour.Yellow, CellColour.Purple, CellColour.Lime };

    // Patchwork draws each colour as another block set. Its per-colour sets are kept here, as a table, until the next UseSkin.
    private static bool _patch;
    private static readonly SkinSpec?[] PatchSource = new SkinSpec?[6];

    /// <summary>
    /// Some block sets hide their symbols for a cleaner look. Colour assist (a setting) or the High Contrast board puts the
    /// symbols back on every set, so colour is never the only cue for anyone who needs the shapes.
    /// </summary>
    public static bool ColourAssist { get; set; }

    /// <summary>The skin in use. The five symbols are drawn the same way whatever it is.</summary>
    public static SkinSpec ActiveSkin => _skin ?? CosmeticCatalog.Classic;

    /// <summary>The block set a colour is drawn with: the active skin, or for Patchwork the set chosen for that colour.</summary>
    public static SkinSpec SpecFor(CellColour c) =>
        _patch && c != CellColour.Empty && PatchSource[(int)c] is { } source ? source : ActiveSkin;

    public static float CornerFor(CellColour c) => CornerRadius * (float)SpecFor(c).CornerScale;
    public static float BorderFor(CellColour c) => BorderWidth * (float)SpecFor(c).BorderScale;
    public static HighlightStyle HighlightFor(CellColour c) => SpecFor(c).Highlight;
    public static SkinPattern PatternFor(CellColour c) => SpecFor(c).Pattern;
    public static bool BevelFor(CellColour c) => SpecFor(c).InnerBevel;

    /// <summary>This colour draws no symbol: its set hides them and colour assist is off.</summary>
    public static bool HidesSymbol(CellColour c) => !ColourAssist && SpecFor(c).HideSymbol;

    public static Color SymbolFor(CellColour c) => SpecFor(c).LightSymbol
        ? Color.FromArgb(SkinSpec.PaleSymbol).WithAlpha((float)SkinSpec.SymbolAlpha)
        : Symbol;

    /// <summary>Corner radius as a fraction of the cell, after the skin's scale.</summary>
    public static float Corner => CornerRadius * (float)(_skin?.CornerScale ?? 1.0);

    /// <summary>Border width as a fraction of the cell, after the skin's scale.</summary>
    public static float Border => BorderWidth * (float)(_skin?.BorderScale ?? 1.0);

    public static HighlightStyle Highlight => _skin?.Highlight ?? HighlightStyle.Gloss;

    public static SkinPattern Pattern => _skin?.Pattern ?? SkinPattern.None;

    /// <summary>The colour of the symbol on each block (dark on every shipped skin; pale on a skin that asks for it).</summary>
    public static Color SymbolNow => _skin is { LightSymbol: true }
        ? Color.FromArgb(SkinSpec.PaleSymbol).WithAlpha((float)SkinSpec.SymbolAlpha)
        : Symbol;

    /// <summary>Switches to a skin. Null or the classic skin goes back to the shipped colours.</summary>
    public static void UseSkin(SkinSpec? skin)
    {
        _patch = false;
        if (skin is null || skin.Id == CosmeticCatalog.DefaultSkin)
        {
            _skin = null;
            return;
        }

        foreach (var c in Five)
        {
            int i = (int)c;
            SkinFace[i] = Color.FromArgb(skin.Face(c));
            SkinBorder[i] = Color.FromArgb(skin.Border(c));
            SkinHighlight[i] = Color.FromArgb(skin.HighlightColour(c));
            SkinNeon[i] = Color.FromArgb(skin.Neon(c));
        }
        _skin = skin;
    }

    /// <summary>
    /// Patchwork: each of the five colours is drawn as the block set chosen for it. The sets are resolved to a table here, once,
    /// when the game starts (or a wardrobe tile is drawn); a colour with no entry is drawn as Classic.
    /// </summary>
    public static void UsePatchwork(IReadOnlyDictionary<CellColour, SkinSpec> sources)
    {
        foreach (var c in Five)
        {
            int i = (int)c;
            var source = sources.TryGetValue(c, out var s) ? s : CosmeticCatalog.Classic;
            PatchSource[i] = source;
            if (source.Id == CosmeticCatalog.DefaultSkin)
            {
                // Classic keeps its hand-tuned borders and highlights, not the derived ones.
                SkinFace[i] = ClassicBase(c);
                SkinBorder[i] = ClassicBorder(c);
                SkinHighlight[i] = ClassicHighlight(c);
                SkinNeon[i] = ClassicNeon(c);
            }
            else
            {
                SkinFace[i] = Color.FromArgb(source.Face(c));
                SkinBorder[i] = Color.FromArgb(source.Border(c));
                SkinHighlight[i] = Color.FromArgb(source.HighlightColour(c));
                SkinNeon[i] = Color.FromArgb(source.Neon(c));
            }
        }
        _skin = CosmeticCatalog.SkinOf(CosmeticCatalog.PatchworkSkin);
        _patch = true;
    }

    /// <summary>Shows a skin for a moment (the wardrobe's previews) and puts the previous one back when disposed.</summary>
    public static IDisposable Preview(SkinSpec skin, IReadOnlyDictionary<CellColour, SkinSpec>? patchwork = null)
    {
        var scope = new SkinScope(_skin, _patch ? (SkinSpec?[])PatchSource.Clone() : null);
        if (skin.IsPatchwork && patchwork is not null) UsePatchwork(patchwork);
        else UseSkin(skin);
        return scope;
    }

    private sealed class SkinScope : IDisposable
    {
        private readonly SkinSpec? _previous;
        private readonly SkinSpec?[]? _previousPatch;
        public SkinScope(SkinSpec? previous, SkinSpec?[]? previousPatch)
        {
            _previous = previous;
            _previousPatch = previousPatch;
        }

        public void Dispose()
        {
            if (_previousPatch is not null)
            {
                var back = new Dictionary<CellColour, SkinSpec>();
                foreach (var c in Five) back[c] = _previousPatch[(int)c] ?? CosmeticCatalog.Classic;
                UsePatchwork(back);
            }
            else UseSkin(_previous);
        }
    }

    public static Color BaseColour(CellColour c) => _skin is not null && c != CellColour.Empty ? SkinFace[(int)c] : ClassicBase(c);

    public static Color BorderColour(CellColour c) => _skin is not null && c != CellColour.Empty ? SkinBorder[(int)c] : ClassicBorder(c);

    public static Color HighlightColour(CellColour c) => _skin is not null && c != CellColour.Empty ? SkinHighlight[(int)c] : ClassicHighlight(c);

    /// <summary>Saturated, glowing version of a block colour.</summary>
    public static Color NeonColour(CellColour c) => _skin is not null && c != CellColour.Empty ? SkinNeon[(int)c] : ClassicNeon(c);

    private static Color ClassicBase(CellColour c) => c switch
    {
        CellColour.Cyan => Cyan,
        CellColour.Coral => Coral,
        CellColour.Yellow => Yellow,
        CellColour.Purple => Purple,
        CellColour.Lime => Lime,
        _ => EmptyCell,
    };

    private static Color ClassicBorder(CellColour c) => c switch
    {
        CellColour.Cyan => CyanBorder,
        CellColour.Coral => CoralBorder,
        CellColour.Yellow => YellowBorder,
        CellColour.Purple => PurpleBorder,
        CellColour.Lime => LimeBorder,
        _ => GridLine,
    };

    private static Color ClassicHighlight(CellColour c) => c switch
    {
        CellColour.Cyan => CyanHighlight,
        CellColour.Coral => CoralHighlight,
        CellColour.Yellow => YellowHighlight,
        CellColour.Purple => PurpleHighlight,
        CellColour.Lime => LimeHighlight,
        _ => PrimaryText,
    };

    private static Color ClassicNeon(CellColour c) => c switch
    {
        CellColour.Cyan => CyanNeon,
        CellColour.Coral => CoralNeon,
        CellColour.Yellow => YellowNeon,
        CellColour.Purple => PurpleNeon,
        CellColour.Lime => LimeNeon,
        _ => PrimaryText,
    };

    // ---- Block geometry ----
    public const float CellInset = 0.06f;
    public const float CornerRadius = 0.26f;
    public const float BorderWidth = 0.035f;
    public const float HighlightWidth = 0.045f;

    /// <summary>Overlap between touching cells so anti-aliasing never leaves a hairline seam.</summary>
    public const float SeamOverlap = 0.75f;

    // ---- Accessibility symbols ----
    public const float SymbolRadius = 0.21f;
    public const float PlusThickness = 0.13f;
    public const float CircleScale = 0.85f;

    // ---- Board ----
    // Kept small so the board fills as much of its area as possible.
    public const float BoardMargin = 1f;
    public const float BoardPadding = 5f;
    public const float BoardCornerRadiusInCells = 0.45f;
    public const float GridLineWidth = 1f;
    public const float GridLineAlpha = 0.35f;
    public const float EmptyCellInset = 0.14f;
    public const float GhostAlpha = 0.5f;

    // ---- Pop timing (milliseconds) ----
    /// <summary>How long the match glows and then sits in solid neon before it pops.</summary>
    public const int HighlightMs = 560;

    /// <summary>Share of the highlight spent glowing before it turns solid neon.</summary>
    public const float GlowShare = 0.45f;

    /// <summary>Pause after the pop while blocks above fall into the gap.</summary>
    public const int SettleMs = 260;

    public const int BurstDurationMs = 620;
    public const int FallDurationMs = 230;
    public const int ChainAccentMs = 1200;
    public const int ShakeMs = 320;
}
