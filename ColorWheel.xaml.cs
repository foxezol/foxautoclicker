using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FoxAutoClicker;

/// <summary>
/// Renk çarkı: açı = ton, merkezden uzaklık = doygunluk. Parlaklık dışarıdan (kaydırıcı) verilir.
/// </summary>
public partial class ColorWheel : UserControl
{
    const int PixelSize = 320;      // keskin görünsün diye yüksek çözünürlüklü çizilir

    double _hue;                    // 0..360
    double _saturation;             // 0..1
    bool _dragging;

    /// <summary>(renk, sürükleme bitti mi). Yalnızca kullanıcı etkileşiminde tetiklenir.</summary>
    public event Action<Color, bool>? ColorChanged;

    public ColorWheel()
    {
        InitializeComponent();
        WheelImage.Source = BuildWheel();
        SizeChanged += (_, _) => UpdateThumb();
        UpdateThumb();
    }

    /// <summary>Parlaklık 0..1.</summary>
    public double Value
    {
        get => _value;
        set { _value = Math.Clamp(value, 0, 1); UpdateThumb(); }
    }
    double _value = 1;

    public Color Color => FromHsv(_hue, _saturation, _value);

    /// <summary>Rengi dışarıdan ayarlar (olay tetiklemez).</summary>
    public void SetColor(Color c)
    {
        var (h, s, v) = ToHsv(c);
        if (s > 0.001) _hue = h;            // gri tonlarda ton korunur
        _saturation = s;
        _value = v;
        UpdateThumb();
    }

    // ───────────────────────── çizim ─────────────────────────

    static WriteableBitmap BuildWheel()
    {
        var bmp = new WriteableBitmap(PixelSize, PixelSize, 96, 96, PixelFormats.Pbgra32, null);
        var pixels = new byte[PixelSize * PixelSize * 4];
        var c = PixelSize / 2.0;
        for (var y = 0; y < PixelSize; y++)
        {
            for (var x = 0; x < PixelSize; x++)
            {
                double dx = (x + 0.5 - c) / c, dy = (y + 0.5 - c) / c;
                var r = Math.Sqrt(dx * dx + dy * dy);
                var coverage = Math.Clamp((1 - r) * c + 0.5, 0, 1);       // kenarda yumuşak geçiş
                if (coverage <= 0) continue;

                var hue = (Math.Atan2(dy, dx) * 180 / Math.PI + 360) % 360;
                var rgb = FromHsv(hue, Math.Min(1, r), 1);
                var i = (y * PixelSize + x) * 4;
                pixels[i] = (byte)(rgb.B * coverage);
                pixels[i + 1] = (byte)(rgb.G * coverage);
                pixels[i + 2] = (byte)(rgb.R * coverage);
                pixels[i + 3] = (byte)(255 * coverage);
            }
        }
        bmp.WritePixels(new Int32Rect(0, 0, PixelSize, PixelSize), pixels, PixelSize * 4, 0);
        bmp.Freeze();
        return bmp;
    }

    void UpdateThumb()
    {
        if (Thumb == null) return;
        double w = ActualWidth > 0 ? ActualWidth : Width, h = ActualHeight > 0 ? ActualHeight : Height;
        var radius = Math.Min(w, h) / 2;
        var angle = _hue * Math.PI / 180;
        var x = w / 2 + Math.Cos(angle) * _saturation * radius;
        var y = h / 2 + Math.Sin(angle) * _saturation * radius;

        Canvas.SetLeft(Thumb, x - Thumb.Width / 2);
        Canvas.SetTop(Thumb, y - Thumb.Height / 2);
        Canvas.SetLeft(ThumbOuter, x - ThumbOuter.Width / 2);
        Canvas.SetTop(ThumbOuter, y - ThumbOuter.Height / 2);
        Thumb.Fill = new SolidColorBrush(Color);
    }

    // ───────────────────────── etkileşim ─────────────────────────

    void Surface_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        Surface.CaptureMouse();
        Pick(e.GetPosition(Surface), final: false);
    }

    void Surface_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragging) Pick(e.GetPosition(Surface), final: false);
    }

    void Surface_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        Surface.ReleaseMouseCapture();
        Pick(e.GetPosition(Surface), final: true);
    }

    void Pick(Point p, bool final)
    {
        double w = ActualWidth, h = ActualHeight;
        double dx = p.X - w / 2, dy = p.Y - h / 2;
        var radius = Math.Min(w, h) / 2;
        _saturation = Math.Min(1, Math.Sqrt(dx * dx + dy * dy) / radius);
        if (_saturation > 0.001) _hue = (Math.Atan2(dy, dx) * 180 / Math.PI + 360) % 360;
        UpdateThumb();
        ColorChanged?.Invoke(Color, final);
    }

    // ───────────────────────── renk dönüşümleri ─────────────────────────

    public static Color FromHsv(double h, double s, double v)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = v - c;
        (double r, double g, double b) = (h % 360) switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }

    public static (double H, double S, double V) ToHsv(Color color)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var d = max - min;
        double h = 0;
        if (d > 0)
        {
            if (max == r) h = 60 * (((g - b) / d) % 6);
            else if (max == g) h = 60 * ((b - r) / d + 2);
            else h = 60 * ((r - g) / d + 4);
            if (h < 0) h += 360;
        }
        return (h, max <= 0 ? 0 : d / max, max);
    }
}
