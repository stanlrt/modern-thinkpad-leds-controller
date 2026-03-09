using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ModernThinkPadLEDsController.Shell;

/// <summary>
/// <para>
/// Registers a configurable system-wide hotkey and raises <see cref="HotkeyPressed"/>
/// each time the user presses the combination.
/// </para>
/// <para>
/// Uses the Win32 RegisterHotKey / UnregisterHotKey API wired through an HwndSource hook
/// on the main window — the standard WPF approach for global hotkeys that does not require
/// a separate background thread or a keyboard hook.
/// </para>
/// </summary>
public sealed class HotkeyService : IDisposable
{
    /// <summary>
    /// Arbitrary unique hotkey id — must not clash with other RegisterHotKey calls in the process.
    /// </summary>
    private const int HOTKEY_ID = 0x3A9C;
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
    private HotkeyModifiers _currentModifiers;
    private int _currentVirtualKey;

    /// <summary>
    /// Registers the hotkey against <paramref name="window"/>'s HWND.
    /// Call this after <c>SourceInitialized</c> so the HWND exists.
    /// </summary>
    /// <param name="window">The window to register the hotkey for</param>
    /// <param name="hotkey">The hotkey binding to register</param>
    /// <returns>True if the hotkey was registered successfully; false if it's already in use</returns>
    public bool Register(Window window, HotkeyBinding hotkey)
    {
        _hwnd = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);

        bool registered = RegisterHotKey(_hwnd, HOTKEY_ID, (int)hotkey.Modifiers, hotkey.VirtualKey);
        if (registered)
        {
            _isRegistered = true;
            _currentModifiers = hotkey.Modifiers;
            _currentVirtualKey = hotkey.VirtualKey;
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
    public bool UpdateHotkey(HotkeyBinding hotkey)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return false;
        }

        if (_isRegistered && hotkey.Modifiers == _currentModifiers && hotkey.VirtualKey == _currentVirtualKey)
        {
            return true;
        }

        if (!_isRegistered)
        {
            bool initialRegistrationSucceeded = RegisterHotKey(_hwnd, HOTKEY_ID, (int)hotkey.Modifiers, hotkey.VirtualKey);
            if (initialRegistrationSucceeded)
            {
                _isRegistered = true;
                _currentModifiers = hotkey.Modifiers;
                _currentVirtualKey = hotkey.VirtualKey;
            }

            return initialRegistrationSucceeded;
        }

        HotkeyModifiers previousModifiers = _currentModifiers;
        int previousVirtualKey = _currentVirtualKey;

        // Unregister old hotkey
        UnregisterHotKey(_hwnd, HOTKEY_ID);

        // Register new hotkey
        bool registered = RegisterHotKey(_hwnd, HOTKEY_ID, (int)hotkey.Modifiers, hotkey.VirtualKey);
        if (registered)
        {
            _currentModifiers = hotkey.Modifiers;
            _currentVirtualKey = hotkey.VirtualKey;
            return true;
        }

        bool restored = RegisterHotKey(_hwnd, HOTKEY_ID, (int)previousModifiers, previousVirtualKey);
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
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero && _isRegistered)
        {
            UnregisterHotKey(_hwnd, HOTKEY_ID);
        }

        _source?.RemoveHook(WndProc);
    }
}
