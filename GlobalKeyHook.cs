using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AtemKey;

/// <summary>
/// Low-level keyboard hook so numpad keys work even when another app has focus.
/// The handler returns true to swallow the key.
/// </summary>
sealed class GlobalKeyHook : IDisposable
{
    const int WH_KEYBOARD_LL = 13;
    const int WM_KEYDOWN = 0x0100, WM_SYSKEYDOWN = 0x0104;
    const int WM_KEYUP = 0x0101, WM_SYSKEYUP = 0x0105;
    const uint LLKHF_EXTENDED = 0x01, LLKHF_INJECTED = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

    delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string? lpModuleName);

    readonly HookProc _proc; // keep delegate alive
    readonly Func<Keys, bool, bool> _onKeyDown; // (key, isExtended) -> swallow?
    readonly HashSet<uint> _swallowedDown = new();
    IntPtr _hook;

    public GlobalKeyHook(Func<Keys, bool, bool> onKeyDown)
    {
        _onKeyDown = onKeyDown;
        _proc = Callback;
        using var mod = Process.GetCurrentProcess().MainModule!;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(mod.ModuleName), 0);
    }

    IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var k = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int msg = (int)wParam;
            if ((k.flags & LLKHF_INJECTED) == 0)
            {
                if (msg is WM_KEYDOWN or WM_SYSKEYDOWN)
                {
                    if (_onKeyDown((Keys)k.vkCode, (k.flags & LLKHF_EXTENDED) != 0))
                    {
                        _swallowedDown.Add(k.vkCode);
                        return 1;
                    }
                }
                else if (msg is WM_KEYUP or WM_SYSKEYUP && _swallowedDown.Remove(k.vkCode))
                {
                    return 1;
                }
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
    }
}
