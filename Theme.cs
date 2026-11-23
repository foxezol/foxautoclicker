using System.Windows;
using System.Windows.Media;

namespace FoxAutoClicker;

/// <summary>FoxFix tasarım sistemi: belirteçler ve vurgu türetme kuralları (bkz. TASARIM-SISTEMI 2.1–2.2).</summary>
public static class Theme
{
    public const string DefaultAccent = "#C9A0DC";

    /// <summary>Tema ya da vurgu rengi değişince tetiklenir (pencere ikonları yenilensin).</summary>
    public static event Action? Changed;
    public static Color AccentColor { get; private set; } = Colors.White;
    public static Color AccentForegroundColor { get; private set; } = Colors.Black;

    /// <summary>Hazır vurgu renkleri (dil anahtarı, hex). Gerisi "özel renk" çarkından seçilir.</summary>
    public static readonly (string Key, string Hex)[] Presets =
    {
        ("accent_lavender", "#C9A0DC"), ("accent_orange", "#FF6A1F"),
    };

    sealed record Pal(string Bg, string Card, string Divider, string Fg, string Muted,
                      string Btn2, string Raised, string Ok, string Warn, string Bad);

    static readonly Pal Dark = new("#0D1220", "#151B2D", "#232B42", "#EAEEF7", "#8F9AB5",
                                   "#1E2640", "#27304D", "#4BD58F", "#F5B946", "#FF7A70");
    static readonly Pal Light = new("#EBEFF6", "#FFFFFF", "#D5DBE8", "#141A2B", "#566079",
                                    "#E1E6F1", "#DCE2EF", "#15774A", "#965800", "#BF2A2F");

    public static bool TryParse(string? s, out Color c)
    {
        c = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim();
        if (!s.StartsWith('#')) s = "#" + s;
        if (s.Length != 7) return false;
        try { c = (Color)ColorConverter.ConvertFromString(s); return true; }
        catch { return false; }
    }

    static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    static double Lin(byte v)
    {
        var s = v / 255.0;
        return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }

    static double Lum(Color c) => 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B);

    public static double Ratio(Color a, Color b)
    {
        var x = Lum(a) + 0.05; var y = Lum(b) + 0.05;
        return x > y ? x / y : y / x;
    }

    static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t),
        (byte)Math.Round(a.B + (b.B - a.B) * t));

    /// <summary>Rengi, tüm yüzeylerde en az <paramref name="min"/> karşıtlık verene kadar hedefe doğru çeker.</summary>
    static Color Pull(Color c, Color target, double min, params Color[] on)
    {
        for (var i = 0; i <= 50; i++)
        {
            var x = Mix(c, target, i / 50.0);
            if (on.All(b => Ratio(x, b) >= min)) return x;
        }
        return target;
    }

    static void Set(string key, Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        Application.Current.Resources[key] = b;
    }

    public static void Apply(bool dark, string accentHex)
    {
        var p = dark ? Dark : Light;
        if (!TryParse(accentHex, out var given)) TryParse(DefaultAccent, out given);

        var bg = C(p.Bg); var card = C(p.Card);
        var target = dark ? Colors.White : Colors.Black;

        var accent = Pull(given, target, 3.0, bg, card);
        var soft = Mix(card, accent, dark ? 0.16 : 0.10);

        var accentText = Pull(accent, target, 4.5, bg, card, soft);
        var accentFg = Ratio(Colors.White, accent) >= Ratio(Colors.Black, accent) ? Colors.White : Colors.Black;

        Set("Background", bg);
        Set("Card", card);
        Set("Divider", C(p.Divider));
        Set("Foreground", C(p.Fg));
        Set("Muted", C(p.Muted));
        Set("ButtonSecondary", C(p.Btn2));
        Set("Raised", C(p.Raised));
        Set("StatusOk", C(p.Ok));
        Set("StatusWarn", C(p.Warn));
        Set("StatusBad", C(p.Bad));
        Set("StatusNeutral", C(p.Muted));
        Set("Scrim", Color.FromArgb(0xA6, 0, 0, 0));
        Set("Accent", accent);
        Set("AccentText", accentText);
        Set("AccentForeground", accentFg);
        Set("AccentSoft", soft);
        Set("OkSoft", Mix(card, C(p.Ok), 0.08));
        Set("WarnSoft", Mix(card, C(p.Warn), 0.08));
        Set("BadSoft", Mix(card, C(p.Bad), 0.08));

        AccentColor = accent;
        AccentForegroundColor = accentFg;
        Changed?.Invoke();
    }
}
