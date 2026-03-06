using System.Windows.Controls;
using System.Windows.Media;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using ModernThinkPadLEDsController.Monitoring;

namespace ModernThinkPadLEDsController.Services;

// TrayIconService manages the system-tray icon (bottom-right of the taskbar).
//
// H.NotifyIcon.Wpf is a pure-WPF tray icon library. Its TaskbarIcon is a
// FrameworkElement we create and hold in a field — no WinForms dependency needed.
//
// GeneratedIconSource renders a filled circle using WPF's own rendering pipeline,
// so we don't need System.Drawing at all.
public sealed class TrayIconService : IDisposable
{
    public event Action? ShowWindowRequested;
    public event Action? ExitRequested;

    private readonly TaskbarIcon _taskbarIcon = new();

    // Pre-baked icon sources so we don't regenerate on every disk poll tick.
    private readonly GeneratedIconSource _iconIdle;
    private readonly GeneratedIconSource _iconRead;
    private readonly GeneratedIconSource _iconWrite;
    private readonly GeneratedIconSource _iconReadWrite;

    public TrayIconService()
    {
        _iconIdle      = MakeIconSource(Color.FromRgb(80, 80, 80));     // dark grey  = idle
        _iconRead      = MakeIconSource(Color.FromRgb(30, 120, 210));   // blue       = reading
        _iconWrite     = MakeIconSource(Color.FromRgb(200, 100, 20));   // orange     = writing
        _iconReadWrite = MakeIconSource(Color.FromRgb(0, 160, 80));     // green      = both
    }

    public void Initialize()
    {
        var showItem = new MenuItem { Header = "Show" };
        showItem.Click += (_, _) => ShowWindowRequested?.Invoke();

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => ExitRequested?.Invoke();

        var menu = new ContextMenu();
        menu.Items.Add(showItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(exitItem);

        _taskbarIcon.ToolTipText       = "Modern ThinkPad LEDs Controller";
        _taskbarIcon.IconSource        = _iconIdle;
        _taskbarIcon.ContextMenu       = menu;
        _taskbarIcon.MenuActivation    = PopupActivationMode.RightClick;
        _taskbarIcon.TrayMouseDoubleClick += (_, _) => ShowWindowRequested?.Invoke();
    }

    public void SetDiskState(DiskActivityState state)
    {
        _taskbarIcon.IconSource = state switch
        {
            DiskActivityState.Read      => _iconRead,
            DiskActivityState.Write     => _iconWrite,
            DiskActivityState.ReadWrite => _iconReadWrite,
            _                           => _iconIdle,
        };
    }

    private static GeneratedIconSource MakeIconSource(Color color) =>
        new() { Text = "⬤", Foreground = new SolidColorBrush(color) };

    public void Dispose() => _taskbarIcon.Dispose();
}

