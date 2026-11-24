using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace FoxAutoClicker;

public sealed class Timing
{
    public int Hours { get; set; }
    public int Minutes { get; set; }
    public int Seconds { get; set; }
    public int Millis { get; set; } = 100;
    public int Jitter { get; set; }
    public bool Limit { get; set; }
    public int Count { get; set; } = 100;

    public long TotalMs => (((long)Hours * 60 + Minutes) * 60 + Seconds) * 1000 + Millis;
    public double BaseMs => Math.Max(1, TotalMs);

    public double NextDelay()
    {
        var delay = BaseMs;
        if (Jitter > 0) delay += Random.Shared.Next(-Jitter, Jitter + 1);
        return Math.Max(1, delay);
    }

    public string Describe()
    {
        var parts = new List<string>();
        if (Hours > 0) parts.Add($"{Hours} {Loc.T("s_h")}");
        if (Minutes > 0) parts.Add($"{Minutes} {Loc.T("s_m")}");
        if (Seconds > 0) parts.Add($"{Seconds} {Loc.T("s_s")}");
        if (Millis > 0 || parts.Count == 0) parts.Add($"{Math.Max(Millis, 1)} ms");
        return string.Join(" ", parts);
    }
}

public sealed class MouseConfig
{
    public bool Enabled { get; set; } = true;
    public int Button { get; set; }          // eski ayar (0 sol, 1 orta, 2 sağ); Buttons'a taşınır
    public int Buttons { get; set; } = 1;    // bit 0 sol, bit 1 orta, bit 2 sağ: birden fazlası aynı anda basılır
    public bool Double { get; set; }
    public bool Hold { get; set; }
    public bool Fixed { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public Timing Timing { get; set; } = new();
}

public sealed class KeyConfig
{
    public bool Enabled { get; set; }
    public List<int> Keys { get; set; } = new();
    public bool Hold { get; set; }
    public Timing Timing { get; set; } = new() { Millis = 500 };
}

public sealed class Profile
{
    public string Name { get; set; } = "";
    public MouseConfig Mouse { get; set; } = new();
    public KeyConfig Key { get; set; } = new();
}

public sealed class Config
{
    public string Lang { get; set; } = "tr";
    public List<Profile> Profiles { get; set; } = new();
    public double? MiniX { get; set; }
    public double? MiniY { get; set; }
    public int Rev { get; set; } = 3;
    public bool FirstRunDone { get; set; }
    public bool TourDone { get; set; }
    public int SoundVariant { get; set; }
    public int SoundVolume { get; set; } = 70;
    public bool AutoUpdateCheck { get; set; } = true;
    public bool Sounds { get; set; } = true;
    public bool Dark { get; set; } = true;
    public string Accent { get; set; } = Theme.DefaultAccent;
    public int HotkeyVk { get; set; } = 0x75; // F6
    public int HotkeyMods { get; set; }
    public int ButtonDelaySec { get; set; } = 3;
    public MouseConfig Mouse { get; set; } = new();
    public KeyConfig Key { get; set; } = new();

    // FOXCLICKER_DATA ortam değişkeni, ayar klasörünü değiştirir (denemeler için)
    static string PathOnDisk => System.IO.Path.Combine(
        Environment.GetEnvironmentVariable("FOXCLICKER_DATA")
            ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FoxAutoClicker"),
        "settings.json");

    public static T Clone<T>(T value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    public static Config Load()
    {
        try
        {
            if (File.Exists(PathOnDisk))
            {
                var c = JsonSerializer.Deserialize<Config>(File.ReadAllText(PathOnDisk)) ?? new Config();
                if (c.Rev < 2) { c.Accent = Theme.DefaultAccent; c.Rev = 2; }
                if (c.Rev < 3)
                {
                    // 0.3.1: yeni varsayılan vurgu (lavanta) ve çoklu fare düğmesi
                    if (string.Equals(c.Accent, "#FF6A1F", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(c.Accent, "#9E20E6", StringComparison.OrdinalIgnoreCase))
                        c.Accent = Theme.DefaultAccent;
                    c.Mouse.Buttons = 1 << Math.Clamp(c.Mouse.Button, 0, 2);
                    foreach (var p in c.Profiles) p.Mouse.Buttons = 1 << Math.Clamp(p.Mouse.Button, 0, 2);
                    c.Rev = 3;
                }
                return c;
            }
        }
        catch { /* bozuk dosya: varsayılanlarla devam */ }
        return new Config();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathOnDisk)!);
            File.WriteAllText(PathOnDisk, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* yazılamazsa sessizce geç */ }
    }
}

public static class KeyNames
{
    /// <summary>OEM tuşları (noktalama ve ulusal harfler): adı düzene göre değişir, enum adı yanıltıcıdır.</summary>
    static bool IsOemKey(int vk) =>
        (vk >= 0xBA && vk <= 0xC0) || (vk >= 0xDB && vk <= 0xDF) || vk == 0xE2;

    public static string Name(int vk)
    {
        // Türkçe düzende ş, ğ, ü, i, ö, ç tuşları "; [ ] ' , ." gibi görünmesin: etkin düzenin gerçek harfini göster
        if (IsOemKey(vk))
        {
            var ch = Native.KeyChar(vk);
            if (ch > ' ')
                return (char.IsLetter(ch) ? char.ToUpper(ch, System.Globalization.CultureInfo.GetCultureInfo("tr-TR")) : ch).ToString();
        }

        var k = KeyInterop.KeyFromVirtualKey(vk);
        return k switch
        {
            Key.Return => "Enter",
            Key.Back => "Backspace",
            Key.Prior => "Page Up",
            Key.Next => "Page Down",
            Key.Capital => "Caps Lock",
            Key.Escape => "Esc",
            Key.Snapshot => "Print Screen",
            Key.Scroll => "Scroll Lock",
            Key.LeftCtrl => "Sol Ctrl",
            Key.RightCtrl => "Sağ Ctrl",
            Key.LeftAlt => "Sol Alt",
            Key.RightAlt => "Sağ Alt",
            Key.LeftShift => "Sol Shift",
            Key.RightShift => "Sağ Shift",
            Key.LWin => "Sol Win",
            Key.RWin => "Sağ Win",
            Key.OemPlus => "+",
            Key.OemMinus => "-",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.Oem1 => ";",
            Key.Oem2 => "/",
            Key.Oem3 => "`",
            Key.Oem4 => "[",
            Key.Oem5 => "\\",
            Key.Oem6 => "]",
            Key.Oem7 => "'",
            >= Key.D0 and <= Key.D9 => ((int)k - (int)Key.D0).ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => "Num " + ((int)k - (int)Key.NumPad0),
            _ => k.ToString()
        };
    }

    public static string Hotkey(int vk, int mods)
    {
        var parts = new List<string>();
        if ((mods & 2) != 0) parts.Add("Ctrl");
        if ((mods & 1) != 0) parts.Add("Alt");
        if ((mods & 4) != 0) parts.Add("Shift");
        if ((mods & 8) != 0) parts.Add("Win");
        parts.Add(Name(vk));
        return string.Join(" + ", parts);
    }
}
