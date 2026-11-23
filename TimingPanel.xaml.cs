using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace FoxAutoClicker;

public partial class TimingPanel : UserControl
{
    bool _loading = true;

    public TimingPanel()
    {
        InitializeComponent();
        _loading = false;
        Loc.Instance.PropertyChanged += (_, _) => Update();
        Update();
    }

    /// <summary>Tekrar sayısının birimi için dil anahtarı (ör. unit_click).</summary>
    public string RepeatUnitKey
    {
        set => UnitText.SetBinding(TextBlock.TextProperty, new Binding($"[{value}]") { Source = Loc.Instance });
    }

    public Timing Value
    {
        get => new()
        {
            Hours = Num(TbH.Text, 999),
            Minutes = Num(TbM.Text, 9999),
            Seconds = Num(TbS.Text, 99999),
            Millis = Num(TbMs.Text, 999999),
            Jitter = Num(TbJitter.Text, 99999),
            Limit = RbCount.IsChecked == true,
            Count = Math.Max(1, Num(TbCount.Text, 999999999)),
        };
        set
        {
            _loading = true;
            TbH.Text = value.Hours.ToString();
            TbM.Text = value.Minutes.ToString();
            TbS.Text = value.Seconds.ToString();
            TbMs.Text = value.Millis.ToString();
            TbJitter.Text = value.Jitter.ToString();
            TbCount.Text = value.Count.ToString();
            RbCount.IsChecked = value.Limit;
            RbInf.IsChecked = !value.Limit;
            _loading = false;
            Update();
        }
    }

    static int Num(string s, int max) => int.TryParse(s, out var n) ? Math.Clamp(n, 0, max) : 0;

    void Digits(object sender, TextCompositionEventArgs e) => e.Handled = !e.Text.All(char.IsDigit);

    void OnChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loading) Update();
    }

    void Preset_Click(object sender, RoutedEventArgs e)
    {
        var ms = int.Parse((string)((RadioButton)sender).Tag);
        _loading = true;
        TbH.Text = "0"; TbM.Text = "0";
        TbS.Text = (ms / 1000).ToString();
        TbMs.Text = (ms % 1000).ToString();
        _loading = false;
        Update();
    }

    void Custom_Click(object sender, RoutedEventArgs e)
    {
        PsCustom.IsChecked = true;
        TbMs.Focus();
        TbMs.SelectAll();
    }

    void Update()
    {
        if (Summary == null) return;
        var t = Value;

        // Ön ayar seçimi: değerler bir ön ayarla eşleşiyorsa o, yoksa "Özel"
        var total = t.TotalMs;
        PsFast.IsChecked = total == 50;
        PsNormal.IsChecked = total == 250;
        PsSlow.IsChecked = total == 1000;
        PsCustom.IsChecked = total != 50 && total != 250 && total != 1000;

        if (total <= 0)
        {
            Summary.Text = Loc.T("interval_zero");
            return;
        }
        var text = Loc.F("interval_total", t.Describe());
        if (total <= 1000) text += Loc.F("interval_rate", $"{1000.0 / total:0.#}");
        if (total < 10) text += Loc.T("interval_short");
        Summary.Text = text;
    }
}
