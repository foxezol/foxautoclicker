using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FoxAutoClicker;

/// <summary>Küçük kontrol penceresi. Ayar ve sayaç tutmaz; hepsi ana pencereden gelir.</summary>
public partial class MiniWindow : Window
{
    readonly MainWindow _main;
    bool _closingForReal;
    bool _applying;

    public MiniWindow(MainWindow main)
    {
        _main = main;
        InitializeComponent();
        Icon = AppIcon.Create(Theme.AccentColor, Theme.AccentForegroundColor);
        Closing += OnClosing;
        LocationChanged += (_, _) => _main.RememberMiniPosition(Left, Top);
    }

    public void Apply(UiState s)
    {
        var (bg, fg, dot) = PhaseStyle.Keys(s.Phase);
        Chip.SetResourceReference(Border.BackgroundProperty, bg);
        ChipText.SetResourceReference(TextBlock.ForegroundProperty, fg);
        Dot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, dot);
        ChipText.Text = s.ChipText;

        MClicks.Text = s.Clicks.ToString("N0");
        MKeys.Text = s.Keys.ToString("N0");
        MTime.Text = s.Elapsed.ToString(@"h\:mm\:ss");
        StartLabel.Text = s.StartLabel;
        StartKey.Text = s.KeyText;
        Hint.Text = s.Hint;
        Hint.Visibility = s.Hint.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Anahtarlar ana pencerenin durumunu yansıtır; geri yazma döngüsü olmasın diye _applying korur
        _applying = true;
        SwMouse.IsChecked = s.MouseOn;
        SwKey.IsChecked = s.KeyOn;
        _applying = false;
    }

    void Drag_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    void Start_Click(object sender, RoutedEventArgs e) => _main.ToggleFromMini();
    void SwMouse_Changed(object sender, RoutedEventArgs e) { if (!_applying) _main.SetMouseEnabled(SwMouse.IsChecked == true); }
    void SwKey_Changed(object sender, RoutedEventArgs e) { if (!_applying) _main.SetKeyEnabled(SwKey.IsChecked == true); }
    void Back_Click(object sender, RoutedEventArgs e) => _main.ReturnFromMini();
    void Min_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized; // görev çubuğuna al; kısayol çalışmaya devam eder

    void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closingForReal) return;
        e.Cancel = true;           // Alt+F4: kapatmak yerine ana pencereye dön
        _main.ReturnFromMini();
    }

    public void CloseForReal()
    {
        _closingForReal = true;
        Close();
    }
}
