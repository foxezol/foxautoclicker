using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FoxAutoClicker;

/// <summary>
/// Başlat/durdur kısayolu olarak fare yan düğmelerini (XButton1/XButton2) dinler.
/// RegisterHotKey fare düğmelerini desteklemediği için düşük seviyeli fare kancası (WH_MOUSE_LL) kullanılır.
/// Seçilen düğmenin kendi işlevi (tarayıcıda geri/ileri) engellenir; yazılımla gönderilen basışlar (kendi tıklamalarımız) engellenmez.
/// </summary>
public sealed class MouseHotkeyHook : IDisposable
{
    const int WhMouseLl = 14;
    const int WmXButtonDown = 0x020B, WmXButtonUp = 0x020C;

    delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    struct MsLlHookStruct { public int X, Y; public uint MouseData, Flags, Time; public IntPtr Extra; }

    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr module, uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string? name);

    readonly HookProc _proc;
    IntPtr _hook;

    // Deneme anahtarı: otomasyon testlerinde yazılımla gönderilen basışlar da kabul edilir
    static readonly bool AcceptInjected = Environment.GetEnvironmentVariable("FOXCLICKER_ACCEPT_INJECTED") == "1";

    /// <summary>1 = XButton1, 2 = XButton2, 0 = kapalı.</summary>
    public int Target { get; private set; }

    /// <summary>Seçili düğme şu an basılı mı? (basılı tutma modu için)</summary>
    public bool IsDown { get; private set; }

    public event Action? Pressed;

    public MouseHotkeyHook() { _proc = Callback; }

    /// <summary>Sanal tuş kodu verilir: 5 = XButton1, 6 = XButton2; başka değer kancayı kapatır.</summary>
    public void SetTarget(int vk)
    {
        Target = vk == 5 ? 1 : vk == 6 ? 2 : 0;
        IsDown = false;
        if (Target != 0 && _hook == IntPtr.Zero)
            _hook = SetWindowsHookEx(WhMouseLl, _proc, GetModuleHandle(null), 0);
        else if (Target == 0 && _hook != IntPtr.Zero)
            Uninstall();
    }

    IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && Target != 0)
        {
            var message = wParam.ToInt32();
            if (message is WmXButtonDown or WmXButtonUp)
            {
                var data = Marshal.PtrToStructure<MsLlHookStruct>(lParam);
                var injected = (data.Flags & 0x3) != 0;                   // LLMHF_INJECTED | LLMHF_LOWER_IL_INJECTED
                var button = (int)((data.MouseData >> 16) & 0xFFFF);
                if ((AcceptInjected || !injected) && button == Target)
                {
                    if (message == WmXButtonDown)
                    {
                        if (!IsDown) { IsDown = true; Pressed?.Invoke(); }
                        return (IntPtr)1;                                 // düğmenin normal işlevini engelle
                    }

                    // Bırakma olayını yalnızca basışını biz yuttuysak yut. Aksi halde (ör. kısayolu seçerken basış sisteme
                    // gitmişti) bırakma da gitmeli; yoksa sistem düğmeyi sonsuza dek basılı sanar ve diğer uygulamalara
                    // tıklanamaz olur.
                    if (IsDown)
                    {
                        IsDown = false;
                        return (IntPtr)1;
                    }
                }
            }
        }
        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    void Uninstall()
    {
        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    public void Dispose()
    {
        Target = 0;
        if (_hook != IntPtr.Zero) Uninstall();
    }
}
