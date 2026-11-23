using System.IO;
using System.Media;

namespace FoxAutoClicker;

/// <summary>Başlat/durdur sesleri. Dosya gerekmez: kısa, yumuşak tonlar çalışma anında üretilir.</summary>
public static class Sounds
{
    public static bool Enabled { get; set; } = true;

    static SoundPlayer? _start, _stop;

    /// <summary>Yükselen iki nota: tıklayıcı çalışmaya başladı.</summary>
    public static void PlayStart()
    {
        if (!Enabled) return;
        Play(ref _start, new[] { (740.0, 0.075), (988.0, 0.12) });
    }

    /// <summary>Alçalan iki nota: tıklayıcı durdu.</summary>
    public static void PlayStop()
    {
        if (!Enabled) return;
        Play(ref _stop, new[] { (880.0, 0.075), (587.0, 0.15) });
    }

    static void Play(ref SoundPlayer? player, (double Freq, double Seconds)[] notes)
    {
        try
        {
            player ??= Create(notes);
            player.Play();   // asenkron; yeni çalma öncekini keser
        }
        catch { /* ses cihazı yoksa sessizce geç */ }
    }

    static SoundPlayer Create((double Freq, double Seconds)[] notes)
    {
        const int rate = 44100;
        const double amplitude = 0.30;
        var samples = new List<short>();

        foreach (var (freq, seconds) in notes)
        {
            var count = (int)(rate * seconds);
            for (var i = 0; i < count; i++)
            {
                var t = (double)i / rate;
                var attack = Math.Min(1.0, i / (rate * 0.004));            // 4 ms: tıklama sesi olmasın
                var release = Math.Pow(1.0 - (double)i / count, 1.6);       // yumuşak sönüm
                var wave = Math.Sin(2 * Math.PI * freq * t) + 0.22 * Math.Sin(4 * Math.PI * freq * t);
                samples.Add((short)(wave * attack * release * amplitude * short.MaxValue));
            }
        }
        samples.AddRange(new short[(int)(rate * 0.02)]);                    // kuyruk sessizliği

        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            var dataBytes = samples.Count * 2;
            w.Write("RIFF"u8); w.Write(36 + dataBytes); w.Write("WAVE"u8);
            w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write("data"u8); w.Write(dataBytes);
            foreach (var s in samples) w.Write(s);
        }
        ms.Position = 0;
        var player = new SoundPlayer(ms);
        player.Load();
        return player;
    }
}
