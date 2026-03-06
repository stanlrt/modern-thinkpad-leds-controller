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
    public event Action<bool>?        LidStateChanged;    // true = lid is open
    public event Action<MonitorState>? MonitorStateChanged;
    public event Action?              SystemSuspending;
    public event Action?              SystemResumed;
    public event Action<bool>?        FullscreenChanged;  // true = a fullscreen app is in front

    private HwndSource? _source;
    private bool? _previousLidState;

    private System.Windows.Threading.DispatcherTimer? _fullscreenTimer;
    private bool _wasFullscreen;

    private IntPtr _lidHandle     = IntPtr.Zero;
    private IntPtr _monitorHandle = IntPtr.Zero;

    // GUIDs that identify which power setting notification we're registering for.
    // These are fixed Windows constants — they never change between OS versions.
    private static readonly Guid GUID_LIDSWITCH_STATE_CHANGE =
        new(0xBA3E0F4D, 0xB817, 0x4094, 0xA2, 0xD1, 0xD5, 0x63, 0x79, 0xE6, 0xA0, 0xF3);

    private static readonly Guid GUID_MONITOR_POWER_ON =
        new(0x02731015, 0x4510, 0x4526, 0x99, 0xE6, 0xE5, 0xA1, 0x7E, 0xBD, 0x1A, 0xEA);

    private const int WM_POWERBROADCAST      = 0x0218;
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

    [DllImport("User32.dll", SetLastError = true)]
    private static extern IntPtr RegisterPowerSettingNotification(
        IntPtr hRecipient, ref Guid PowerSettingGuid, int Flags);

    [DllImport("User32.dll", SetLastError = true)]
    private static extern bool UnregisterPowerSettingNotification(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(HandleRef hWnd, ref RECT rect);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    // Attach() must be called after the main Window has loaded (so its HWND exists).
    // WindowInteropHelper retrieves the underlying Win32 window handle from a WPF Window.
    public void Attach(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _source    = HwndSource.FromHwnd(helper.Handle);
        _source.AddHook(WndProcHook);

        Guid lidGuid = GUID_LIDSWITCH_STATE_CHANGE;
        Guid monGuid = GUID_MONITOR_POWER_ON;
        _lidHandle     = RegisterPowerSettingNotification(helper.Handle, ref lidGuid, DEVICE_NOTIFY_WINDOW_HANDLE);
        _monitorHandle = RegisterPowerSettingNotification(helper.Handle, ref monGuid, DEVICE_NOTIFY_WINDOW_HANDLE);

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    // Fullscreen polling uses a DispatcherTimer — it runs on the WPF UI thread
    // at a fixed interval, avoiding any threading complexity.
    public void StartFullscreenPolling()
    {
        _fullscreenTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _fullscreenTimer.Tick += (_, _) => CheckFullscreen();
        _fullscreenTimer.Start();
    }

    public void StopFullscreenPolling() => _fullscreenTimer?.Stop();

    private void CheckFullscreen()
    {
        bool isFull = IsForegroundFullscreen();
        if (isFull == _wasFullscreen) return;
        _wasFullscreen = isFull;
        FullscreenChanged?.Invoke(isFull);
    }

    private static bool IsForegroundFullscreen()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;

        var rect = new RECT();
        GetWindowRect(new HandleRef(null, hwnd), ref rect);

        int screenW = GetSystemMetrics(SM_CXSCREEN);
        int screenH = GetSystemMetrics(SM_CYSCREEN);

        return rect.left <= 0 && rect.top <= 0 &&
               rect.right >= screenW && rect.bottom >= screenH;
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
            case PowerModes.Resume:  SystemResumed?.Invoke();    break;
        }
    }

    public void Dispose()
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        if (_lidHandle     != IntPtr.Zero) UnregisterPowerSettingNotification(_lidHandle);
        if (_monitorHandle != IntPtr.Zero) UnregisterPowerSettingNotification(_monitorHandle);
        _source?.RemoveHook(WndProcHook);
        _fullscreenTimer?.Stop();
    }
}
