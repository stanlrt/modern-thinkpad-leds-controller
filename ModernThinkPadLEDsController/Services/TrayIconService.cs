using System.Windows.Controls;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using ModernThinkPadLEDsController.Monitoring;

namespace ModernThinkPadLEDsController.Services;

// TrayIconService manages the system-tray icon (bottom-right of the taskbar).
//
// H.NotifyIcon.Wpf is a pure-WPF tray icon library. Its TaskbarIcon is a
// FrameworkElement we create and hold in a field — no WinForms dependency needed.
public sealed class TrayIconService : IDisposable
{
    public event Action? ShowWindowRequested;
    public event Action? ExitRequested;

    private readonly TaskbarIcon _taskbarIcon = new();

    public TrayIconService()
    {
    }

    public void Initialize()
    {
        try
        {
            var showItem = new System.Windows.Controls.MenuItem { Header = "Show" };
            showItem.Click += (_, _) => ShowWindowRequested?.Invoke();

            var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
            exitItem.Click += (_, _) => ExitRequested?.Invoke();

            var menu = new System.Windows.Controls.ContextMenu();
            menu.Items.Add(showItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(exitItem);

            _taskbarIcon.ToolTipText = "Modern ThinkPad LEDs Controller";

            // Load icon from file in output directory
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "favicon.ico");
            System.Diagnostics.Debug.WriteLine($"Looking for icon at: {iconPath}");
            System.Diagnostics.Debug.WriteLine($"Icon file exists: {System.IO.File.Exists(iconPath)}");

            if (System.IO.File.Exists(iconPath))
            {
                _taskbarIcon.Icon = new System.Drawing.Icon(iconPath);
                System.Diagnostics.Debug.WriteLine("Icon loaded successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Icon file not found!");
            }

            _taskbarIcon.ContextMenu = menu;
            _taskbarIcon.MenuActivation = PopupActivationMode.RightClick;
            _taskbarIcon.TrayLeftMouseUp += (_, _) => ShowWindowRequested?.Invoke();
            _taskbarIcon.TrayMouseDoubleClick += (_, _) => ShowWindowRequested?.Invoke();

            System.Diagnostics.Debug.WriteLine("About to set Visibility to Visible");
            _taskbarIcon.Visibility = System.Windows.Visibility.Visible;
            System.Diagnostics.Debug.WriteLine("TaskbarIcon visibility set to Visible");
            System.Diagnostics.Debug.WriteLine($"TaskbarIcon created: {_taskbarIcon != null}");

            // Force creation of the tray icon
            _taskbarIcon?.ForceCreate();
            System.Diagnostics.Debug.WriteLine("ForceCreate() called");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing tray icon: {ex}");
            throw;
        }
    }

    public void Dispose() => _taskbarIcon.Dispose();
}

