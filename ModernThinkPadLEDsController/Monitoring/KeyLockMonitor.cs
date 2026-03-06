using System.Runtime.InteropServices;
using System.Windows.Input;

namespace ModernThinkPadLEDsController.Monitoring;

// KeyLockMonitor installs a low-level keyboard hook that fires immediately
// when Caps Lock or Num Lock is pressed — no polling, no delay.
//
// In the legacy app this needed a polling fallback because the hook only
// worked for processes at the same privilege level. Since we always run as
// Administrator (requireAdministrator manifest), the hook sees ALL keystrokes
// from every process on the system. The polling loop and the CapsLockDelay
// setting are gone entirely.
public sealed class KeyLockMonitor : IDisposable
{
    // Events fire on the hook's thread (which is the UI message-pump thread).
    // bool parameter = new state AFTER the keypress (true = key will be ON).
    public event Action<bool>? CapsLockChanged;
    public event Action<bool>? NumLockChanged;

    // WH_KEYBOARD_LL = low-level keyboard hook type (13).
    // WM_KEYDOWN     = message sent when a non-system key is pressed (0x100).
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int VK_CAPITAL = 0x14;  // virtual key code for Caps Lock
    private const int VK_NUMLOCK = 0x90;  // virtual key code for Num Lock

    // We store the delegate in a field so the GC cannot collect it while
    // the hook is active. If it were a local variable, the GC might free it
    // and the hook would call freed memory — a crash.
    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId = IntPtr.Zero;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    public KeyLockMonitor() => _proc = HookCallback;

    public void Start()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(module.ModuleName), 0);
    }

    public void Stop()
    {
        if (_hookId == IntPtr.Zero) return;
        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    // Call once at startup so the LEDs reflect the actual key state immediately,
    // not only after the first keypress.
    public void SyncInitialState()
    {
        CapsLockChanged?.Invoke(Keyboard.IsKeyToggled(Key.CapsLock));
        NumLockChanged?.Invoke(Keyboard.IsKeyToggled(Key.NumLock));
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            int vkCode = Marshal.ReadInt32(lParam);

            if (vkCode == VK_CAPITAL)
            {
                // WM_KEYDOWN fires BEFORE the OS toggles the key state.
                // IsKeyToggled still returns the OLD state here, so we invert it
                // to get the state it WILL be after this keypress completes.
                bool willBeOn = !Keyboard.IsKeyToggled(Key.CapsLock);
                CapsLockChanged?.Invoke(willBeOn);
            }
            else if (vkCode == VK_NUMLOCK)
            {
                bool willBeOn = !Keyboard.IsKeyToggled(Key.NumLock);
                NumLockChanged?.Invoke(willBeOn);
            }
        }

        // Always call the next hook — never swallow the keystroke.
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose() => Stop();
}
