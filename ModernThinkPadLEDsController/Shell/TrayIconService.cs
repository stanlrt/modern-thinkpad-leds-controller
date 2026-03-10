using System.IO;
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace ModernThinkPadLEDsController.Shell;

/// <summary>
/// Owns the system tray icon and menu callbacks.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    public event Action? ShowWindowRequested;
    public event Action? ExitRequested;

    private readonly TaskbarIcon _taskbarIcon = new();

    public void Initialize()
    {
        try
        {
            MenuItem showItem = new() { Header = "Show" };
            showItem.Click += (_, _) => ShowWindowRequested?.Invoke();

            MenuItem exitItem = new() { Header = "Exit" };
            exitItem.Click += (_, _) => ExitRequested?.Invoke();

            ContextMenu menu = new();
            menu.Items.Add(showItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(exitItem);

            _taskbarIcon.ToolTipText = "Modern ThinkPad LEDs Controller";

            // Load icon from WPF embedded resource using pack:// URI
            // This works with trimming and doesn't require Windows Forms
            Uri iconUri = new("pack://application:,,,/Resources/favicon.ico");
            try
            {
                System.Windows.Resources.StreamResourceInfo resourceInfo = Application.GetResourceStream(iconUri);
                if (resourceInfo != null)
                {
                    using Stream iconStream = resourceInfo.Stream;
                    _taskbarIcon.Icon = new System.Drawing.Icon(iconStream);
                }
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load icon from resources: {ex.Message}");
                // Icon is optional - tray will still work without it
            }

            // Don't assign ContextMenu directly - handle manually to fix first-click positioning
            _taskbarIcon.MenuActivation = PopupActivationMode.None; // Disable default behavior
            _taskbarIcon.TrayRightMouseUp += (_, _) => menu.IsOpen = true;

            _taskbarIcon.TrayLeftMouseUp += (_, _) => ShowWindowRequested?.Invoke();
            _taskbarIcon.TrayMouseDoubleClick += (_, _) => ShowWindowRequested?.Invoke();

            _taskbarIcon.Visibility = System.Windows.Visibility.Visible;
            System.Diagnostics.Debug.WriteLine($"TaskbarIcon created: {_taskbarIcon != null}");

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

