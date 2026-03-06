using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using ModernThinkPadLEDsController.ViewModels;
using Wpf.Ui.Controls;

namespace ModernThinkPadLEDsController;

public partial class MainWindow : FluentWindow
{
    public MainViewModel MainVm { get; }
    public SettingsViewModel SettingsVm { get; }

    public MainWindow(MainViewModel mainVm, SettingsViewModel settingsVm)
    {
        MainVm = mainVm;
        SettingsVm = settingsVm;
        InitializeComponent();
        DataContext = this;

        // Hook into Win32 message pump to catch horizontal mouse wheel events
        SourceInitialized += (s, e) =>
        {
            var source = PresentationSource.FromVisual(this) as HwndSource;
            source?.AddHook((IntPtr _, int msg, IntPtr wParam, IntPtr __, ref bool handled) =>
            {
                if (msg == WM_MOUSEHWHEEL && TableScrollViewer != null)
                {
                    int delta = (short)((long)wParam >> 16);
                    TableScrollViewer.ScrollToHorizontalOffset(TableScrollViewer.HorizontalOffset + delta / 3.0);
                    handled = true;
                }
                return IntPtr.Zero;
            });
        };
    }

    // When the user clicks ✕, hide to tray instead of closing.
    // App.xaml.cs listens to TrayIconService.ExitRequested for a real exit.
    private void Window_Closing(object sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    // Adjust vertical scroll speed for smoother scrolling
    private void MainScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var sv = (ScrollViewer)sender;
        // Reduce scroll speed: divide delta by 3 for smoother scrolling
        sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 3.0);
        e.Handled = true;
    }

    // Let vertical scrolling bubble up from table to main ScrollViewer
    private void TableScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Don't handle - let the event bubble up to MainScrollViewer
        e.Handled = false;
    }

    // Constant for horizontal mouse wheel messages from precision touchpads
    private const int WM_MOUSEHWHEEL = 0x020E;
}
