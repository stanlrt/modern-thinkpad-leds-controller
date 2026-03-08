using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace ModernThinkPadLEDsController.Monitoring;

public enum MonitorState { On, Off }

// PowerEventListener translates Windows power/hardware messages into clean C# events.
//
// Windows communicates low-level events (lid open/close, monitor on/off) by sending
// "Windows Messages" — numbered packets — to a registered window handle (HWND).
//
// In WinForms (legacy), Form had a WndProc override you could intercept.
// In WPF, the Window class hides this, so we use HwndSource:
//   - HwndSource is the bridge between WPF's Window and the underlying Win32 window.
//   - AddHook registers our callback to receive every raw message the window gets.
//
// System suspend/resume comes through SystemEvents.PowerModeChanged (simpler than
// parsing WM_POWERBROADCAST wParam values).
public sealed class PowerEventListener : IDisposable
{
    public event Action<bool>? LidStateChanged;    // true = lid is open
    public event Action<MonitorState>? MonitorStateChanged;
    public event Action? SystemSuspending;
    public event Action? SystemResumed;
    public event Action<bool>? FullscreenChanged;  // true = a fullscreen app is in front

    private HwndSource? _source;
    private bool? _previousLidState;

    private System.Windows.Threading.DispatcherTimer? _fullscreenTimer;
    private bool _wasFullscreen;
    private bool _isFirstFullscreenCheck;
    private IntPtr _attachedWindowHandle = IntPtr.Zero;

    private IntPtr _lidHandle = IntPtr.Zero;
    private IntPtr _monitorHandle = IntPtr.Zero;

    // GUIDs that identify which power setting notification we're registering for.
    // These are fixed Windows constants — they never change between OS versions.
    private static readonly Guid GUID_LIDSWITCH_STATE_CHANGE =
        new(0xBA3E0F4D, 0xB817, 0x4094, 0xA2, 0xD1, 0xD5, 0x63, 0x79, 0xE6, 0xA0, 0xF3);

    private static readonly Guid GUID_MONITOR_POWER_ON =
        new(0x02731015, 0x4510, 0x4526, 0x99, 0xE6, 0xE5, 0xA1, 0x7E, 0xBD, 0x1A, 0xEA);

    private const int WM_POWERBROADCAST = 0x0218;
    private const int PBT_POWERSETTINGCHANGE = 0x8013;
    private const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x0000;

    [StructLayout(LayoutKind.Sequential)]
    private struct POWERBROADCAST_SETTING
    {
        public Guid PowerSetting;
        public uint DataLength;
        public byte Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("User32.dll", SetLastError = true)]
    private static extern IntPtr RegisterPowerSettingNotification(
        IntPtr hRecipient, ref Guid PowerSettingGuid, int Flags);

    [DllImport("User32.dll", SetLastError = true)]
    private static extern bool UnregisterPowerSettingNotification(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out QUERY_USER_NOTIFICATION_STATE pquns);

    private const int GWL_STYLE = -16;
    private const long WS_CAPTION = 0x00C00000L;
    private const long WS_THICKFRAME = 0x00040000L;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int FullscreenBoundsTolerance = 2;

    private enum QUERY_USER_NOTIFICATION_STATE
    {
        QUNS_NOT_PRESENT = 1,              // No user is logged in
        QUNS_BUSY = 2,                      // User is busy (screen saver, full screen app, etc)
        QUNS_RUNNING_D3D_FULL_SCREEN = 3,  // A D3D fullscreen app is running
        QUNS_PRESENTATION_MODE = 4,         // Presentation mode is enabled
        QUNS_ACCEPTS_NOTIFICATIONS = 5,     // Normal state - notifications allowed
        QUNS_QUIET_TIME = 6,               // Quiet hours
        QUNS_APP = 7                       // An app has focus
    }

    // Attach() must be called after the main Window has loaded (so its HWND exists).
    // WindowInteropHelper retrieves the underlying Win32 window handle from a WPF Window.
    public void Attach(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _attachedWindowHandle = helper.Handle;
        _source = HwndSource.FromHwnd(helper.Handle);
        _source.AddHook(WndProcHook);

        Guid lidGuid = GUID_LIDSWITCH_STATE_CHANGE;
        Guid monGuid = GUID_MONITOR_POWER_ON;
        _lidHandle = RegisterPowerSettingNotification(helper.Handle, ref lidGuid, DEVICE_NOTIFY_WINDOW_HANDLE);
        _monitorHandle = RegisterPowerSettingNotification(helper.Handle, ref monGuid, DEVICE_NOTIFY_WINDOW_HANDLE);

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    // Fullscreen polling uses a DispatcherTimer — it runs on the WPF UI thread
    // at a fixed interval, avoiding any threading complexity.
    public void StartFullscreenPolling()
    {
        // Mark first check to skip firing events during initialization
        _isFirstFullscreenCheck = true;

        if (_fullscreenTimer is null)
        {
            _fullscreenTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _fullscreenTimer.Tick += OnFullscreenTimerTick;
        }

        _fullscreenTimer.Stop();
        _fullscreenTimer.Start();
    }

    public void StopFullscreenPolling() => _fullscreenTimer?.Stop();

    private void OnFullscreenTimerTick(object? sender, EventArgs e) => CheckFullscreen();

    private void CheckFullscreen()
    {
        bool isFull = IsForegroundFullscreen();

        // On first check, just initialize state without firing events
        if (_isFirstFullscreenCheck)
        {
            _wasFullscreen = isFull;
            _isFirstFullscreenCheck = false;
            return;
        }

        if (isFull == _wasFullscreen) return;

        _wasFullscreen = isFull;
        FullscreenChanged?.Invoke(isFull);
    }

    private bool IsForegroundFullscreen()
    {
        int result = SHQueryUserNotificationState(out QUERY_USER_NOTIFICATION_STATE state);

        // Exclusive fullscreen apps report through the shell API.
        if (result == 0 && state == QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN)
            return true;

        // Borderless fullscreen apps (for example Electron/Chromium players) don't always
        // use exclusive mode, so fall back to foreground-window vs monitor-bounds matching.
        IntPtr foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero || foregroundWindow == _attachedWindowHandle)
            return false;

        if (!IsWindowVisible(foregroundWindow))
            return false;

        IntPtr monitor = MonitorFromWindow(foregroundWindow, MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
            return false;

        MONITORINFO monitorInfo = new() { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
            return false;

        if (!GetWindowRect(foregroundWindow, out RECT windowRect))
            return false;

        if (!CoversMonitor(windowRect, monitorInfo.rcMonitor))
            return false;

        long style = GetWindowStyle(foregroundWindow);
        bool hasStandardChrome = (style & (WS_CAPTION | WS_THICKFRAME)) != 0;
        return !hasStandardChrome;
    }

    private static bool CoversMonitor(RECT windowRect, RECT monitorRect)
    {
        return Math.Abs(windowRect.left - monitorRect.left) <= FullscreenBoundsTolerance
            && Math.Abs(windowRect.top - monitorRect.top) <= FullscreenBoundsTolerance
            && Math.Abs(windowRect.right - monitorRect.right) <= FullscreenBoundsTolerance
            && Math.Abs(windowRect.bottom - monitorRect.bottom) <= FullscreenBoundsTolerance;
    }

    private static long GetWindowStyle(IntPtr hWnd)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, GWL_STYLE).ToInt64()
            : GetWindowLongPtr32(hWnd, GWL_STYLE).ToInt32();
    }

    // This method is called by the WPF message pump for EVERY message the window gets.
    // We only care about WM_POWERBROADCAST with PBT_POWERSETTINGCHANGE.
    private IntPtr WndProcHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_POWERBROADCAST && (int)wParam == PBT_POWERSETTINGCHANGE)
        {
            var ps = Marshal.PtrToStructure<POWERBROADCAST_SETTING>(lParam);

            if (ps.PowerSetting == GUID_LIDSWITCH_STATE_CHANGE)
            {
                bool isOpen = ps.Data != 0;
                if (_previousLidState != isOpen)
                {
                    _previousLidState = isOpen;
                    LidStateChanged?.Invoke(isOpen);
                }
                handled = true;
            }
            else if (ps.PowerSetting == GUID_MONITOR_POWER_ON)
            {
                MonitorStateChanged?.Invoke(ps.Data != 0 ? MonitorState.On : MonitorState.Off);
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Suspend: SystemSuspending?.Invoke(); break;
            case PowerModes.Resume: SystemResumed?.Invoke(); break;
        }
    }

    public void Dispose()
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        if (_lidHandle != IntPtr.Zero) UnregisterPowerSettingNotification(_lidHandle);
        if (_monitorHandle != IntPtr.Zero) UnregisterPowerSettingNotification(_monitorHandle);
        _source?.RemoveHook(WndProcHook);
        _fullscreenTimer?.Stop();
    }
}
