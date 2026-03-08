using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ModernThinkPadLEDsController.Services;

/// <summary>
/// Registers a configurable system-wide hotkey and raises <see cref="HotkeyPressed"/>
/// each time the user presses the combination.
///
/// Uses the Win32 RegisterHotKey / UnregisterHotKey API wired through an HwndSource hook
/// on the main window — the standard WPF approach for global hotkeys that does not require
/// a separate background thread or a keyboard hook.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    // Win32 modifier flags (public for external use)
    public const int MOD_ALT = 0x0001;
    public const int MOD_CONTROL = 0x0002;
    public const int MOD_SHIFT = 0x0004;
    public const int MOD_WIN = 0x0008;

    // Arbitrary unique hotkey id — must not clash with other RegisterHotKey calls in the process.
    private const int HotkeyId = 0x3A9C;

    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>Raised on the UI thread each time the configured hotkey is pressed.</summary>
    public event Action? HotkeyPressed;

    private HwndSource? _source;
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _isRegistered;
    private int _currentModifiers;
    private int _currentVirtualKey;

    /// <summary>
    /// Registers the hotkey against <paramref name="window"/>'s HWND.
    /// Call this after <c>SourceInitialized</c> so the HWND exists.
    /// </summary>
    /// <param name="window">The window to register the hotkey for</param>
    /// <param name="modifiers">Modifier keys (MOD_ALT, MOD_CONTROL, MOD_SHIFT, MOD_WIN)</param>
    /// <param name="virtualKey">Virtual key code (e.g., 0x4B for K)</param>
    /// <returns>True if the hotkey was registered successfully; false if it's already in use</returns>
    public bool Register(Window window, int modifiers, int virtualKey)
    {
        _hwnd = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);

        bool registered = RegisterHotKey(_hwnd, HotkeyId, modifiers, virtualKey);
        if (registered)
        {
            _isRegistered = true;
            _currentModifiers = modifiers;
            _currentVirtualKey = virtualKey;
        }
        else
        {
            _isRegistered = false;
        }

        return registered;
    }

    /// <summary>
    /// Updates the registered hotkey to a new combination.
    /// </summary>
    /// <returns>True if the new hotkey was registered successfully; false if it's already in use</returns>
    public bool UpdateHotkey(int modifiers, int virtualKey)
    {
        if (_hwnd == IntPtr.Zero) return false;

        if (_isRegistered && modifiers == _currentModifiers && virtualKey == _currentVirtualKey)
            return true;

        if (!_isRegistered)
        {
            bool initialRegistrationSucceeded = RegisterHotKey(_hwnd, HotkeyId, modifiers, virtualKey);
            if (initialRegistrationSucceeded)
            {
                _isRegistered = true;
                _currentModifiers = modifiers;
                _currentVirtualKey = virtualKey;
            }

            return initialRegistrationSucceeded;
        }

        int previousModifiers = _currentModifiers;
        int previousVirtualKey = _currentVirtualKey;

        // Unregister old hotkey
        UnregisterHotKey(_hwnd, HotkeyId);

        // Register new hotkey
        bool registered = RegisterHotKey(_hwnd, HotkeyId, modifiers, virtualKey);
        if (registered)
        {
            _currentModifiers = modifiers;
            _currentVirtualKey = virtualKey;
            return true;
        }

        bool restored = RegisterHotKey(_hwnd, HotkeyId, previousModifiers, previousVirtualKey);
        _isRegistered = restored;

        if (restored)
        {
            _currentModifiers = previousModifiers;
            _currentVirtualKey = previousVirtualKey;
        }

        return false;
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
        if (_hwnd != IntPtr.Zero && _isRegistered)
            UnregisterHotKey(_hwnd, HotkeyId);

        _source?.RemoveHook(WndProc);
    }
}
