using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using ModernThinkPadLEDsController.ViewModels;
using Wpf.Ui.Controls;

namespace ModernThinkPadLEDsController;

public partial class MainWindow : FluentWindow
{

    private const int WM_MOUSEHWHEEL = 0x020E;
    private const double SCROLL_SENSITIVITY = 3.0;

    public MainViewModel MainVm { get; }
    public SettingsViewModel SettingsVm { get; }

    public MainWindow(MainViewModel mainVm, SettingsViewModel settingsVm)
    {
        MainVm = mainVm;
        SettingsVm = settingsVm;
        InitializeComponent();
        DataContext = this;
        SourceInitialized += OnWindowInitialized;
    }

    private void OnWindowInitialized(object? sender, EventArgs e)
    {
        var source = PresentationSource.FromVisual(this) as HwndSource;
        source?.AddHook(OnHorizontalMouseWheel);
    }

    private IntPtr OnHorizontalMouseWheel(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_MOUSEHWHEEL && TableScrollViewer != null)
        {
            int delta = ExtractMouseWheelDelta(wParam);
            TableScrollViewer.ScrollToHorizontalOffset(TableScrollViewer.HorizontalOffset + delta / SCROLL_SENSITIVITY);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void OnWindowClosing(object sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private void MainScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ReduceVerticalScrollSpeed(sender, e);
    }

    private void ReduceVerticalScrollSpeed(object sender, MouseWheelEventArgs e)
    {
        var sv = (ScrollViewer)sender;
        sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / SCROLL_SENSITIVITY);
        e.Handled = true;
    }

    private void TableScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Don't handle, let event bubble up to MainScrollViewer
        e.Handled = false;
    }

    private void OnCheckForUpdateClicked(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/stanlrt/modern-thinkpad-leds-controller/releases",
            UseShellExecute = true
        });
    }

    private static int ExtractMouseWheelDelta(IntPtr wParam)
    {
        return (short)((long)wParam >> 16);
    }


}
