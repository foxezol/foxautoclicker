using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace FoxAutoClicker;

public partial class MainWindow : Window
{
    const int HotkeyId = 0x4F58;
    const int WmHotkey = 0x0312;

    readonly Config _cfg;
    readonly ClickController _ctl = new();
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    readonly ObservableCollection<KeyItem> _keyItems = new();
    readonly bool _ready;

    HwndSource? _src;
    MiniWindow? _mini;
    bool _hotkeyOk = true;

    // tek tuş / kısayol yakalama
    Func<Key, ModifierKeys, bool>? _capture;
    Button? _captureBtn;
    string _capturePrompt = "";

    // tuş kaydı
    bool _recording;

    string? _notice;
    DateTime _noticeUntil;
    bool _stoppedAfterRun;
    readonly ObservableCollection<ProfileRow> _rows = new();
    ProfileRow? _armedRow;
    DateTime _armedUntil;
    bool _pulsing;
    Phase? _lastPhase;
    bool _tourActive;
    DateTime _resetArmedUntil, _uninstallArmedUntil;
    bool _lastWarn;
    readonly UpdateService _updates = new();
    bool _updating;
    static string AppVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.3.0";
    DateTime _nextHotkeyRetry;

    const string DefaultFont = "";

    public MainWindow(Config cfg)
    {
        _cfg = cfg;
        InitializeComponent();
        KeyChips.ItemsSource = _keyItems;
        BuildSwatches();
        AccentWheel.ColorChanged += Wheel_ColorChanged;
        ApplyClickersToUi();
        ApplyAppToUi();
        ProfileList.ItemsSource = _rows;
        RebuildProfiles();

        _ctl.Finished += () => Dispatcher.BeginInvoke(() => StopAll(finishedNaturally: true));
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        SourceInitialized += (_, _) =>
        {
            _src = (HwndSource)PresentationSource.FromVisual(this)!;
            _src.AddHook(WndProc);
            RegisterHotkey();
            Refresh();
        };
        Closing += (_, _) =>
        {
            _ctl.Stop();
            ReadUi();
            _cfg.Save();
            UnregisterHotkey();
            _mini?.CloseForReal();
        };
        Closed += (_, _) => Application.Current.Shutdown();

        _ready = true;
        ApplySoundConfig();
        UpdateIcon();
        Theme.Changed += UpdateIcon;
        Refresh();

        Loaded += async (_, _) =>
        {
            if (!_cfg.FirstRunDone)
            {
                await Task.Delay(450);
                ShowOverlay(HotkeyOverlay, HotkeyCard, HotkeyStack);
                FocusAfterEntrance(HotkeyOkBtn);
                return;                                   // ilk açılışta güncelleme penceresi araya girmesin
            }
            if (!_cfg.AutoUpdateCheck) return;
            await Task.Delay(1200);                       // açılış sakinleşsin
            await CheckUpdatesAsync(manual: false);
        };
    }

    void UpdateIcon()
    {
        Icon = AppIcon.Create(Theme.AccentColor, Theme.AccentForegroundColor);
        if (_mini != null) _mini.Icon = Icon;
    }

    void ApplySoundConfig()
    {
        Sounds.Enabled = _cfg.Sounds;
        Sounds.Configure(_cfg.SoundVariant, _cfg.SoundVolume);
    }

    // ───────────────────────── Kısayol ─────────────────────────

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            Toggle(fromButton: false);
            handled = true;
        }
        return IntPtr.Zero;
    }

    void RegisterHotkey()
    {
        if (_src == null) return;
        _hotkeyOk = Native.RegisterHotKey(_src.Handle, HotkeyId, (uint)_cfg.HotkeyMods | 0x4000, (uint)_cfg.HotkeyVk);
    }

    void UnregisterHotkey()
    {
        if (_src != null) Native.UnregisterHotKey(_src.Handle, HotkeyId);
    }

    string HotkeyText => KeyNames.Hotkey(_cfg.HotkeyVk, _cfg.HotkeyMods);

    // ───────────────────────── Başlat / durdur ─────────────────────────

    void Toggle(bool fromButton)
    {
        if (_capture != null || _tourActive) return;

        if (_ctl.IsActive)
        {
            StopAll(finishedNaturally: false);
            return;
        }

        if (_recording)
        {
            Notice(Loc.T("notice_recording"));
            return;
        }

        ReadUi();
        if (!_cfg.Mouse.Enabled && !_cfg.Key.Enabled)
        {
            Notice(Loc.T("notice_none"));
            return;
        }
        if (_cfg.Mouse.Enabled && _cfg.Mouse.Buttons == 0)
        {
            Notice(Loc.T("notice_nobutton"));
            return;
        }
        if (_cfg.Key.Enabled && !_cfg.Mouse.Enabled && _cfg.Key.Keys.Count == 0)
        {
            Notice(Loc.T("notice_nokeys"));
            return;
        }

        _cfg.Save();
        _stoppedAfterRun = false;
        _notice = null;
        _ctl.Start(_cfg, fromButton ? _cfg.ButtonDelaySec : 0);
        Refresh();
    }

    void StopAll(bool finishedNaturally)
    {
        if (!_ctl.IsActive) return;
        var cancelledCountdown = _ctl.IsWaiting;
        _ctl.Stop();
        _stoppedAfterRun = !cancelledCountdown;
        _cfg.Save();
        Refresh();
    }

    void Notice(string text)
    {
        _notice = text;
        _noticeUntil = DateTime.Now.AddSeconds(5);
        Refresh();
    }

    void Toggle_Click(object sender, RoutedEventArgs e) => Toggle(fromButton: true);

    // Mini pencere köprüsü
    public void ToggleFromMini() => Toggle(fromButton: true);
    public void SetMouseEnabled(bool on) { ChkMouse.IsChecked = on; Refresh(); }
    public void SetKeyEnabled(bool on) { ChkKey.IsChecked = on; Refresh(); }

    public void RememberMiniPosition(double left, double top)
    {
        _cfg.MiniX = left; _cfg.MiniY = top;
    }

    void Mini_Click(object sender, RoutedEventArgs e)
    {
        if (_capture != null) EndCapture();
        _mini ??= new MiniWindow(this);

        var wa = SystemParameters.WorkArea;
        var x = _cfg.MiniX ?? wa.Right - 360;
        var y = _cfg.MiniY ?? wa.Top + 60;
        var vs = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                          SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (!vs.Contains(new Point(x + 40, y + 20))) { x = wa.Right - 360; y = wa.Top + 60; }
        _mini.WindowStartupLocation = WindowStartupLocation.Manual;
        _mini.Left = x; _mini.Top = y;

        _mini.Apply(BuildState());
        _mini.Show();
        Hide();
    }

    public void ReturnFromMini()
    {
        _mini?.Hide();
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Refresh();
    }

    // ───────────────────────── Arayüz ↔ ayar ─────────────────────────

    void ApplyClickersToUi()
    {
        var m = _cfg.Mouse;
        ChkMouse.IsChecked = m.Enabled;
        MbLeft.IsChecked = (m.Buttons & 1) != 0;
        MbMiddle.IsChecked = (m.Buttons & 2) != 0;
        MbRight.IsChecked = (m.Buttons & 4) != 0;
        (m.Hold ? MtHold : m.Double ? MtDouble : MtSingle).IsChecked = true;
        (m.Fixed ? PosFixed : PosCursor).IsChecked = true;
        TbX.Text = m.X.ToString();
        TbY.Text = m.Y.ToString();
        MouseTiming.Value = m.Timing;
        MouseTiming.RepeatUnitKey = "unit_click";

        var k = _cfg.Key;
        ChkKey.IsChecked = k.Enabled;
        _keyItems.Clear();
        foreach (var vk in k.Keys) _keyItems.Add(new KeyItem { Vk = vk });
        Reindex();
        (k.Hold ? KmHold : KmPress).IsChecked = true;
        KeyTiming.Value = k.Timing;
        KeyTiming.RepeatUnitKey = "unit_round";
    }

    void ApplyAppToUi()
    {
        (_cfg.Dark ? ThemeDark : ThemeLight).IsChecked = true;
        (_cfg.Lang == "en" ? LangEn : LangTr).IsChecked = true;
        TbHex.Text = _cfg.Accent;
        SyncSwatches();
        SyncWheel();
        TbDelay.Text = _cfg.ButtonDelaySec.ToString();
        HotkeyChip.Text = HotkeyText;
        ChkAutoUpdate.IsChecked = _cfg.AutoUpdateCheck;
        ChkSounds.IsChecked = _cfg.Sounds;
        SoundRadio(_cfg.SoundVariant).IsChecked = true;
        SoundVolume.Value = _cfg.SoundVolume;
        SoundVolumeText.Text = $"{_cfg.SoundVolume}%";
    }

    RadioButton SoundRadio(int variant) => variant switch { 1 => SndClick, 2 => SndDigital, 3 => SndCrystal, 4 => SndPop, _ => SndSoft };

    void ReadUi()
    {
        var m = _cfg.Mouse;
        m.Enabled = ChkMouse.IsChecked == true;
        m.Buttons = (MbLeft.IsChecked == true ? 1 : 0) | (MbMiddle.IsChecked == true ? 2 : 0) | (MbRight.IsChecked == true ? 4 : 0);
        m.Double = MtDouble.IsChecked == true;
        m.Hold = MtHold.IsChecked == true;
        m.Fixed = PosFixed.IsChecked == true;
        m.X = Num(TbX.Text, 99999);
        m.Y = Num(TbY.Text, 99999);
        m.Timing = MouseTiming.Value;

        _cfg.Key.Enabled = ChkKey.IsChecked == true;
        _cfg.Key.Hold = KmHold.IsChecked == true;
        _cfg.Key.Timing = KeyTiming.Value;

        _cfg.ButtonDelaySec = Num(TbDelay.Text, 30);
        _cfg.AutoUpdateCheck = ChkAutoUpdate.IsChecked == true;
        _cfg.Sounds = ChkSounds.IsChecked == true;
        _cfg.SoundVolume = (int)SoundVolume.Value;
        _cfg.SoundVariant = SndClick.IsChecked == true ? 1 : SndDigital.IsChecked == true ? 2 : SndCrystal.IsChecked == true ? 3 : SndPop.IsChecked == true ? 4 : 0;
        ApplySoundConfig();
    }

    static int Num(string s, int max) => int.TryParse(s, out var n) ? Math.Clamp(n, 0, max) : 0;

    void Digits(object sender, TextCompositionEventArgs e) => e.Handled = !e.Text.All(char.IsDigit);

    // ───────────────────────── Durum ve yenileme ─────────────────────────

    UiState BuildState()
    {
        var active = _ctl.IsActive;
        var waiting = _ctl.IsWaiting;
        var hotkey = HotkeyText;

        var phase = waiting ? Phase.Countdown : active ? Phase.Running : _stoppedAfterRun ? Phase.Stopped : Phase.Ready;
        var chip = phase switch
        {
            Phase.Countdown => $"{Loc.T("st_countdown")} {Math.Max(1, (int)Math.Ceiling(_ctl.RemainingMs / 1000.0))}",
            Phase.Running => Loc.T("st_running"),
            Phase.Stopped => Loc.T("st_stopped"),
            _ => Loc.T("st_ready"),
        };

        string hint;
        if (!_hotkeyOk) hint = Loc.F("hotkey_conflict", hotkey);
        else if (_notice != null && DateTime.Now < _noticeUntil && !active) hint = _notice;
        else hint = "";

        var startLabel = waiting ? Loc.T("btn_cancel") : active ? Loc.T("btn_stop") : Loc.T("btn_start");

        return new UiState(phase, chip, hint, _ctl.MouseCount, _ctl.KeyCount, _ctl.Elapsed,
                           ChkMouse.IsChecked == true, ChkKey.IsChecked == true, startLabel, hotkey);
    }

    void Refresh()
    {
        if (!_ready) return;
        ReadUi();

        // kısayol başka bir uygulama tarafından tutuluyorsa, boşalınca yeniden dene
        if (!_hotkeyOk && _capture == null && DateTime.Now >= _nextHotkeyRetry)
        {
            _nextHotkeyRetry = DateTime.Now.AddSeconds(2);
            RegisterHotkey();
        }

        var s = BuildState();
        if (s.Phase != _lastPhase)
        {
            var (bg, fg, dot) = PhaseStyle.Keys(s.Phase);
            StateChip.SetResourceReference(Border.BackgroundProperty, bg);
            StateTitle.SetResourceReference(TextBlock.ForegroundProperty, fg);
            StateDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, dot);
            OnPhaseChanged(_lastPhase, s.Phase);
            _lastPhase = s.Phase;
        }
        StateTitle.Text = s.ChipText;
        StateText.Text = s.Hint;
        StateText.Visibility = s.Hint.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        var warnHint = _notice != null && DateTime.Now < _noticeUntil && !_ctl.IsActive || !_hotkeyOk;
        if (warnHint != _lastWarn || _lastPhase == null)
        {
            _lastWarn = warnHint;
            StateText.SetResourceReference(TextBlock.ForegroundProperty, warnHint ? "StatusWarn" : "Muted");
        }
        ToggleLabel.Text = s.StartLabel;
        ToggleKey.Text = s.KeyText;

        MetricMouse.Text = s.Clicks.ToString("N0");
        MetricKey.Text = s.Keys.ToString("N0");
        MetricTime.Text = s.Elapsed.ToString(@"h\:mm\:ss");

        // geri sayım katmanı
        var waiting = s.Phase == Phase.Countdown;
        CountdownOverlay.Visibility = waiting ? Visibility.Visible : Visibility.Collapsed;
        if (waiting) CountdownNumber.Text = Math.Max(1, (int)Math.Ceiling(_ctl.RemainingMs / 1000.0)).ToString();

        // kapalı bölümler
        var mouseOn = s.MouseOn; var keyOn = s.KeyOn;
        ApplyShields();
        MouseOffNote.Visibility = mouseOn ? Visibility.Collapsed : Visibility.Visible;
        KeyOffNote.Visibility = keyOn ? Visibility.Collapsed : Visibility.Visible;

        MouseTiming.Visibility = _cfg.Mouse.Hold ? Visibility.Collapsed : Visibility.Visible;
        MouseHoldNote.Visibility = _cfg.Mouse.Hold ? Visibility.Visible : Visibility.Collapsed;
        KeyTiming.Visibility = _cfg.Key.Hold ? Visibility.Collapsed : Visibility.Visible;

        // klavye: kayıt durumu, boş durum, akış açıklaması
        KeyEmpty.Visibility = _keyItems.Count == 0 && !_recording ? Visibility.Visible : Visibility.Collapsed;
        RecBanner.Visibility = _recording ? Visibility.Visible : Visibility.Collapsed;
        RecCount.Text = Loc.F("recorded_n", _keyItems.Count);
        RecHint.Text = Loc.F("record_hint", HotkeyText);
        RecordBtn.Style = (Style)FindResource(_recording ? "PrimaryButton" : "SecondaryButton");
        RecordBtn.Content = Loc.T(_recording ? "record_stop" : "record_start");
        AddKeyBtn.IsEnabled = !_recording;
        UndoBtn.IsEnabled = !_recording && _keyItems.Count > 0;
        ClearBtn.IsEnabled = !_recording && _keyItems.Count > 0;
        KeyChips.IsEnabled = !_recording;
        KeyFlow.Text = DescribeFlow();

        AddKeyBtn.Content = _captureBtn == AddKeyBtn ? Loc.T(_capturePrompt) : Loc.T("add_key");
        HotkeyBtn.Content = _captureBtn == HotkeyBtn ? Loc.T(_capturePrompt) : Loc.T("hotkey_change");
        if (HotkeyOverlay.Visibility == Visibility.Visible)
        {
            HotkeyDlgChangeBtn.Content = _captureBtn == HotkeyDlgChangeBtn ? Loc.T(_capturePrompt) : Loc.T("hotkey_change");
            HotkeyDlgKey.Text = HotkeyText;
            HotkeyDlgMsg.Text = _hotkeyOk ? "" : Loc.F("hotkey_conflict", HotkeyText);
        }
        HotkeyChip.Text = HotkeyText;

        // profiller
        ProfilesEmpty.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_armedRow != null && DateTime.Now >= _armedUntil) { _armedRow.DeleteArmed = false; _armedRow = null; }
        ResetBtn.Content = Loc.T(DateTime.Now < _resetArmedUntil ? "maint_reset_confirm" : "maint_reset");
        UninstallBtn.Content = Loc.T(DateTime.Now < _uninstallArmedUntil ? "maint_uninstall_confirm" : "maint_uninstall");

        UpdatePulse(s.Phase == Phase.Running);
        if (_mini is { IsVisible: true }) _mini.Apply(s);

        // pencere gizli/küçültülmüşken ve çalışmıyorken daha seyrek yenile (boşta işlemci kullanımı düşer)
        var idle = !_ctl.IsActive && (!IsVisible || WindowState == WindowState.Minimized) && _mini is not { IsVisible: true };
        var wanted = TimeSpan.FromMilliseconds(idle ? 500 : 100);
        if (_timer.Interval != wanted) _timer.Interval = wanted;
    }

    /// <summary>Çalışmaya başlayınca yükselen, durunca alçalan kısa ses.</summary>
    void OnPhaseChanged(Phase? from, Phase to)
    {
        if (from == null) return;
        if (to == Phase.Running && from != Phase.Running) Sounds.PlayStart();
        else if (from is Phase.Running or Phase.Countdown && to is Phase.Stopped or Phase.Ready) Sounds.PlayStop();
    }

    string DescribeFlow()
    {
        if (_keyItems.Count == 0) return Loc.T("flow_empty");
        if (_cfg.Key.Hold)
            return Loc.F("flow_hold", string.Join(" + ", _keyItems.Select(k => k.Name)));
        return Loc.F("flow_press", string.Join(" → ", _keyItems.Select(k => k.Name)), _cfg.Key.Timing.Describe());
    }

    /// <summary>Çalışırken durum noktasının halkası, tıklama hızında atar.</summary>
    void UpdatePulse(bool on)
    {
        if (on == _pulsing) return;
        _pulsing = on;
        if (!on || !SystemParameters.ClientAreaAnimation)
        {
            PulseScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            PulseScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            PulseRing.BeginAnimation(OpacityProperty, null);
            PulseRing.Opacity = 0;
            return;
        }
        var t = !_cfg.Mouse.Enabled || _cfg.Mouse.Hold ? _cfg.Key.Timing : _cfg.Mouse.Timing;
        var ms = Math.Clamp(t.BaseMs, 450, 2000);
        var d = new Duration(TimeSpan.FromMilliseconds(ms));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var grow = new DoubleAnimation(1, 2.4, d) { RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease };
        PulseScale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        PulseScale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        PulseRing.BeginAnimation(OpacityProperty, new DoubleAnimation(0.55, 0, d) { RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease });
    }

    // ───────────────────────── Hakkında ve güncelleme ─────────────────────────

    void Brand_Click(object sender, RoutedEventArgs e)
    {
        AboutVersionText.Text = Loc.F("version", AppVersion);
        AboutMsg.Text = "";
        ShowOverlay(AboutOverlay, AboutCard, AboutStack);
        FocusAfterEntrance(AboutCloseBtn);
    }

    void AboutOverlay_MouseDown(object sender, MouseButtonEventArgs e) => HideOverlay(AboutOverlay, AboutCard);
    void Card_MouseDown(object sender, MouseButtonEventArgs e) => e.Handled = true;   // kart içi tık katmanı kapatmasın
    void AboutClose_Click(object sender, RoutedEventArgs e) => HideOverlay(AboutOverlay, AboutCard);

    void AboutGitHub_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(UpdateService.RepoUrl) { UseShellExecute = true }); }
        catch { /* tarayıcı açılamazsa sessizce geç */ }
    }

    void AboutShare_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText($"{Loc.T("share_text")}\n{UpdateService.RepoUrl}");
            AboutMsg.Text = Loc.T("about_copied");
        }
        catch { /* pano başka uygulamada kilitli olabilir */ }
    }

    void CheckUpdate_Click(object sender, RoutedEventArgs e) => _ = CheckUpdatesAsync(manual: true);

    async Task CheckUpdatesAsync(bool manual)
    {
        if (_updating) return;
        if (manual) { CheckUpdateBtn.IsEnabled = false; UpdateStatus.Text = Loc.T("update_checking"); }

        var result = await _updates.CheckAsync();
        if (manual) CheckUpdateBtn.IsEnabled = true;

        switch (result.State)
        {
            case UpdateState.Available:
                UpdateStatus.Text = Loc.F("update_available", result.Version!);
                UpdateText.Text = Loc.F(_updates.CanSelfUpdate ? "update_text" : "update_text_portable", result.Version!);
                UpdateNowBtn.Content = Loc.T(_updates.CanSelfUpdate ? "update_now" : "update_open");
                UpdateMsg.Text = "";
                UpdateBarHost.Visibility = Visibility.Collapsed;
                UpdateNowBtn.IsEnabled = UpdateLaterBtn.IsEnabled = true;
                ShowOverlay(UpdateOverlay, UpdateCard, UpdateStack);
                FocusAfterEntrance(UpdateNowBtn);
                break;
            case UpdateState.UpToDate:
                if (manual) UpdateStatus.Text = Loc.F("update_uptodate", AppVersion);
                break;
            case UpdateState.Unavailable:
                if (manual) UpdateStatus.Text = Loc.T("update_dev");
                break;
            default:
                if (manual) UpdateStatus.Text = Loc.T("update_error");
                break;
        }
    }

    void UpdateLater_Click(object sender, RoutedEventArgs e) => HideOverlay(UpdateOverlay, UpdateCard);

    async void UpdateNow_Click(object sender, RoutedEventArgs e)
    {
        if (!_updates.CanSelfUpdate)
        {
            _updates.OpenReleasePage();                  // taşınabilir sürüm kendini güncelleyemez; sayfayı açar
            HideOverlay(UpdateOverlay, UpdateCard);
            return;
        }
        _updating = true;
        UpdateNowBtn.IsEnabled = UpdateLaterBtn.IsEnabled = false;
        UpdateBarHost.Visibility = Visibility.Visible;
        UpdateMsg.Text = Loc.F("update_downloading", 0);
        try
        {
            await _updates.DownloadAsync(p => Dispatcher.BeginInvoke(() =>
            {
                UpdateBarScale.ScaleX = Math.Clamp(p / 100.0, 0, 1);
                UpdateMsg.Text = Loc.F("update_downloading", p);
            }));
            UpdateMsg.Text = Loc.T("update_restarting");
            _ctl.Stop();
            ReadUi();
            _cfg.Save();
            _updates.ApplyAndRestart();      // kurulum sessizce başlar
            Application.Current.Shutdown();  // dosyalar serbest kalsın; kurulum bitince uygulama yeniden açılır
        }
        catch
        {
            UpdateMsg.Text = Loc.T("update_failed");
            UpdateNowBtn.IsEnabled = UpdateLaterBtn.IsEnabled = true;
            _updating = false;
        }
    }

    // ───────────────────────── Katman animasyonları ─────────────────────────

    readonly Dictionary<Border, int> _overlayVersion = new();

    /// <summary>Katmanı açar: arka plan belirir, kart hafifçe büyüyüp yükselir, içindekiler sırayla (≈45 ms arayla) gelir.</summary>
    void ShowOverlay(Border overlay, FrameworkElement card, Panel? stack = null)
    {
        _overlayVersion[overlay] = _overlayVersion.GetValueOrDefault(overlay) + 1;   // süren kapanış animasyonunu geçersiz kılar
        var animate = SystemParameters.ClientAreaAnimation;

        // Önceki (yarım kalmış ya da tutulan) animasyonları temizle. Temizlenmezse yazılar eski değerde takılır,
        // sonra başa sarıp yeniden gelir: art arda açınca "gidip gelen" yazıların nedeni buydu.
        overlay.BeginAnimation(OpacityProperty, null);
        if (card.RenderTransform is TransformGroup { Children.Count: 2 } old)
        {
            old.Children[0].BeginAnimation(ScaleTransform.ScaleXProperty, null);
            old.Children[0].BeginAnimation(ScaleTransform.ScaleYProperty, null);
            old.Children[1].BeginAnimation(TranslateTransform.YProperty, null);
        }
        card.RenderTransform = Transform.Identity;
        if (stack != null)
        {
            foreach (UIElement child in stack.Children)
            {
                child.BeginAnimation(OpacityProperty, null);
                if (child.RenderTransform is TranslateTransform shiftOld) shiftOld.BeginAnimation(TranslateTransform.YProperty, null);
                child.RenderTransform = Transform.Identity;
                child.Opacity = animate ? 0 : 1;           // animasyon başlayana dek görünmesin
            }
        }
        overlay.Opacity = animate ? 0 : 1;
        overlay.Visibility = Visibility.Visible;
        if (!animate) return;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        overlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170)) { EasingFunction = ease });

        var scale = new ScaleTransform(0.94, 0.94);
        var move = new TranslateTransform(0, 16);
        card.RenderTransformOrigin = new Point(0.5, 0.5);
        card.RenderTransform = new TransformGroup { Children = { scale, move } };
        var span = TimeSpan.FromMilliseconds(280);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.94, 1, span) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.94, 1, span) { EasingFunction = ease });
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(16, 0, span) { EasingFunction = ease });

        if (stack == null) return;
        var i = 0;
        foreach (UIElement child in stack.Children)
        {
            var shift = new TranslateTransform(0, 10);
            child.RenderTransform = shift;
            var delay = TimeSpan.FromMilliseconds(90 + i * 45);
            child.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240)) { BeginTime = delay, EasingFunction = ease });
            shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(280)) { BeginTime = delay, EasingFunction = ease });
            i++;
        }
    }

    /// <summary>Odak halkası (adorner) kayan öğeye yapışmasın diye, giriş animasyonu bitince odaklar.</summary>
    async void FocusAfterEntrance(UIElement target)
    {
        if (SystemParameters.ClientAreaAnimation) await Task.Delay(750);
        if (target.IsVisible) target.Focus();
    }

    /// <summary>Kapanış daha kısa ve hafif: kart 8 px aşağı kayarak solar.</summary>
    void HideOverlay(Border overlay, FrameworkElement card)
    {
        if (overlay.Visibility != Visibility.Visible) return;
        var version = _overlayVersion[overlay] = _overlayVersion.GetValueOrDefault(overlay) + 1;
        if (!SystemParameters.ClientAreaAnimation) { overlay.Visibility = Visibility.Collapsed; return; }

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(120));
        fade.Completed += (_, _) =>
        {
            if (_overlayVersion.GetValueOrDefault(overlay) != version) return;       // arada yeniden açıldıysa dokunma
            overlay.Visibility = Visibility.Collapsed;
            overlay.BeginAnimation(OpacityProperty, null);
            overlay.Opacity = 1;
        };
        overlay.BeginAnimation(OpacityProperty, fade);
        if (card.RenderTransform is TransformGroup { Children.Count: 2 } g && g.Children[1] is TranslateTransform move)
            move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, 8, TimeSpan.FromMilliseconds(120)));
    }

    // ───────────────────────── Kısa bildirim ─────────────────────────

    int _toastVersion;

    /// <summary>Alttan kayarak açılan bildirim; birkaç saniye sonra kendiliğinden kapanır.</summary>
    async void ShowToast(string text, int milliseconds = 2800)
    {
        var version = ++_toastVersion;
        var animate = SystemParameters.ClientAreaAnimation;
        ToastText.Text = text;

        Toast.BeginAnimation(OpacityProperty, null);
        var move = new TranslateTransform(0, animate ? 14 : 0);
        Toast.RenderTransform = move;
        Toast.Opacity = animate ? 0 : 1;
        Toast.Visibility = Visibility.Visible;
        if (animate)
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            Toast.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease });
            move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
        }

        await Task.Delay(milliseconds);
        if (version != _toastVersion) return;                  // araya yeni bildirim girdiyse ona dokunma

        if (!animate) { Toast.Visibility = Visibility.Collapsed; return; }
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
        fade.Completed += (_, _) =>
        {
            if (version == _toastVersion) Toast.Visibility = Visibility.Collapsed;
        };
        Toast.BeginAnimation(OpacityProperty, fade);
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, 10, TimeSpan.FromMilliseconds(220)));
    }

    // ───────────────────────── İlk açılış: kısayol ─────────────────────────

    void HotkeyDlgChange_Click(object sender, RoutedEventArgs e)
    {
        if (_capture != null) { EndCapture(); return; }
        BeginCapture(HotkeyDlgChangeBtn, "hotkey_prompt", (key, mods) =>
        {
            if (IsModifier(key)) return false;
            var vk = KeyInterop.VirtualKeyFromKey(key);
            if (vk == 0) return false;
            _cfg.HotkeyVk = vk;
            _cfg.HotkeyMods = (int)mods;
            return true;
        });
    }

    async void HotkeyOk_Click(object sender, RoutedEventArgs e)
    {
        if (_capture != null) EndCapture();
        _cfg.FirstRunDone = true;
        _cfg.Save();
        HideOverlay(HotkeyOverlay, HotkeyCard);

        if (_cfg.TourDone) return;
        await Task.Delay(SystemParameters.ClientAreaAnimation ? 450 : 0);
        StartTour();                       // yeni kullanıcıya tanıtım turu
    }

    // ───────────────────────── Tanıtım turu ─────────────────────────

    void Tour_Click(object sender, RoutedEventArgs e) => StartTour();

    void GoToPage(string tag)
    {
        switch (tag)
        {
            case "Keyboard": NavKeyboard.IsChecked = true; break;
            case "Profiles": NavProfiles.IsChecked = true; break;
            case "Settings": NavSettings.IsChecked = true; break;
            case "MouseTop":                       // üst çubuk ve alt çubuk adımları: sayfa başa dönsün
                NavMouse.IsChecked = true;
                PageMouse.ScrollToTop();
                break;
            default: NavMouse.IsChecked = true; break;
        }
    }

    void ScrollPagesToTop()
    {
        PageMouse.ScrollToTop();
        PageKeyboard.ScrollToTop();
        PageProfiles.ScrollToTop();
        PageSettings.ScrollToTop();
    }

    void StartTour()
    {
        if (Tour.IsActive) return;
        if (_ctl.IsActive) StopAll(finishedNaturally: false);
        _tourActive = true;
        ApplyShields();

        var steps = new List<TourStep>
        {
            new("Mouse", null, "tour_1_title", "tour_1_text"),
            new("Mouse", () => MouseHeader, "tour_2_title", "tour_2_text"),
            new("Mouse", () => MouseButtonsPanel, "tour_3_title", "tour_3_text"),
            new("Mouse", () => MouseTiming, "tour_4_title", "tour_4_text"),
            new("Mouse", () => PositionPanel, "tour_5_title", "tour_5_text"),
            new("Keyboard", () => KeysPanel, "tour_6_title", "tour_6_text"),
            new("Profiles", () => ProfilesPanel, "tour_7_title", "tour_7_text"),
            new("Settings", () => HotkeyPanel, "tour_8_title", "tour_8_text"),
            new("Settings", () => SoundsPanel, "tour_9_title", "tour_9_text"),
            new("MouseTop", () => ToggleBtn, "tour_10_title", "tour_10_text", 10),
            new("MouseTop", () => MiniBtn, "tour_11_title", "tour_11_text", 6),
            new("MouseTop", () => BrandBtn, "tour_12_title", "tour_12_text", 4),
        };
        Tour.Run(steps, GoToPage, completed =>
        {
            _tourActive = false;
            ApplyShields();
            _cfg.TourDone = true;
            _cfg.Save();
            NavMouse.IsChecked = true;
            ScrollPagesToTop();
            if (!completed) ShowToast(Loc.T("tour_skipped_toast"), 3200);    // atlayana, turu nereden açacağını hatırlat
        });
    }

    // ───────────────────────── Sıfırla ve kaldır ─────────────────────────

    void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (DateTime.Now >= _resetArmedUntil)
        {
            _resetArmedUntil = DateTime.Now.AddSeconds(3);   // ikinci tıklama onaylar
            return;
        }
        _resetArmedUntil = DateTime.MinValue;
        if (_ctl.IsActive) StopAll(finishedNaturally: false);

        var fresh = new Config();
        UnregisterHotkey();
        _cfg.Dark = fresh.Dark;
        _cfg.Accent = fresh.Accent;
        _cfg.HotkeyVk = fresh.HotkeyVk;
        _cfg.HotkeyMods = fresh.HotkeyMods;
        _cfg.ButtonDelaySec = fresh.ButtonDelaySec;
        _cfg.AutoUpdateCheck = fresh.AutoUpdateCheck;
        _cfg.Sounds = fresh.Sounds;
        _cfg.SoundVariant = fresh.SoundVariant;
        _cfg.SoundVolume = fresh.SoundVolume;
        _cfg.Mouse = fresh.Mouse;
        _cfg.Key = fresh.Key;
        RegisterHotkey();

        Theme.Apply(_cfg.Dark, _cfg.Accent);
        ApplyClickersToUi();
        ApplyAppToUi();
        _cfg.Save();
        MaintMsg.Text = Loc.T("maint_reset_done");
    }

    void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        if (!_updates.CanSelfUpdate)
        {
            MaintMsg.Text = Loc.T("maint_uninstall_portable");   // taşınabilir sürümde kaldırıcı yok
            return;
        }
        if (DateTime.Now >= _uninstallArmedUntil)
        {
            _uninstallArmedUntil = DateTime.Now.AddSeconds(3);
            return;
        }
        _uninstallArmedUntil = DateTime.MinValue;
        try
        {
            if (_ctl.IsActive) StopAll(finishedNaturally: false);
            ReadUi();
            _cfg.Save();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                System.IO.Path.Combine(AppContext.BaseDirectory, "unins000.exe")) { UseShellExecute = true });
            Application.Current.Shutdown();
        }
        catch
        {
            MaintMsg.Text = Loc.T("update_error");
        }
    }

    // ───────────────────────── Sesler ─────────────────────────

    void Sound_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        ReadUi();
        _cfg.Save();
        Sounds.PlayStartAlways();       // seçince hemen duyulsun
    }

    void SoundPreview_Click(object sender, RoutedEventArgs e)
    {
        ReadUi();
        Sounds.Preview();
    }

    void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SoundVolumeText != null) SoundVolumeText.Text = $"{(int)e.NewValue}%";
    }

    void Volume_Released(object sender, MouseButtonEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            ReadUi();
            _cfg.Save();
            Sounds.PlayStartAlways();   // bırakınca yeni düzeyi duy
        }, DispatcherPriority.Input);
    }

    // ───────────────────────── Etkinleştirme kalkanı ─────────────────────────

    void Enable_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        if (_recording && ChkKey.IsChecked != true) StopRecording();   // klavye kapanınca kayıt da biter
        ApplyShields();   // zamanlayıcıyı beklemeden anında tepki
    }

    void ApplyShields()
    {
        if (_recording && ChkKey.IsChecked != true) StopRecording();   // mini pencereden kapatılırsa da
        SetShield(MouseShield, MouseBody, ChkMouse.IsChecked == true);
        SetShield(KeyShield, KeyBody, ChkKey.IsChecked == true);
    }

    /// <summary>Kapalı bölümü, tüm kontrolleri tek tek devre dışı bırakmak yerine tek bir yarı saydam katmanla soluklaştırır.</summary>
    void SetShield(Border shield, UIElement body, bool enabled)
    {
        var blocked = !enabled && !_tourActive;   // tur sürerken kapalı bölümler de net görünsün
        if (shield.IsHitTestVisible == blocked && _shieldInit.Contains(shield)) return;
        _shieldInit.Add(shield);
        shield.IsHitTestVisible = blocked;
        KeyboardNavigation.SetTabNavigation(body, blocked ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);

        var to = blocked ? 0.55 : 0.0;
        if (!_ready || !SystemParameters.ClientAreaAnimation)
        {
            shield.BeginAnimation(OpacityProperty, null);
            shield.Opacity = to;
            return;
        }
        var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(blocked ? 140 : 90))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        shield.BeginAnimation(OpacityProperty, anim);   // yeni animasyon eskisini yarıda keser; kesintisiz
    }

    readonly HashSet<Border> _shieldInit = new();

    // ───────────────────────── Sayfa geçişi ─────────────────────────

    void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        ShowPage((string)((RadioButton)sender).Tag);
    }

    void ShowPage(string tag)
    {
        FrameworkElement[] pages = { PageMouse, PageKeyboard, PageProfiles, PageSettings };
        var target = tag switch { "Keyboard" => PageKeyboard, "Profiles" => PageProfiles, "Settings" => PageSettings, _ => PageMouse };
        foreach (var p in pages) p.Visibility = p == target ? Visibility.Visible : Visibility.Collapsed;

        if (!SystemParameters.ClientAreaAnimation) return;
        var tt = new TranslateTransform(-12, 0);
        target.RenderTransform = tt;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        tt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-12, 0, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease });
        target.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease });
    }

    // ───────────────────────── Konum seçme ─────────────────────────

    async void PickPos_Click(object sender, RoutedEventArgs e)
    {
        PickBtn.IsEnabled = false;
        var prev = WindowState;
        WindowState = WindowState.Minimized;
        await Task.Delay(350);

        var overlay = new PickOverlay();
        overlay.ShowDialog();

        WindowState = prev == WindowState.Minimized ? WindowState.Normal : prev;
        Activate();
        if (overlay.Result is { } p)
        {
            TbX.Text = p.X.ToString();
            TbY.Text = p.Y.ToString();
            PosFixed.IsChecked = true;
        }
        PickBtn.IsEnabled = true;
    }

    // ───────────────────────── Tuş yakalama ve kayıt ─────────────────────────

    void BeginCapture(Button btn, string promptKey, Func<Key, ModifierKeys, bool> onKey)
    {
        EndCapture();
        UnregisterHotkey();
        _capture = onKey;
        _captureBtn = btn;
        _capturePrompt = promptKey;
        Keyboard.Focus(btn);
        Refresh();
    }

    void EndCapture()
    {
        if (_capture == null) return;
        _capture = null;
        _captureBtn = null;
        RegisterHotkey();
        Refresh();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (Tour.IsActive && Tour.HandleKey(e.Key)) { e.Handled = true; return; }
        if (e.Key == Key.Escape && !_updating && _capture == null)
        {
            if (UpdateOverlay.Visibility == Visibility.Visible) { HideOverlay(UpdateOverlay, UpdateCard); e.Handled = true; return; }
            if (AboutOverlay.Visibility == Visibility.Visible) { HideOverlay(AboutOverlay, AboutCard); e.Handled = true; return; }
        }
        if (_capture == null && !_recording) { base.OnPreviewKeyDown(e); return; }

        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            Key.DeadCharProcessed => e.DeadCharProcessedKey,
            _ => e.Key
        };
        e.Handled = true;
        if (key == Key.None) return;

        if (_recording)
        {
            if (e.IsRepeat) return;
            if (key == Key.Escape) { StopRecording(); return; }
            var vk = KeyInterop.VirtualKeyFromKey(key);
            if (vk == 0 || vk == _cfg.HotkeyVk) return; // kısayol tuşu diziye girmez
            AddKeyItem(vk);
            return;
        }

        if (key == Key.Escape) { EndCapture(); return; }
        if (_capture!(key, Keyboard.Modifiers)) EndCapture();
    }

    static bool IsModifier(Key k) => k is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
        or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

    void AddKeyItem(int vk)
    {
        _keyItems.Add(new KeyItem { Vk = vk });
        SyncKeys();
    }

    void SyncKeys()
    {
        Reindex();
        _cfg.Key.Keys = _keyItems.Select(k => k.Vk).ToList();
    }

    void Reindex()
    {
        for (var i = 0; i < _keyItems.Count; i++) _keyItems[i].Index = i + 1;
    }

    void AddKey_Click(object sender, RoutedEventArgs e)
    {
        if (_capture != null) { EndCapture(); return; }
        BeginCapture(AddKeyBtn, "add_key_prompt", (key, _) =>
        {
            var vk = KeyInterop.VirtualKeyFromKey(key);
            if (vk == 0) return false;
            AddKeyItem(vk);
            return true;
        });
    }

    void Record_Click(object sender, RoutedEventArgs e)
    {
        if (_recording) StopRecording(); else StartRecording();
    }

    void StartRecording()
    {
        if (_ctl.IsActive) StopAll(finishedNaturally: false);
        EndCapture();
        _recording = true;
        Keyboard.Focus(RecordBtn);
        Refresh();
    }

    void StopRecording()
    {
        _recording = false;
        SyncKeys();
        Refresh();
    }

    void UndoKey_Click(object sender, RoutedEventArgs e)
    {
        if (_keyItems.Count == 0) return;
        _keyItems.RemoveAt(_keyItems.Count - 1);
        SyncKeys();
    }

    void ClearKeys_Click(object sender, RoutedEventArgs e)
    {
        _keyItems.Clear();
        SyncKeys();
    }

    void KeyLeft_Click(object sender, RoutedEventArgs e) => MoveKey(sender, -1);
    void KeyRight_Click(object sender, RoutedEventArgs e) => MoveKey(sender, +1);

    void MoveKey(object sender, int delta)
    {
        if (((FrameworkElement)sender).DataContext is not KeyItem item) return;
        var i = _keyItems.IndexOf(item);
        var j = i + delta;
        if (i < 0 || j < 0 || j >= _keyItems.Count) return;
        _keyItems.Move(i, j);
        SyncKeys();
    }

    void KeyRemove_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not KeyItem item) return;
        _keyItems.Remove(item);
        SyncKeys();
    }

    void HotkeyChange_Click(object sender, RoutedEventArgs e)
    {
        if (_capture != null) { EndCapture(); return; }
        BeginCapture(HotkeyBtn, "hotkey_prompt", (key, mods) =>
        {
            if (IsModifier(key)) return false; // Ctrl/Alt/Shift ile birlikte basılmasını bekle
            var vk = KeyInterop.VirtualKeyFromKey(key);
            if (vk == 0) return false;
            _cfg.HotkeyVk = vk;
            _cfg.HotkeyMods = (int)mods;
            return true;
        });
    }

    // ───────────────────────── Profiller ─────────────────────────

    void RebuildProfiles()
    {
        _rows.Clear();
        foreach (var p in _cfg.Profiles) _rows.Add(new ProfileRow(p));
    }

    const int MaxProfileName = 16;
    static string ClipName(string s) { s = s.Trim(); return s.Length > MaxProfileName ? s[..MaxProfileName] : s; }

    static ProfileRow? RowOf(object sender) => (sender as FrameworkElement)?.DataContext as ProfileRow;

    bool NameTaken(string name, Profile? except = null) =>
        _cfg.Profiles.Any(p => p != except && string.Equals(p.Name, name, StringComparison.CurrentCultureIgnoreCase));

    void ProfileSaveNew_Click(object sender, RoutedEventArgs e)
    {
        var name = ClipName(TbProfile.Text);
        if (name.Length == 0) { ProfileMsg.Text = Loc.T("profile_need_name"); return; }
        if (NameTaken(name)) { ProfileMsg.Text = Loc.T("profile_exists"); return; }
        ReadUi();
        _cfg.Profiles.Add(new Profile { Name = name, Mouse = Config.Clone(_cfg.Mouse), Key = Config.Clone(_cfg.Key) });
        _cfg.Save();
        RebuildProfiles();
        TbProfile.Text = "";
        ProfileMsg.Text = Loc.F("profile_saved", name);
    }

    void TbProfile_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        ProfileSaveNew_Click(sender, e);
    }

    void ProfileLoad_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        _cfg.Mouse = Config.Clone(row.Profile.Mouse);
        _cfg.Key = Config.Clone(row.Profile.Key);
        ApplyClickersToUi();
        _cfg.Save();
        ProfileMsg.Text = Loc.F("profile_loaded", row.Name);
    }

    void ProfileUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        ReadUi();
        row.Profile.Mouse = Config.Clone(_cfg.Mouse);
        row.Profile.Key = Config.Clone(_cfg.Key);
        _cfg.Save();
        ProfileMsg.Text = Loc.F("profile_saved", row.Name);
    }

    void ProfileRename_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        foreach (var r in _rows) r.IsEditing = false;
        row.EditText = row.Name;
        row.IsEditing = true;
    }

    void ProfileEditOk_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row) CommitRename(row);
    }

    void ProfileEditCancel_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row) row.IsEditing = false;
    }

    void EditBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        if (e.Key == Key.Enter) { e.Handled = true; CommitRename(row); }
        else if (e.Key == Key.Escape) { e.Handled = true; row.IsEditing = false; }
    }

    void EditBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && sender is TextBox tb)
            Dispatcher.BeginInvoke(() => { tb.Focus(); tb.SelectAll(); }, DispatcherPriority.Input);
    }

    void CommitRename(ProfileRow row)
    {
        var name = ClipName(row.EditText);
        if (name.Length == 0) { ProfileMsg.Text = Loc.T("profile_need_name"); return; }
        if (NameTaken(name, row.Profile)) { ProfileMsg.Text = Loc.T("profile_exists"); return; }
        row.Profile.Name = name;
        row.Refresh();
        row.IsEditing = false;
        _cfg.Save();
        ProfileMsg.Text = Loc.F("profile_renamed", name);
    }

    void ProfileDelete_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        if (!row.DeleteArmed)
        {
            if (_armedRow != null) _armedRow.DeleteArmed = false;
            row.DeleteArmed = true;                 // ikinci tıklama silmeyi onaylar
            _armedRow = row;
            _armedUntil = DateTime.Now.AddSeconds(3);
            return;
        }
        _armedRow = null;
        _cfg.Profiles.Remove(row.Profile);
        _rows.Remove(row);
        _cfg.Save();
        ProfileMsg.Text = Loc.F("profile_deleted", row.Name);
    }

    // ───────────────────────── Görünüm ve dil ─────────────────────────

    void BuildSwatches()
    {
        foreach (var (name, hex) in Theme.Presets)
        {
            Theme.TryParse(hex, out var c);
            var rb = new RadioButton
            {
                Style = (Style)FindResource("Swatch"),
                GroupName = "Accent",
                Tag = hex,
                Background = new SolidColorBrush(c),
                ToolTip = name,
            };
            System.Windows.Automation.AutomationProperties.SetName(rb, name);
            rb.Checked += (_, _) =>
            {
                if (_ready) SetAccent(hex);
            };
            SwatchHost.Children.Add(rb);
        }
    }

    void SyncSwatches()
    {
        foreach (RadioButton rb in SwatchHost.Children)
            rb.IsChecked = string.Equals((string)rb.Tag, _cfg.Accent, StringComparison.OrdinalIgnoreCase);
    }

    void SetAccent(string hex)
    {
        _cfg.Accent = hex.ToUpperInvariant();
        TbHex.Text = _cfg.Accent;
        Theme.Apply(_cfg.Dark, _cfg.Accent);
        _cfg.Save();
        SyncWheel();
    }

    // ── renk çarkı ──

    bool _syncingWheel;
    long _lastWheelApply;

    /// <summary>Çark ve parlaklık kaydırıcısını kayıtlı vurgu rengine eşler (olay tetiklemeden).</summary>
    void SyncWheel()
    {
        if (!Theme.TryParse(_cfg.Accent, out var c)) return;
        _syncingWheel = true;
        AccentWheel.SetColor(c);
        AccentBright.Value = Math.Round(AccentWheel.Value * 100);
        _syncingWheel = false;
    }

    void Wheel_ColorChanged(Color c, bool final)
    {
        if (_syncingWheel || !_ready) return;
        var hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        _cfg.Accent = hex;
        TbHex.Text = hex;

        // sürüklerken tüm arayüzü her pikselde yeniden boyamamak için sınırla
        var now = Environment.TickCount64;
        if (final || now - _lastWheelApply >= 45)
        {
            _lastWheelApply = now;
            Theme.Apply(_cfg.Dark, hex);
        }
        if (final)
        {
            _cfg.Save();
            SyncSwatches();
        }
    }

    void Bright_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncingWheel || !_ready) return;
        AccentWheel.Value = e.NewValue / 100;
        Wheel_ColorChanged(AccentWheel.Color, final: false);
    }

    void Bright_Released(object sender, MouseButtonEventArgs e)
    {
        Dispatcher.BeginInvoke(() => Wheel_ColorChanged(AccentWheel.Color, final: true), DispatcherPriority.Input);
    }

    void Theme_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _cfg.Dark = ThemeDark.IsChecked == true;
        Theme.Apply(_cfg.Dark, _cfg.Accent);
        _cfg.Save();
    }

    void Lang_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _cfg.Lang = LangEn.IsChecked == true ? "en" : "tr";
        Loc.Set(_cfg.Lang);
        HexNote.Text = Loc.T("hex_note");
        _cfg.Save();
        Refresh();
    }

    void HexApply_Click(object sender, RoutedEventArgs e)
    {
        var text = TbHex.Text.Trim();
        if (!text.StartsWith('#')) text = "#" + text;
        if (!Theme.TryParse(text, out _))
        {
            HexNote.Text = Loc.T("hex_invalid");
            return;
        }
        HexNote.Text = Loc.T("hex_note");
        SetAccent(text);
        SyncSwatches();
    }
}

