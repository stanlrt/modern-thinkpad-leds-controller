using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace ModernThinkPadLEDsController.Monitoring;

/// <summary>
/// Represents the monitor power state reported by Windows.
/// </summary>
public enum MonitorState
{
    On = 0,
    Off = 1,
}

/// <summary>
/// Translates window power messages into application-level events.
/// </summary>
public sealed class PowerEventMonitor : IWindowAttachedMonitor
{
    public event Action<bool>? LidStateChanged;
    public event Action<MonitorState>? MonitorStateChanged;
    public event Action? SystemSuspending;
    public event Action? SystemResumed;

    private HwndSource? _source;
    private bool? _previousLidState;
    private IntPtr _lidHandle = IntPtr.Zero;
    private IntPtr _monitorHandle = IntPtr.Zero;

    private static readonly Guid _guidLidSwitchStateChange =
        new(0xBA3E0F4D, 0xB817, 0x4094, 0xA2, 0xD1, 0xD5, 0x63, 0x79, 0xE6, 0xA0, 0xF3);

    private static readonly Guid _guidMonitorPowerOn =
        new(0x02731015, 0x4510, 0x4526, 0x99, 0xE6, 0xE5, 0xA1, 0x7E, 0xBD, 0x1A, 0xEA);

    private const int WM_POWER_BROADCAST = 0x0218;
    private const int PBT_POWER_SETTING_CHANGE = 0x8013;
    private const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x0000;

    [StructLayout(LayoutKind.Sequential)]
    private struct PowerBroadcastSetting
    {
        public Guid PowerSetting;
        public uint DataLength;
        public byte Data;
    }

    [DllImport("User32.dll", SetLastError = true)]
    private static extern IntPtr RegisterPowerSettingNotification(
        IntPtr hRecipient, ref Guid powerSettingGuid, int flags);

    [DllImport("User32.dll", SetLastError = true)]
    private static extern bool UnregisterPowerSettingNotification(IntPtr handle);

    /// <summary>
    /// Attaches native power notifications to the provided window.
    /// </summary>
    public void Attach(Window window)
    {
        WindowInteropHelper helper = new(window);
        _source = HwndSource.FromHwnd(helper.Handle);
        _source?.AddHook(WndProcHook);

        Guid lidGuid = _guidLidSwitchStateChange;
        Guid monitorGuid = _guidMonitorPowerOn;
        _lidHandle = RegisterPowerSettingNotification(helper.Handle, ref lidGuid, DEVICE_NOTIFY_WINDOW_HANDLE);
        _monitorHandle = RegisterPowerSettingNotification(helper.Handle, ref monitorGuid, DEVICE_NOTIFY_WINDOW_HANDLE);

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    private IntPtr WndProcHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_POWER_BROADCAST && (int)wParam == PBT_POWER_SETTING_CHANGE)
        {
            PowerBroadcastSetting powerSetting = Marshal.PtrToStructure<PowerBroadcastSetting>(lParam);

            if (powerSetting.PowerSetting == _guidLidSwitchStateChange)
            {
                bool isOpen = powerSetting.Data != 0;
                if (_previousLidState != isOpen)
                {
                    _previousLidState = isOpen;
                    LidStateChanged?.Invoke(isOpen);
                }
                handled = true;
            }
            else if (powerSetting.PowerSetting == _guidMonitorPowerOn)
            {
                MonitorStateChanged?.Invoke(powerSetting.Data != 0 ? MonitorState.On : MonitorState.Off);
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Suspend:
                SystemSuspending?.Invoke();
                break;
            case PowerModes.Resume:
                SystemResumed?.Invoke();
                break;
        }
    }

    public void Dispose()
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;

        if (_lidHandle != IntPtr.Zero)
        {
            UnregisterPowerSettingNotification(_lidHandle);
        }

        if (_monitorHandle != IntPtr.Zero)
        {
            UnregisterPowerSettingNotification(_monitorHandle);
        }

        _source?.RemoveHook(WndProcHook);
    }
}
