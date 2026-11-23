using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace FoxAutoClicker;

/// <summary>
/// Konum seçerken tüm ekranı kaplayan kılavuz katmanı.
/// Performans için ekranın anlık görüntüsünü alıp opak bir pencerede gösterir: saydam (layered) pencere
/// kullanılmaz, her karede tüm ekran yeniden karıştırılmaz. Çizgiler yalnızca imleç hareket edince güncellenir.
/// Tıklayınca imlecin yeri seçilir; Esc ya da sağ tık iptal eder.
/// </summary>
public sealed class PickOverlay : Window
{
    const double Gap = 16;

    readonly Canvas _canvas = new() { IsHitTestVisible = false };
    readonly Line _left = Guide(), _right = Guide(), _top = Guide(), _bottom = Guide();
    readonly Ellipse _ring = new() { Width = 26, Height = 26, StrokeThickness = 2 };
    readonly Ellipse _dot = new() { Width = 6, Height = 6 };
    readonly Border _coords = new() { CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 5, 10, 5) };
    readonly TextBlock _coordsText = new() { FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 13 };
    readonly Border _banner = new() { CornerRadius = new CornerRadius(18), Padding = new Thickness(18, 10, 18, 10) };
    readonly TextBlock _bannerText = new() { FontWeight = FontWeights.SemiBold };
    readonly BitmapSourceHolder _shot;

    int _vx, _vy, _vw, _vh;
    int _lastX = int.MinValue, _lastY = int.MinValue;
    bool _done;

    public Native.POINT? Result { get; private set; }

    sealed record BitmapSourceHolder(ImageSource? Source);

    static Line Guide() => new() { StrokeThickness = 1.5, Opacity = 0.9, IsHitTestVisible = false, SnapsToDevicePixels = true };

    public PickOverlay()
    {

        _vx = Native.GetSystemMetrics(76); _vy = Native.GetSystemMetrics(77);
        _vw = Native.GetSystemMetrics(78); _vh = Native.GetSystemMetrics(79);
        _shot = new BitmapSourceHolder(Native.CaptureScreen(_vx, _vy, _vw, _vh));

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Background = Brushes.Black;
        Topmost = true;
        ShowInTaskbar = false;
        Cursor = Cursors.Cross;
        Left = 0; Top = 0; Width = 200; Height = 200;
        UseLayoutRounding = true;

        var accent = Res("Accent");
        foreach (var l in new[] { _left, _right, _top, _bottom }) { l.Stroke = accent; _canvas.Children.Add(l); }
        _ring.Stroke = accent;
        _dot.Fill = Res("Foreground");
        _coords.Background = Res("Card");
        _coordsText.Foreground = Res("Foreground");
        _coords.Child = _coordsText;
        _banner.Background = Res("Card");
        _bannerText.Foreground = Res("Foreground");
        _bannerText.Text = Loc.T("pick_banner");
        _banner.Child = _bannerText;
        foreach (UIElement e in new UIElement[] { _ring, _dot, _coords, _banner }) _canvas.Children.Add(e);

        var root = new Grid();
        if (_shot.Source != null)
            root.Children.Add(new Image { Source = _shot.Source, Stretch = Stretch.Fill, IsHitTestVisible = false });
        root.Children.Add(new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(0x59, 0, 0, 0)), IsHitTestVisible = false });
        root.Children.Add(_canvas);
        Content = root;

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            Native.SetWindowPos(hwnd, new IntPtr(-1), _vx, _vy, _vw, _vh, 0x0040); // TOPMOST + SHOWWINDOW
        };
        Loaded += (_, _) =>
        {
            Activate(); Focus();
            OnFrame(null, EventArgs.Empty);
            FadeInBanner();
            CompositionTarget.Rendering += OnFrame;   // ekran yenilemesiyle eşlenir; iş yalnızca değişince yapılır
        };
        Closed += (_, _) => CompositionTarget.Rendering -= OnFrame;

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { e.Handled = true; Finish(false); }
        };
        MouseLeftButtonDown += (_, _) => Finish(true);
        MouseRightButtonDown += (_, _) => Finish(false);
    }

    static Brush Res(string key) => (Brush)Application.Current.Resources[key];

    void FadeInBanner()
    {
        if (!SystemParameters.ClientAreaAnimation) return;
        _banner.Opacity = 0;
        _banner.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    void OnFrame(object? sender, EventArgs e)
    {
        if (_done) return;
        Native.GetCursorPos(out var p);

        var moved = p.X != _lastX || p.Y != _lastY;
        var first = _lastX == int.MinValue;

        if (moved)
        {
            _lastX = p.X; _lastY = p.Y;
            var dpi = VisualTreeHelper.GetDpi(this);
            double x = (p.X - _vx) / dpi.DpiScaleX, y = (p.Y - _vy) / dpi.DpiScaleY;
            double w = ActualWidth, h = ActualHeight;

            Set(_left, 0, y, Math.Max(0, x - Gap), y);
            Set(_right, x + Gap, y, w, y);
            Set(_top, x, 0, x, Math.Max(0, y - Gap));
            Set(_bottom, x, y + Gap, x, h);

            Canvas.SetLeft(_ring, x - 13); Canvas.SetTop(_ring, y - 13);
            Canvas.SetLeft(_dot, x - 3); Canvas.SetTop(_dot, y - 3);

            _coordsText.Text = $"X {p.X,4}   Y {p.Y,4}";   // sabit genişlikli rakamlar: etiket titremez
            var cw = _coords.ActualWidth > 0 ? _coords.ActualWidth : 130;
            var ch = _coords.ActualHeight > 0 ? _coords.ActualHeight : 30;
            Canvas.SetLeft(_coords, x + 24 + cw > w ? x - 24 - cw : x + 24);
            Canvas.SetTop(_coords, y + 24 + ch > h ? y - 24 - ch : y + 24);
        }

        if (first)
        {
            _banner.UpdateLayout();
            Canvas.SetLeft(_banner, Math.Max(8, (ActualWidth - _banner.ActualWidth) / 2));
            Canvas.SetTop(_banner, 28);
        }
    }

    static void Set(Line l, double x1, double y1, double x2, double y2)
    {
        l.X1 = x1; l.Y1 = y1; l.X2 = x2; l.Y2 = y2;
    }

    void Finish(bool accept)
    {
        if (_done) return;
        _done = true;
        if (accept) { Native.GetCursorPos(out var p); Result = p; }
        Close();
    }
}
