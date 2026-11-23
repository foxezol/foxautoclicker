using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace FoxAutoClicker;

/// <param name="Page">Adımın gösterileceği sekme ("Mouse", "Keyboard", "Profiles", "Settings").</param>
/// <param name="Target">Vurgulanacak öğe; null ise ekran tamamen kararır ve balon ortada çıkar.</param>
public sealed record TourStep(string Page, Func<FrameworkElement?>? Target, string TitleKey, string TextKey, double Pad = 8);

/// <summary>
/// Yeni kullanıcılar için adım adım tur: ekranı karartır, öğeyi çerçeveler, yanına açıklama balonu koyar.
/// Sağ/sol ok ya da Enter ile ilerler; Esc turu bitirir.
/// </summary>
public partial class TourOverlay : UserControl
{
    readonly RectangleGeometry _outer = new();
    readonly RectangleGeometry _hole = new() { RadiusX = 14, RadiusY = 14 };
    readonly RectangleGeometry _ring = new() { RadiusX = 14, RadiusY = 14 };

    IReadOnlyList<TourStep> _steps = Array.Empty<TourStep>();
    Action<string>? _goToPage;
    Action<bool>? _finished;
    string? _lastPage;
    int _index;
    bool _busy;

    public bool IsActive => Visibility == Visibility.Visible;

    public TourOverlay()
    {
        InitializeComponent();
        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        group.Children.Add(_outer);
        group.Children.Add(_hole);
        Dim.Data = group;
        Ring.Data = _ring;

        SizeChanged += (_, _) =>
        {
            _outer.Rect = new Rect(0, 0, ActualWidth, ActualHeight);
            if (IsActive && !_busy) _ = PlaceAsync(animate: false);
        };
        Loc.Instance.PropertyChanged += (_, _) => { if (IsActive) UpdateTexts(); };
    }

    public void Run(IReadOnlyList<TourStep> steps, Action<string> goToPage, Action<bool> finished)
    {
        _steps = steps;
        _goToPage = goToPage;
        _finished = finished;
        _lastPage = null;
        _index = 0;

        BuildDots();
        Visibility = Visibility.Visible;
        _outer.Rect = new Rect(0, 0, ActualWidth, ActualHeight);
        var center = new Rect(ActualWidth / 2, ActualHeight / 2, 0, 0);
        _hole.Rect = center;
        _ring.Rect = center;
        Opacity = 1;
        if (SystemParameters.ClientAreaAnimation)
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
        _ = ShowStepAsync(0, first: true);
    }

    public bool HandleKey(Key key)
    {
        if (!IsActive) return false;
        switch (key)
        {
            case Key.Escape: Finish(completed: false); return true;
            case Key.Right or Key.Enter: Next(); return true;
            case Key.Left: Back(); return true;
            default: return false;
        }
    }

    // ───────────────────────── adımlar ─────────────────────────

    async Task ShowStepAsync(int index, bool first)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            _index = index;
            var step = _steps[index];
            var pageChanged = step.Page != _lastPage;
            _lastPage = step.Page;
            _goToPage?.Invoke(step.Page);
            UpdateTexts();

            // sekme geçiş animasyonu (240 ms) bitmeden ölçüm yapma: öğe hâlâ kayıyor olur
            if (pageChanged && !first && SystemParameters.ClientAreaAnimation) await Task.Delay(290);
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);

            step.Target?.Invoke()?.BringIntoView();
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);

            await PlaceAsync(animate: true);
            NextBtn.Focus();
        }
        finally { _busy = false; }
    }

    void UpdateTexts()
    {
        if (_index >= _steps.Count) return;
        var step = _steps[_index];
        TitleText.Text = Loc.T(step.TitleKey);
        BodyText.Text = Loc.T(step.TextKey);
        ProgressText.Text = Loc.F("tour_progress", _index + 1, _steps.Count);
        BackBtn.Visibility = _index > 0 ? Visibility.Visible : Visibility.Collapsed;
        NextBtn.Content = Loc.T(_index == _steps.Count - 1 ? "tour_done" : "tour_next");
        SkipTextBtn.Visibility = _index == _steps.Count - 1 ? Visibility.Collapsed : Visibility.Visible;
        for (var i = 0; i < Dots.Children.Count; i++)
            ((Ellipse)Dots.Children[i]).SetResourceReference(Shape.FillProperty, i == _index ? "Accent" : "Raised");
    }

    void BuildDots()
    {
        Dots.Children.Clear();
        for (var i = 0; i < _steps.Count; i++)
            Dots.Children.Add(new Ellipse { Width = 7, Height = 7, Margin = new Thickness(0, 0, 5, 0) });
    }

    /// <summary>Deliği ve balonu hedefe göre yerleştirir.</summary>
    async Task PlaceAsync(bool animate)
    {
        var step = _steps[_index];
        var target = step.Target?.Invoke();
        var view = new Rect(0, 0, ActualWidth, ActualHeight);

        Rect? bounds = null;
        if (target is { IsVisible: true, ActualWidth: > 0 })
        {
            var r = target.TransformToVisual(this).TransformBounds(new Rect(0, 0, target.ActualWidth, target.ActualHeight));
            r.Inflate(step.Pad, step.Pad);
            r.Intersect(view);
            if (!r.IsEmpty) bounds = r;
        }

        var to = bounds ?? new Rect(ActualWidth / 2, ActualHeight / 2, 0, 0);
        if (animate && SystemParameters.ClientAreaAnimation)
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var span = TimeSpan.FromMilliseconds(300);
            _hole.BeginAnimation(RectangleGeometry.RectProperty, new RectAnimation(to, span) { EasingFunction = ease });
            _ring.BeginAnimation(RectangleGeometry.RectProperty, new RectAnimation(to, span) { EasingFunction = ease });
        }
        else
        {
            _hole.BeginAnimation(RectangleGeometry.RectProperty, null);
            _ring.BeginAnimation(RectangleGeometry.RectProperty, null);
            _hole.Rect = to;
            _ring.Rect = to;
        }
        Ring.Visibility = bounds == null ? Visibility.Collapsed : Visibility.Visible;

        PlacePopover(bounds, view);

        if (animate && SystemParameters.ClientAreaAnimation)
        {
            Pop.BeginAnimation(OpacityProperty, new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(220)));
            Arrow.BeginAnimation(OpacityProperty, new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(220)));
        }
        await Task.CompletedTask;
    }

    void PlacePopover(Rect? bounds, Rect view)
    {
        Pop.Measure(new Size(Pop.Width, double.PositiveInfinity));
        var size = Pop.DesiredSize;
        const double margin = 14, gap = 18;
        double x, y;
        Arrow.Visibility = Visibility.Collapsed;

        if (bounds is not { } b)
        {
            x = (view.Width - size.Width) / 2;
            y = (view.Height - size.Height) / 2;
        }
        else
        {
            double cx = b.Left + b.Width / 2, cy = b.Top + b.Height / 2;
            var fitsBelow = b.Bottom + gap + size.Height <= view.Height - margin;
            var fitsAbove = b.Top - gap - size.Height >= margin;
            var fitsRight = b.Right + gap + size.Width <= view.Width - margin;
            var fitsLeft = b.Left - gap - size.Width >= margin;

            var side = fitsBelow ? "bottom" : fitsAbove ? "top" : fitsRight ? "right" : fitsLeft ? "left" : "bottom";
            switch (side)
            {
                case "bottom":
                case "top":
                    x = Math.Clamp(cx - size.Width / 2, margin, Math.Max(margin, view.Width - size.Width - margin));
                    y = side == "bottom" ? b.Bottom + gap : b.Top - gap - size.Height;
                    break;
                default:
                    y = Math.Clamp(cy - size.Height / 2, margin, Math.Max(margin, view.Height - size.Height - margin));
                    x = side == "right" ? b.Right + gap : b.Left - gap - size.Width;
                    break;
            }
            x = Math.Clamp(x, margin, Math.Max(margin, view.Width - size.Width - margin));
            y = Math.Clamp(y, margin, Math.Max(margin, view.Height - size.Height - margin));

            // ok: balonun hedefe bakan kenarında, hedefin ortasına hizalı
            const double half = 7;
            double ax, ay;
            switch (side)
            {
                case "bottom": ax = Math.Clamp(cx, x + 26, x + size.Width - 26) - half; ay = y - half; break;
                case "top": ax = Math.Clamp(cx, x + 26, x + size.Width - 26) - half; ay = y + size.Height - half; break;
                case "right": ax = x - half; ay = Math.Clamp(cy, y + 26, y + size.Height - 26) - half; break;
                default: ax = x + size.Width - half; ay = Math.Clamp(cy, y + 26, y + size.Height - 26) - half; break;
            }
            Canvas.SetLeft(Arrow, ax);
            Canvas.SetTop(Arrow, ay);
            Arrow.Visibility = Visibility.Visible;
        }
        Canvas.SetLeft(Pop, x);
        Canvas.SetTop(Pop, y);
    }

    // ───────────────────────── gezinme ─────────────────────────

    void Next()
    {
        if (_busy) return;
        if (_index >= _steps.Count - 1) Finish(completed: true);
        else _ = ShowStepAsync(_index + 1, first: false);
    }

    void Back()
    {
        if (_busy || _index == 0) return;
        _ = ShowStepAsync(_index - 1, first: false);
    }

    void Finish(bool completed)
    {
        if (!IsActive) return;
        void Done()
        {
            Visibility = Visibility.Collapsed;
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
            _finished?.Invoke(completed);
        }
        if (!SystemParameters.ClientAreaAnimation) { Done(); return; }
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(160));
        fade.Completed += (_, _) => Done();
        BeginAnimation(OpacityProperty, fade);
    }

    void Next_Click(object sender, RoutedEventArgs e) => Next();
    void Back_Click(object sender, RoutedEventArgs e) => Back();
    void Skip_Click(object sender, RoutedEventArgs e) => Finish(completed: false);
}
