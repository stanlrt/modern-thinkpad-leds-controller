using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    }

    // When the user clicks ✕, hide to tray instead of closing.
    // App.xaml.cs listens to TrayIconService.ExitRequested for a real exit.
    private void Window_Closing(object sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    // The table's horizontal ScrollViewer would swallow all wheel events, blocking
    // vertical scroll on the outer ScrollViewer while hovering the table.
    // Only bubble the event up when the inner viewer can't scroll horizontally
    // (i.e. it is already at its limit), so horizontal touchpad swipes are kept
    // by the inner viewer while vertical scroll always reaches the outer one.
    private void TableScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var sv = (ScrollViewer)sender;

        // Let the inner viewer keep the event if it can still scroll horizontally.
        bool canScrollH = sv.ScrollableWidth > 0
                          && ((e.Delta < 0 && sv.HorizontalOffset < sv.ScrollableWidth)
                              || (e.Delta > 0 && sv.HorizontalOffset > 0));

        // Shift+wheel is the standard horizontal-scroll gesture — keep those too.
        bool isHorizontalGesture = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        if (canScrollH || isHorizontalGesture)
            return;

        // Vertical (or exhausted horizontal) — bubble up to the outer ScrollViewer.
        e.Handled = true;
        var parent = sv.Parent as UIElement;
        parent?.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = MouseWheelEvent,
            Source = sender
        });
    }
}
