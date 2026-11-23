using System.IO;
using System.Media;

namespace FoxAutoClicker;

/// <summary>Başlat/durdur sesleri. Dosya gerekmez: kısa tonlar çalışma anında üretilir; tür ve ses düzeyi seçilebilir.</summary>
public static class Sounds
{
    public const int VariantCount = 5;

    public static bool Enabled { get; set; } = true;
    public static int Variant { get; private set; }
    public static int Volume { get; private set; } = 70;

    static readonly Dictionary<(int Variant, bool Start), SoundPlayer> Cache = new();

    enum Wave { Sine, Square, Bell }

    sealed record Tone(double F0, double F1, double Seconds, Wave Kind, double Decay, double[]? Harmonics = null, double GapBefore = 0);

    public static void Configure(int variant, int volume)
    {
        variant = Math.Clamp(variant, 0, VariantCount - 1);
        volume = Math.Clamp(volume, 0, 100);
        if (variant == Variant && volume == Volume) return;
        Variant = variant;
        Volume = volume;
        foreach (var p in Cache.Values) p.Dispose();
        Cache.Clear();
    }

    public static void PlayStart() { if (Enabled) Play(true); }
    public static void PlayStop() { if (Enabled) Play(false); }

    /// <summary>Ayarlardaki önizleme: açık/kapalı durumundan bağımsız, başlat ardından durdur sesini çalar.</summary>
    public static async void Preview()
    {
        Play(true);
        await Task.Delay(700);
        Play(false);
    }

    public static void PlayStartAlways() => Play(true);

    static void Play(bool start)
    {
        try
        {
            if (Volume == 0) return;
            if (!Cache.TryGetValue((Variant, start), out var player))
            {
                player = Create(Recipe(Variant, start), Gain(Variant));
                Cache[(Variant, start)] = player;
            }
            player.Play();   // asenkron; yeni çalma öncekini keser
        }
        catch { /* ses cihazı yoksa sessizce geç */ }
    }

    static double Gain(int variant) => variant switch { 1 => 1.35, 2 => 0.55, 3 => 0.8, _ => 1.0 };

    static Tone[] Recipe(int variant, bool start)
    {
        var soft = new[] { 1.0, 0.22 };
        switch (variant)
        {
            case 1: // Klik: kısa, kuru tıklamalar
                return start
                    ? new[] { new Tone(1800, 1800, 0.018, Wave.Sine, 3), new Tone(2600, 2600, 0.014, Wave.Sine, 3, null, 0.04) }
                    : new[] { new Tone(1100, 1100, 0.022, Wave.Sine, 3), new Tone(700, 700, 0.026, Wave.Sine, 3, null, 0.05) };
            case 2: // Dijital: kare dalga bipleri
                return start
                    ? new[] { new Tone(880, 880, 0.06, Wave.Square, 1.0), new Tone(1320, 1320, 0.09, Wave.Square, 1.4, null, 0.01) }
                    : new[] { new Tone(660, 660, 0.06, Wave.Square, 1.0), new Tone(440, 440, 0.11, Wave.Square, 1.6, null, 0.01) };
            case 3: // Kristal: çan gibi uzayan tonlar
                return start
                    ? new[] { new Tone(1175, 1175, 0.18, Wave.Bell, 2.2), new Tone(1568, 1568, 0.26, Wave.Bell, 2.4) }
                    : new[] { new Tone(1568, 1568, 0.18, Wave.Bell, 2.2), new Tone(1175, 1175, 0.30, Wave.Bell, 2.4) };
            case 4: // Pop: yükselen / alçalan kayma
                return start
                    ? new[] { new Tone(350, 900, 0.11, Wave.Sine, 1.5, soft) }
                    : new[] { new Tone(900, 350, 0.13, Wave.Sine, 1.5, soft) };
            default: // Yumuşak: iki notalı çınlama
                return start
                    ? new[] { new Tone(740, 740, 0.075, Wave.Sine, 1.6, soft), new Tone(988, 988, 0.12, Wave.Sine, 1.6, soft) }
                    : new[] { new Tone(880, 880, 0.075, Wave.Sine, 1.6, soft), new Tone(587, 587, 0.15, Wave.Sine, 1.6, soft) };
        }
    }

    static SoundPlayer Create(Tone[] tones, double gain)
    {
        const int rate = 44100;
        var amplitude = 0.5 * Volume / 100.0 * gain;
        var samples = new List<short>();
        double[] bellRatios = { 1, 2.76, 5.4, 8.93 };
        double[] bellAmps = { 1, 0.45, 0.22, 0.1 };

        foreach (var tone in tones)
        {
            samples.AddRange(new short[(int)(rate * tone.GapBefore)]);
            var count = (int)(rate * tone.Seconds);
            double phase = 0;
            for (var i = 0; i < count; i++)
            {
                var progress = (double)i / count;
                var freq = tone.F0 + (tone.F1 - tone.F0) * progress;
                phase += 2 * Math.PI * freq / rate;

                var attack = Math.Min(1.0, i / (rate * 0.003));                 // 3 ms: patlama sesi olmasın
                var release = Math.Pow(1.0 - progress, tone.Decay);              // yumuşak sönüm

                double wave;
                switch (tone.Kind)
                {
                    case Wave.Square:
                        wave = Math.Tanh(4 * Math.Sin(phase)) * 0.7;              // hafif yumuşatılmış kare dalga
                        break;
                    case Wave.Bell:
                        wave = 0;
                        for (var k = 0; k < bellRatios.Length; k++)
                            wave += bellAmps[k] * Math.Sin(phase * bellRatios[k]) * Math.Exp(-progress * (1.5 + k * 2.5));
                        wave /= 1.4;
                        break;
                    default:
                        wave = Math.Sin(phase);
                        if (tone.Harmonics is { Length: > 1 }) wave = Math.Sin(phase) * tone.Harmonics[0] + Math.Sin(2 * phase) * tone.Harmonics[1];
                        break;
                }
                var value = Math.Clamp(wave * attack * release * amplitude, -1, 1);
                samples.Add((short)(value * short.MaxValue));
            }
        }
        samples.AddRange(new short[(int)(rate * 0.02)]);                          // kuyruk sessizliği

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
