namespace PopBlocks.Engine.Services;

/// <summary>
/// Small colour helpers for cosmetics. They work on <c>#RRGGBB</c> text so skins can be described and tested in the
/// Engine (no drawing library), and the app turns the result into its own colour type. The tests use these to prove a
/// skin keeps each block colour's hue, keeps the five colours apart, and keeps the symbol readable.
/// </summary>
public static class ColourMath
{
    public static (double R, double G, double B) Parse(string hex)
    {
        if (hex is null || hex.Length != 7 || hex[0] != '#') throw new FormatException($"'{hex}' is not #RRGGBB.");
        return (Convert.ToInt32(hex.Substring(1, 2), 16) / 255.0,
                Convert.ToInt32(hex.Substring(3, 2), 16) / 255.0,
                Convert.ToInt32(hex.Substring(5, 2), 16) / 255.0);
    }

    public static string ToHex(double r, double g, double b)
    {
        static int Byte(double v) => (int)Math.Round(Math.Clamp(v, 0.0, 1.0) * 255.0);
        return $"#{Byte(r):X2}{Byte(g):X2}{Byte(b):X2}";
    }

    public static bool IsHex(string hex)
    {
        if (hex is null || hex.Length != 7 || hex[0] != '#') return false;
        for (int i = 1; i < 7; i++) if (!Uri.IsHexDigit(hex[i])) return false;
        return true;
    }

    // ---- Hue, saturation, value ----

    public static (double H, double S, double V) ToHsv(string hex)
    {
        var (r, g, b) = Parse(hex);
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        double h = 0;
        if (d > 1e-9)
        {
            if (max == r) h = 60 * (((g - b) / d) % 6);
            else if (max == g) h = 60 * ((b - r) / d + 2);
            else h = 60 * ((r - g) / d + 4);
            if (h < 0) h += 360;
        }
        return (h, max <= 1e-9 ? 0 : d / max, max);
    }

    public static string FromHsv(double h, double s, double v)
    {
        h = ((h % 360) + 360) % 360;
        s = Math.Clamp(s, 0, 1);
        v = Math.Clamp(v, 0, 1);
        double c = v * s, x = c * (1 - Math.Abs((h / 60) % 2 - 1)), m = v - c;
        (double r, double g, double b) = (int)(h / 60) switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return ToHex(r + m, g + m, b + m);
    }

    /// <summary>Smallest angle between two hues, in degrees (0 to 180).</summary>
    public static double HueDistance(string a, string b)
    {
        double d = Math.Abs(ToHsv(a).H - ToHsv(b).H) % 360;
        return Math.Min(d, 360 - d);
    }

    // ---- Derived shades (same hue as the face) ----

    /// <summary>The face colour mixed towards black. Hue is unchanged.</summary>
    public static string Darken(string hex, double amount)
    {
        var (r, g, b) = Parse(hex);
        double k = 1 - Math.Clamp(amount, 0, 1);
        return ToHex(r * k, g * k, b * k);
    }

    /// <summary>The face colour mixed towards white.</summary>
    public static string Lighten(string hex, double amount)
    {
        var (r, g, b) = Parse(hex);
        double t = Math.Clamp(amount, 0, 1);
        return ToHex(r + (1 - r) * t, g + (1 - g) * t, b + (1 - b) * t);
    }

    /// <summary>The solid, glowing version used while a match is about to pop: same hue, full saturation and brightness.</summary>
    public static string Neon(string hex)
    {
        var (h, _, _) = ToHsv(hex);
        return FromHsv(h, 1.0, 1.0);
    }

    // ---- Perception and legibility ----

    private static double Linear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

    public static double Luminance(string hex)
    {
        var (r, g, b) = Parse(hex);
        return 0.2126 * Linear(r) + 0.7152 * Linear(g) + 0.0722 * Linear(b);
    }

    /// <summary>CIE L*a*b* (D65).</summary>
    public static (double L, double A, double B) ToLab(string hex)
    {
        var (r0, g0, b0) = Parse(hex);
        double r = Linear(r0), g = Linear(g0), b = Linear(b0);
        double x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047;
        double y = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        double z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883;
        static double F(double t) => t > 0.008856 ? Math.Cbrt(t) : 7.787 * t + 16.0 / 116.0;
        return (116 * F(y) - 16, 500 * (F(x) - F(y)), 200 * (F(y) - F(z)));
    }

    /// <summary>Perceived difference between two colours (CIE76 delta E). Around 2 is barely noticeable; 25 or more is clearly different.</summary>
    public static double DeltaE(string a, string b)
    {
        var (l1, a1, b1) = ToLab(a);
        var (l2, a2, b2) = ToLab(b);
        return Math.Sqrt((l1 - l2) * (l1 - l2) + (a1 - a2) * (a1 - a2) + (b1 - b2) * (b1 - b2));
    }

    /// <summary>
    /// WCAG contrast ratio between a symbol drawn at <paramref name="symbolAlpha"/> over a face colour.
    /// 3 is the minimum for graphics that carry meaning.
    /// </summary>
    public static double SymbolContrast(string face, string symbol, double symbolAlpha)
    {
        var (fr, fg, fb) = Parse(face);
        var (sr, sg, sb) = Parse(symbol);
        double a = Math.Clamp(symbolAlpha, 0, 1);
        string blended = ToHex(sr * a + fr * (1 - a), sg * a + fg * (1 - a), sb * a + fb * (1 - a));
        double l1 = Luminance(face), l2 = Luminance(blended);
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }
}
