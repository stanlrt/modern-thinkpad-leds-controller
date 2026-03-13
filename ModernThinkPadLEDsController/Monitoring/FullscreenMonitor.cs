using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ModernThinkPadLEDsController.Monitoring;

/// <summary>
/// Detects when another window has entered fullscreen mode.
/// </summary>
public sealed partial class FullscreenMonitor : ILifecycleMonitor, IWindowAttachedMonitor
{
    public event Action<bool>? FullscreenChanged;

    private DispatcherTimer? _timer;
    private bool _wasFullscreen;
    private bool _isFirstCheck;
    private IntPtr _ignoredWindowHandle = IntPtr.Zero;

    private const int GWL_STYLE = -16;
    private const long WS_CAPTION = 0x00C00000L;
    private const long WS_THICK_FRAME = 0x00040000L;
    private const uint MONITOR_DEFAULT_TO_NEAREST = 2;
    private const int FULLSCREEN_BOUNDS_TOLERANCE = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int CbSize;
        public Rect RcMonitor;
        public Rect RcWork;
        public uint DwFlags;
    }

    private enum QueryUserNotificationState
    {
        QunsNotPresent = 1,
        QunsBusy = 2,
        QunsRunningD3dFullScreen = 3,
        QunsPresentationMode = 4,
        QunsAcceptsNotifications = 5,
        QunsQuietTime = 6,
        QunsApp = 7,
    }

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(IntPtr hWnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo monitorInfo);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static partial IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static partial IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

    [LibraryImport("shell32.dll")]
    private static partial int SHQueryUserNotificationState(out QueryUserNotificationState state);

    /// <summary>
    /// Sets the app window handle that should be ignored by fullscreen detection.
    /// </summary>
    public void Attach(Window window)
    {
        _ignoredWindowHandle = new WindowInteropHelper(window).Handle;
    }

    /// <summary>
    /// Starts polling for fullscreen changes.
    /// </summary>
    public void Start()
    {
        _isFirstCheck = true;

        if (_timer is null)
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500),
            };
            _timer.Tick += OnTimerTick;
        }

        _timer.Stop();
        _timer.Start();
    }

    /// <summary>
    /// Stops polling for fullscreen changes.
    /// </summary>
    public void Stop()
    {
        _timer?.Stop();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        bool isFullscreen = IsForegroundFullscreen();

        if (_isFirstCheck)
        {
            _wasFullscreen = isFullscreen;
            _isFirstCheck = false;
            return;
        }

        if (isFullscreen == _wasFullscreen)
        {
            return;
        }

        _wasFullscreen = isFullscreen;
        FullscreenChanged?.Invoke(isFullscreen);
    }

    private bool IsForegroundFullscreen()
    {
        int result = SHQueryUserNotificationState(out QueryUserNotificationState state);
        if (result == 0 && state == QueryUserNotificationState.QunsRunningD3dFullScreen)
        {
            return true;
        }

        IntPtr foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero || foregroundWindow == _ignoredWindowHandle)
        {
            return false;
        }

        if (!IsWindowVisible(foregroundWindow))
        {
            return false;
        }

        IntPtr monitor = MonitorFromWindow(foregroundWindow, MONITOR_DEFAULT_TO_NEAREST);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        MonitorInfo monitorInfo = new() { CbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return false;
        }

        if (!GetWindowRect(foregroundWindow, out Rect windowRect))
        {
            return false;
        }

        if (!CoversMonitor(windowRect, monitorInfo.RcMonitor))
        {
            return false;
        }

        long style = GetWindowStyle(foregroundWindow);
        bool hasStandardChrome = (style & (WS_CAPTION | WS_THICK_FRAME)) != 0;
        return !hasStandardChrome;
    }

    private static bool CoversMonitor(Rect windowRect, Rect monitorRect)
    {
        return Math.Abs(windowRect.Left - monitorRect.Left) <= FULLSCREEN_BOUNDS_TOLERANCE
            && Math.Abs(windowRect.Top - monitorRect.Top) <= FULLSCREEN_BOUNDS_TOLERANCE
            && Math.Abs(windowRect.Right - monitorRect.Right) <= FULLSCREEN_BOUNDS_TOLERANCE
            && Math.Abs(windowRect.Bottom - monitorRect.Bottom) <= FULLSCREEN_BOUNDS_TOLERANCE;
    }

    private static long GetWindowStyle(IntPtr hWnd)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, GWL_STYLE).ToInt64()
            : GetWindowLongPtr32(hWnd, GWL_STYLE).ToInt32();
    }

    public void Dispose()
    {
        if (_timer is null)
        {
            return;
        }

        _timer.Stop();
        _timer.Tick -= OnTimerTick;
    }
}
