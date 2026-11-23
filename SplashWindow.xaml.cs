using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace FoxAutoClicker;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
    }

    /// <summary>İlerleme 0..1; çubuk yumuşakça dolar, yüzde metni hemen güncellenir.</summary>
    public void SetProgress(double value)
    {
        value = Math.Clamp(value, 0, 1);
        Percent.Text = $"{(int)Math.Round(value * 100)}%";
        if (!SystemParameters.ClientAreaAnimation) { FillScale.ScaleX = value; return; }
        FillScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(value, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    public void FadeAndClose()
    {
        if (!SystemParameters.ClientAreaAnimation) { Close(); return; }
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
    }
}
