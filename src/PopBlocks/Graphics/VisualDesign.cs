using Microsoft.Maui.Graphics;
using PopBlocks.Engine.Models;

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

    public static Color BaseColour(CellColour c) => c switch
    {
        CellColour.Cyan => Cyan,
        CellColour.Coral => Coral,
        CellColour.Yellow => Yellow,
        CellColour.Purple => Purple,
        CellColour.Lime => Lime,
        _ => EmptyCell,
    };

    public static Color BorderColour(CellColour c) => c switch
    {
        CellColour.Cyan => CyanBorder,
        CellColour.Coral => CoralBorder,
        CellColour.Yellow => YellowBorder,
        CellColour.Purple => PurpleBorder,
        CellColour.Lime => LimeBorder,
        _ => GridLine,
    };

    public static Color HighlightColour(CellColour c) => c switch
    {
        CellColour.Cyan => CyanHighlight,
        CellColour.Coral => CoralHighlight,
        CellColour.Yellow => YellowHighlight,
        CellColour.Purple => PurpleHighlight,
        CellColour.Lime => LimeHighlight,
        _ => PrimaryText,
    };

    /// <summary>Saturated, glowing version of a block colour.</summary>
    public static Color NeonColour(CellColour c) => c switch
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
    public const float BoardMargin = 4f;
    public const float BoardPadding = 7f;
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
    public const int ChainAccentMs = 900;
    public const int ShakeMs = 320;
}
