using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FoxAutoClicker;

/// <summary>Win32 SendInput sarmalayıcısı.</summary>
public static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    struct INPUT { public uint type; public InputUnion U; }

    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint mapType);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int w, int h);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);

    /// <summary>Sanal ekranın (tüm monitörler) o anki görüntüsünü, donmuş bir bitmap olarak alır.</summary>
    public static System.Windows.Media.Imaging.BitmapSource? CaptureScreen(int x, int y, int w, int h)
    {
        var screen = GetDC(IntPtr.Zero);
        var mem = CreateCompatibleDC(screen);
        var bmp = CreateCompatibleBitmap(screen, w, h);
        var old = SelectObject(mem, bmp);
        try
        {
            if (!BitBlt(mem, 0, 0, w, h, screen, x, y, 0x00CC0020)) return null; // SRCCOPY
            var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                bmp, IntPtr.Zero, System.Windows.Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        finally
        {
            SelectObject(mem, old);
            DeleteObject(bmp);
            DeleteDC(mem);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint ms);
    [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint ms);

    const uint InputMouse = 0, InputKeyboard = 1;
    const uint KeyExtended = 0x1, KeyUp = 0x2, KeyScan = 0x8;

    static readonly (uint Down, uint Up)[] MouseFlags =
    {
        (0x0002, 0x0004), // sol
        (0x0020, 0x0040), // orta
        (0x0008, 0x0010), // sağ
    };

    public static void MouseClick(int button, int times)
    {
        var (down, up) = MouseFlags[Math.Clamp(button, 0, 2)];
        var list = new List<INPUT>();
        for (var i = 0; i < times; i++)
        {
            list.Add(Mouse(down));
            list.Add(Mouse(up));
        }
        SendInput((uint)list.Count, list.ToArray(), Marshal.SizeOf<INPUT>());
    }

    public static void MouseButton(int button, bool down)
    {
        var (d, u) = MouseFlags[Math.Clamp(button, 0, 2)];
        SendInput(1, new[] { Mouse(down ? d : u) }, Marshal.SizeOf<INPUT>());
    }

    static INPUT Mouse(uint flags) => new() { type = InputMouse, U = new InputUnion { mi = new MOUSEINPUT { dwFlags = flags } } };

    static bool IsExtended(int vk) => vk is 0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28
        or 0x2C or 0x2D or 0x2E or 0x5B or 0x5C or 0x5D or 0x6F or 0x90 or 0xA3 or 0xA5;

    public static void Key(int vk, bool down)
    {
        var scan = (ushort)MapVirtualKey((uint)vk, 0);
        uint flags = down ? 0u : KeyUp;
        if (IsExtended(vk)) flags |= KeyExtended;
        var ki = scan != 0
            ? new KEYBDINPUT { wVk = 0, wScan = scan, dwFlags = flags | KeyScan }
            : new KEYBDINPUT { wVk = (ushort)vk, dwFlags = flags };
        var input = new INPUT { type = InputKeyboard, U = new InputUnion { ki = ki } };
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }
}

/// <summary>Kendi iş parçacığında, hassas zamanlamayla bir eylemi tekrarlar.</summary>
public sealed class Runner
{
    readonly Action _tick;
    readonly Func<double> _nextDelay;
    readonly long _max;
    readonly bool _holdAfterFirst;
    CancellationTokenSource? _cts;
    Task? _task;
    long _count;

    public Runner(Action tick, Func<double> nextDelay, long max, bool holdAfterFirst = false)
    {
        _tick = tick; _nextDelay = nextDelay; _max = max; _holdAfterFirst = holdAfterFirst;
    }

    public long Count => Interlocked.Read(ref _count);
    public event Action? Completed;

    public void Start(int initialDelayMs)
    {
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _task = Task.Factory.StartNew(() => Loop(ct, initialDelayMs), TaskCreationOptions.LongRunning);
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _task?.Wait(1500); } catch { /* iptal */ }
    }

    void Loop(CancellationToken ct, int initialDelayMs)
    {
        var completed = false;
        Native.timeBeginPeriod(1);
        try
        {
            var sw = Stopwatch.StartNew();
            double next = initialDelayMs;
            while (!ct.IsCancellationRequested)
            {
                if (!WaitUntil(sw, next, ct)) break;
                _tick();
                Interlocked.Increment(ref _count);

                if (_holdAfterFirst) { ct.WaitHandle.WaitOne(); break; }
                if (_max > 0 && Count >= _max) { completed = true; break; }

                var now = sw.Elapsed.TotalMilliseconds;
                next += _nextDelay();
                if (next < now - 50) next = now; // takılma sonrası yeniden eşitle
            }
        }
        finally { Native.timeEndPeriod(1); }
        if (completed) Completed?.Invoke();
    }

    static bool WaitUntil(Stopwatch sw, double target, CancellationToken ct)
    {
        while (true)
        {
            var wait = target - sw.Elapsed.TotalMilliseconds;
            if (wait <= 0) return !ct.IsCancellationRequested;
            if (wait > 2)
            {
                if (ct.WaitHandle.WaitOne((int)Math.Min(wait - 1, 1_000_000))) return false;
            }
            else
            {
                if (ct.IsCancellationRequested) return false;
                Thread.SpinWait(40);
            }
        }
    }
}

public sealed class ClickController
{
    Runner? _mouse, _key;
    readonly Stopwatch _sw = new();
    int _delayMs;
    int _active;
    List<int> _held = new();
    int _heldMouse = -1;

    public bool IsActive { get; private set; }
    public bool IsWaiting => IsActive && _sw.ElapsedMilliseconds < _delayMs;
    public int RemainingMs => IsWaiting ? (int)(_delayMs - _sw.ElapsedMilliseconds) : 0;
    public TimeSpan Elapsed => TimeSpan.FromMilliseconds(Math.Max(0, _sw.ElapsedMilliseconds - _delayMs));
    public long MouseCount => _mouse?.Count ?? 0;
    public long KeyCount => _key?.Count ?? 0;

    /// <summary>Tüm sınırlı işler bitince çağrılır (arka plan iş parçacığından).</summary>
    public event Action? Finished;

    public void Start(Config cfg, int delaySec)
    {
        if (IsActive) return;
        _delayMs = Math.Max(0, delaySec) * 1000;
        _active = 0;
        _mouse = null; _key = null; _held = new(); _heldMouse = -1;

        if (cfg.Mouse.Enabled)
        {
            var m = cfg.Mouse; var t = m.Timing;
            var clicks = m.Double ? 2 : 1;
            if (m.Hold)
            {
                _heldMouse = m.Button;
                _mouse = new Runner(() =>
                {
                    if (m.Fixed) Native.SetCursorPos(m.X, m.Y);
                    Native.MouseButton(m.Button, true);
                }, () => 1000, 1, holdAfterFirst: true);
            }
            else
            {
                _mouse = new Runner(() =>
                {
                    if (m.Fixed) Native.SetCursorPos(m.X, m.Y);
                    Native.MouseClick(m.Button, clicks);
                }, t.NextDelay, t.Limit ? Math.Max(1, t.Count) : 0);
                _mouse.Completed += OnRunnerDone;
                _active++;
            }
        }

        if (cfg.Key.Enabled && cfg.Key.Keys.Count > 0)
        {
            var k = cfg.Key; var t = k.Timing;
            var keys = k.Keys.ToArray();
            if (k.Hold)
            {
                _held = keys.ToList();
                _key = new Runner(() => { foreach (var vk in keys) Native.Key(vk, true); },
                                  () => 1000, 1, holdAfterFirst: true);
            }
            else
            {
                _key = new Runner(() =>
                {
                    for (var i = 0; i < keys.Length; i++)
                    {
                        Native.Key(keys[i], true);
                        Thread.Sleep(12);
                        Native.Key(keys[i], false);
                        if (i < keys.Length - 1) Thread.Sleep(12);
                    }
                }, t.NextDelay, t.Limit ? Math.Max(1, t.Count) : 0);
                _key.Completed += OnRunnerDone;
                _active++;
            }
        }

        IsActive = true;
        _sw.Restart();
        _mouse?.Start(_delayMs);
        _key?.Start(_delayMs);
    }

    void OnRunnerDone()
    {
        if (Interlocked.Decrement(ref _active) == 0) Finished?.Invoke();
    }

    public void Stop()
    {
        if (!IsActive) return;
        IsActive = false;
        _mouse?.Stop();
        _key?.Stop();
        foreach (var vk in _held) Native.Key(vk, false);
        _held = new();
        if (_heldMouse >= 0) Native.MouseButton(_heldMouse, false);
        _heldMouse = -1;
        _sw.Stop();
    }
}
