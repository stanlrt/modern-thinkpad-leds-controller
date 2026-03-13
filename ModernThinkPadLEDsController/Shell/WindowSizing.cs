using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;

namespace ModernThinkPadLEDsController.Shell;

/// <summary>
/// Utility class for properly handling windows maximization with auto-hide taskbar support.
/// </summary>
public static partial class WindowSizing
{
    private const int WM_GETMINMAXINFO = 0x0024;
    private const int MONITOR_DEFAULTTONEAREST = 0x00000002;
    private const int AUTOHIDE_TASKBAR_MARGIN = 2;

    private static readonly Dictionary<Window, Action> _disposeHandlers = [];


    [LibraryImport("shell32", EntryPoint = "SHAppBarMessage")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    public static partial int SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

    [LibraryImport("user32", SetLastError = true, EntryPoint = "FindWindowW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [LibraryImport("user32", SetLastError = true, EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [LibraryImport("user32", EntryPoint = "MonitorFromWindow")]
    internal static partial IntPtr MonitorFromWindow(IntPtr handle, int flags);


    public static void RegisterSizingEvents(Window window)
    {
        IntPtr handle = new WindowInteropHelper(window).Handle;
        HwndSourceHook hook = new(WindowProc);
        HwndSource? hwnd = HwndSource.FromHwnd(handle);
        if (hwnd != null)
        {
            hwnd.AddHook(hook);
            _disposeHandlers.Add(window, () => hwnd.RemoveHook(hook));
        }
    }

    public static void UnregisterSizingEvents(Window window)
    {
        if (_disposeHandlers.TryGetValue(window, out Action? dispose))
        {
            dispose();
            _disposeHandlers.Remove(window);
        }
    }

    private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            WmGetMinMaxInfo(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO))!;
        IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

        if (monitor != IntPtr.Zero)
        {
            MONITORINFO monitorInfo = new()
            {
                cbSize = Marshal.SizeOf<MONITORINFO>()
            };
            GetMonitorInfo(monitor, ref monitorInfo);
            RECT workArea = monitorInfo.rcWork;
            RECT monitorArea = monitorInfo.rcMonitor;

            mmi.ptMaxPosition.x = Math.Abs(workArea.left - monitorArea.left);
            mmi.ptMaxPosition.y = Math.Abs(workArea.top - monitorArea.top);
            mmi.ptMaxSize.x = Math.Abs(workArea.right - workArea.left);
            mmi.ptMaxSize.y = Math.Abs(workArea.bottom - workArea.top);
            mmi.ptMaxTrackSize.x = mmi.ptMaxSize.x;
            mmi.ptMaxTrackSize.y = mmi.ptMaxSize.y;
            mmi.ptMinTrackSize.x = 640;
            mmi.ptMinTrackSize.y = 400;

            mmi = AdjustWorkingAreaForAutoHide(monitor, mmi);
        }

        Marshal.StructureToPtr(mmi, lParam, true);
    }

    private static MINMAXINFO AdjustWorkingAreaForAutoHide(IntPtr monitor, MINMAXINFO mmi)
    {
        IntPtr hwnd = FindWindow("Shell_TrayWnd", null);
        if (hwnd == IntPtr.Zero)
        {
            return mmi;
        }

        IntPtr taskbarMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (!monitor.Equals(taskbarMonitor))
        {
            return mmi;
        }

        APPBARDATA abd = new()
        {
            cbSize = Marshal.SizeOf<APPBARDATA>(),
            hWnd = hwnd
        };

        SHAppBarMessage((int)ABMsg.ABM_GETTASKBARPOS, ref abd);
        ABEdge edge = GetEdge(abd.rc);
        ABState state = (ABState)SHAppBarMessage((int)ABMsg.ABM_GETSTATE, ref abd);

        if (state.HasFlag(ABState.ABS_AUTOHIDE))
        {
            AdjustSizeForAutohide(edge, ref mmi);
        }

        return mmi;
    }

    private static void AdjustSizeForAutohide(ABEdge edge, ref MINMAXINFO mmi)
    {
        switch (edge)
        {
            case ABEdge.ABE_LEFT:
                mmi.ptMaxPosition.x += AUTOHIDE_TASKBAR_MARGIN;
                mmi.ptMaxTrackSize.x -= AUTOHIDE_TASKBAR_MARGIN;
                mmi.ptMaxSize.x -= AUTOHIDE_TASKBAR_MARGIN;
                break;
            case ABEdge.ABE_RIGHT:
                mmi.ptMaxSize.x -= AUTOHIDE_TASKBAR_MARGIN;
                mmi.ptMaxTrackSize.x -= AUTOHIDE_TASKBAR_MARGIN;
                break;
            case ABEdge.ABE_TOP:
                mmi.ptMaxPosition.y += AUTOHIDE_TASKBAR_MARGIN;
                mmi.ptMaxTrackSize.y -= AUTOHIDE_TASKBAR_MARGIN;
                mmi.ptMaxSize.y -= AUTOHIDE_TASKBAR_MARGIN;
                break;
            case ABEdge.ABE_BOTTOM:
                mmi.ptMaxSize.y -= AUTOHIDE_TASKBAR_MARGIN;
                mmi.ptMaxTrackSize.y -= AUTOHIDE_TASKBAR_MARGIN;
                break;
        }
    }

    private static ABEdge GetEdge(RECT rc)
    {
        if (rc.top == rc.left && rc.bottom > rc.right)
        {
            return ABEdge.ABE_LEFT;
        }
        if (rc.top == rc.left && rc.bottom < rc.right)
        {
            return ABEdge.ABE_TOP;
        }
        if (rc.top > rc.left)
        {
            return ABEdge.ABE_BOTTOM;
        }
        return ABEdge.ABE_RIGHT;
    }

    #region Windows API Types and Enums

    public enum ABEdge
    {
        ABE_LEFT = 0,
        ABE_TOP = 1,
        ABE_RIGHT = 2,
        ABE_BOTTOM = 3
    }

    [Flags]
    public enum ABState
    {
        None = 0,
        ABS_AUTOHIDE = 1,
        ABS_ALWAYSONTOP = 1 << 1,
    }

    public enum ABMsg
    {
        ABM_NEW = 0,
        ABM_REMOVE = 1,
        ABM_QUERYPOS = 2,
        ABM_SETPOS = 3,
        ABM_GETSTATE = 4,
        ABM_GETTASKBARPOS = 5,
        ABM_ACTIVATE = 6,
        ABM_GETAUTOHIDEBAR = 7,
        ABM_SETAUTOHIDEBAR = 8,
        ABM_WINDOWPOSCHANGED = 9,
        ABM_SETSTATE = 10
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uCallbackMessage;
        public int uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;

        public POINT(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    #endregion Windows API Types and Enums
}
