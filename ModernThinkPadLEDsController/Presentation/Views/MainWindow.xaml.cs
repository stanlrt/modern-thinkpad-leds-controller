using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using ModernThinkPadLEDsController.Shell;
using ModernThinkPadLEDsController.Presentation.ViewModels;
using Wpf.Ui.Controls;

namespace ModernThinkPadLEDsController.Presentation.Views;

/// <summary>
/// Hosts the main settings and LED configuration UI.
/// </summary>
public partial class MainWindow : FluentWindow
{

    private const int WM_MOUSEHWHEEL = 0x020E;
    private const double SCROLL_SENSITIVITY = 3.0;
    private bool _horizontalMouseWheelHookAttached;
    private readonly HotkeyConfigurationService _hotkeyConfiguration;

    public MainViewModel MainVm { get; }
    public SettingsViewModel SettingsVm { get; }

    public MainWindow(
        MainViewModel mainVm,
        SettingsViewModel settingsVm,
        HotkeyConfigurationService hotkeyConfiguration)
    {
        MainVm = mainVm;
        SettingsVm = settingsVm;
        _hotkeyConfiguration = hotkeyConfiguration;
        InitializeComponent();
        DataContext = this;
        SourceInitialized += OnWindowInitialized;
        Loaded += OnWindowLoaded;
    }

    private void OnWindowInitialized(object? sender, EventArgs e)
    {
        TryAttachHorizontalMouseWheelHook();
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        TryAttachHorizontalMouseWheelHook();
    }

    private void TryAttachHorizontalMouseWheelHook()
    {
        if (_horizontalMouseWheelHookAttached)
        {
            return;
        }

        IntPtr handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        HwndSource? source = HwndSource.FromHwnd(handle);
        if (source is null)
        {
            return;
        }

        source.AddHook(OnHorizontalMouseWheel);
        _horizontalMouseWheelHookAttached = true;
        SourceInitialized -= OnWindowInitialized;
        Loaded -= OnWindowLoaded;
    }

    private IntPtr OnHorizontalMouseWheel(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_MOUSEHWHEEL && TableScrollViewer != null)
        {
            int delta = ExtractMouseWheelDelta(wParam);
            TableScrollViewer.ScrollToHorizontalOffset(TableScrollViewer.HorizontalOffset + (delta / SCROLL_SENSITIVITY));
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
        ScrollViewer sv = (ScrollViewer)sender;
        sv.ScrollToVerticalOffset(sv.VerticalOffset - (e.Delta / SCROLL_SENSITIVITY));
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

    // --- Hotkey capture methods ---

    private void OnHotkeyBorderClick(object sender, MouseButtonEventArgs e)
    {
        HotkeyTextBox.Focus();
    }

    private void OnHotkeyFocused(object sender, RoutedEventArgs e)
    {
        MainVm.IsRecordingHotkey = true;
        MainVm.HotkeyDisplayText = "Press your keys...";
    }

    private void OnHotkeyLostFocus(object sender, RoutedEventArgs e)
    {
        if (MainVm.IsRecordingHotkey)
        {
            MainVm.IsRecordingHotkey = false;
            // Restore previous hotkey display if user didn't press anything
            UpdateHotkeyDisplayFromSettings();
        }
    }

    private void OnHotkeyCapture(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!MainVm.IsRecordingHotkey)
        {
            return;
        }

        e.Handled = true;

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        HotkeyBinding hotkey = HotkeyBinding.FromCurrentKeyPress(key);

        HotkeyCaptureResult captureResult = _hotkeyConfiguration.CaptureHotkey(hotkey);
        if (!captureResult.CaptureCompleted)
        {
            return;
        }

        MainVm.HotkeyDisplayText = captureResult.DisplayText;
        MainVm.HotkeyWarningMessage = captureResult.WarningMessage;
        MainVm.IsRecordingHotkey = false;

        // Remove focus
        HotkeyTextBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
    }

    private void UpdateHotkeyDisplayFromSettings()
    {
        MainVm.HotkeyDisplayText = _hotkeyConfiguration.GetHotkeyDisplayText();
    }

    private static int ExtractMouseWheelDelta(IntPtr wParam)
    {
        const int SHIFT_TO_HIGH_WORD = 16;
        return (short)((long)wParam >> SHIFT_TO_HIGH_WORD);
    }

    // --- LED row hover effects ---

    private void OnLedRowMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string ledName)
        {
            if (FindName($"{ledName}LedName") is System.Windows.Controls.TextBlock ledNameTextBlock)
            {
                ledNameTextBlock.FontWeight = FontWeights.Bold;
            }

            // Bold the mode header
            int column = Grid.GetColumn(element);
            BoldModeHeader(column, true);
        }
    }

    private void OnLedRowMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string ledName)
        {
            if (FindName($"{ledName}LedName") is System.Windows.Controls.TextBlock ledNameTextBlock)
            {
                ledNameTextBlock.FontWeight = FontWeights.Normal;
            }

            // Restore mode header weight
            int column = Grid.GetColumn(element);
            BoldModeHeader(column, false);
        }
    }

    private void BoldModeHeader(int column, bool bold)
    {
        FontWeight weight = bold ? FontWeights.Bold : FontWeights.SemiBold;

        switch (column)
        {
            case 0:
                if (FindName("DefaultModeHeader") is Controls.LabelWithHelp defaultHeader)
                {
                    defaultHeader.FontWeight = weight;
                }

                break;
            case 1:
                if (FindName("OnModeHeader") is System.Windows.Controls.TextBlock onHeader)
                {
                    onHeader.FontWeight = weight;
                }

                break;
            case 2:
                if (FindName("OffModeHeader") is System.Windows.Controls.TextBlock offHeader)
                {
                    offHeader.FontWeight = weight;
                }

                break;
            case 3:
                if (FindName("BlinkModeHeader") is System.Windows.Controls.TextBlock blinkHeader)
                {
                    blinkHeader.FontWeight = weight;
                }

                break;
            case 4:
                if (FindName("HotkeyModeHeader") is Controls.LabelWithHelp hotkeyHeader)
                {
                    hotkeyHeader.FontWeight = weight;
                }

                break;
            case 5:
                if (FindName("DiskWriteModeHeader") is Controls.LabelWithHelp diskWriteHeader)
                {
                    diskWriteHeader.FontWeight = weight;
                }

                break;
            case 6:
                if (FindName("DiskReadModeHeader") is Controls.LabelWithHelp diskReadHeader)
                {
                    diskReadHeader.FontWeight = weight;
                }

                break;
        }
    }


}
