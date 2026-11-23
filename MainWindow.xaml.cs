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
        Sounds.Enabled = _cfg.Sounds;
        Refresh();

        Loaded += async (_, _) =>
        {
            if (!_cfg.AutoUpdateCheck) return;
            await Task.Delay(1200);          // açılış sakinleşsin
            await CheckUpdatesAsync(manual: false);
        };
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
        if (_capture != null) return;

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
        (m.Button switch { 1 => MbMiddle, 2 => MbRight, _ => MbLeft }).IsChecked = true;
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
        TbDelay.Text = _cfg.ButtonDelaySec.ToString();
        HotkeyChip.Text = HotkeyText;
        ChkAutoUpdate.IsChecked = _cfg.AutoUpdateCheck;
        ChkSounds.IsChecked = _cfg.Sounds;
    }

    void ReadUi()
    {
        var m = _cfg.Mouse;
        m.Enabled = ChkMouse.IsChecked == true;
        m.Button = MbMiddle.IsChecked == true ? 1 : MbRight.IsChecked == true ? 2 : 0;
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
        Sounds.Enabled = _cfg.Sounds;
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
        HotkeyChip.Text = HotkeyText;

        // profiller
        ProfilesEmpty.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_armedRow != null && DateTime.Now >= _armedUntil) { _armedRow.DeleteArmed = false; _armedRow = null; }

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
        AboutOverlay.Visibility = Visibility.Visible;
        AboutCloseBtn.Focus();
    }

    void AboutOverlay_MouseDown(object sender, MouseButtonEventArgs e) => AboutOverlay.Visibility = Visibility.Collapsed;
    void Card_MouseDown(object sender, MouseButtonEventArgs e) => e.Handled = true;   // kart içi tık katmanı kapatmasın
    void AboutClose_Click(object sender, RoutedEventArgs e) => AboutOverlay.Visibility = Visibility.Collapsed;

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
                UpdateText.Text = Loc.F("update_text", result.Version!);
                UpdateMsg.Text = "";
                UpdateBarHost.Visibility = Visibility.Collapsed;
                UpdateNowBtn.IsEnabled = UpdateLaterBtn.IsEnabled = true;
                UpdateOverlay.Visibility = Visibility.Visible;
                UpdateNowBtn.Focus();
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

    void UpdateLater_Click(object sender, RoutedEventArgs e) => UpdateOverlay.Visibility = Visibility.Collapsed;

    async void UpdateNow_Click(object sender, RoutedEventArgs e)
    {
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
            _updates.ApplyAndRestart();      // uygulamayı kapatır, güncelleyip yeniden açar
        }
        catch
        {
            UpdateMsg.Text = Loc.T("update_failed");
            UpdateNowBtn.IsEnabled = UpdateLaterBtn.IsEnabled = true;
            _updating = false;
        }
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
        var blocked = !enabled;
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
        if (e.Key == Key.Escape && !_updating)
        {
            if (UpdateOverlay.Visibility == Visibility.Visible) { UpdateOverlay.Visibility = Visibility.Collapsed; e.Handled = true; return; }
            if (AboutOverlay.Visibility == Visibility.Visible) { AboutOverlay.Visibility = Visibility.Collapsed; e.Handled = true; return; }
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

