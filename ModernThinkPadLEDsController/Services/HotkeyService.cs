using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ModernThinkPadLEDsController.Services;

/// <summary>
/// Registers <kbd>Win</+Shift+K</kbd> as a system-wide hotkey and raises <see cref="HotkeyPressed"/>
/// each time the user presses the combination.
///
/// Uses the Win32 RegisterHotKey / UnregisterHotKey API wired through an HwndSource hook
/// on the main window — the standard WPF approach for global hotkeys that does not require
/// a separate background thread or a keyboard hook.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    // Win32 modifier flags
    private const int MOD_SHIFT = 0x0004;
    private const int MOD_WIN   = 0x0008;

    // Virtual-key code for K
    private const int VK_K = 0x4B;

    // Arbitrary unique hotkey id — must not clash with other RegisterHotKey calls in the process.
    private const int HotkeyId = 0x3A9C;

    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>Raised on the UI thread each time Win+Shift+K is pressed.</summary>
    public event Action? HotkeyPressed;

    private HwndSource? _source;
    private IntPtr _hwnd = IntPtr.Zero;

    /// <summary>
    /// Registers the hotkey against <paramref name="window"/>'s HWND.
    /// Call this after <c>SourceInitialized</c> so the HWND exists.
    /// </summary>
    public void Register(Window window)
    {
        _hwnd   = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
        RegisterHotKey(_hwnd, HotkeyId, MOD_WIN | MOD_SHIFT, VK_K);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero)
            UnregisterHotKey(_hwnd, HotkeyId);

        _source?.RemoveHook(WndProc);
    }
}
