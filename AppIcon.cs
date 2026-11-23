using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FoxAutoClicker;

/// <summary>Pencere/görev çubuğu simgesi: vurgu renkli yuvarlatılmış kare üstünde tilki; vurgu değişince yeniden çizilir.</summary>
public static class AppIcon
{
    static BitmapImage? _logo;

    public static ImageSource Create(Color tile, Color fox)
    {
        _logo ??= LoadLogo();
        const int size = 256;
        var scale = size * 0.72 / Math.Max(_logo.PixelWidth, _logo.PixelHeight);
        var w = _logo.PixelWidth * scale;
        var h = _logo.PixelHeight * scale;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRoundedRectangle(new SolidColorBrush(tile), null, new Rect(0, 0, size, size), 56, 56);
            dc.PushOpacityMask(new ImageBrush(_logo) { Stretch = Stretch.Uniform });
            dc.DrawRectangle(new SolidColorBrush(fox), null, new Rect((size - w) / 2, (size - h) / 2, w, h));
            dc.Pop();
        }
        var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    static BitmapImage LoadLogo()
    {
        var img = new BitmapImage(new Uri("pack://application:,,,/Assets/logo.png"));
        img.Freeze();
        return img;
    }
}
