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
        if (!MainVm.IsRecordingHotkey) return;

        e.Handled = true;

        // Get modifier keys
        var modifiers = Keyboard.Modifiers;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore pure modifier presses
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
        {
            return;
        }

        // Convert WPF modifiers to Win32 modifiers
        int win32Modifiers = 0;
        if ((modifiers & ModifierKeys.Alt) != 0) win32Modifiers |= 0x0001; // MOD_ALT
        if ((modifiers & ModifierKeys.Control) != 0) win32Modifiers |= 0x0002; // MOD_CONTROL
        if ((modifiers & ModifierKeys.Shift) != 0) win32Modifiers |= 0x0004; // MOD_SHIFT
        if ((modifiers & ModifierKeys.Windows) != 0) win32Modifiers |= 0x0008; // MOD_WIN

        // Convert WPF key to virtual key code
        int virtualKey = KeyInterop.VirtualKeyFromKey(key);

        // Update display
        string displayText = FormatHotkeyDisplay(modifiers, key);
        MainVm.HotkeyDisplayText = displayText;
        MainVm.IsRecordingHotkey = false;

        // Update settings and re-register hotkey
        var app = (App)System.Windows.Application.Current;
        bool success = app.UpdateHotkey(win32Modifiers, virtualKey, displayText);

        if (!success)
        {
            // Registration failed - hotkey is already in use
            MainVm.HotkeyWarningMessage = $"⚠ The hotkey '{displayText}' is already in use by Windows or another application. Please choose a different combination.";
            UpdateHotkeyDisplayFromSettings(); // Restore previous hotkey
        }
        else
        {
            // Success - check if warning needed for modifier-less hotkey
            if (modifiers == ModifierKeys.None)
            {
                MainVm.HotkeyWarningMessage = $"⚠ Warning: '{displayText}' has no modifier keys. This may interfere with normal keyboard usage.";
            }
            else
            {
                MainVm.HotkeyWarningMessage = null;
            }
        }

        // Remove focus
        HotkeyTextBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
    }

    private string FormatHotkeyDisplay(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();

        if ((modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        if ((modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");

        parts.Add(key.ToString());

        return string.Join(" + ", parts);
    }

    private void UpdateHotkeyDisplayFromSettings()
    {
        var app = (App)System.Windows.Application.Current;
        MainVm.HotkeyDisplayText = app.GetHotkeyDisplayText();
    }

    private static int ExtractMouseWheelDelta(IntPtr wParam)
    {
        return (short)((long)wParam >> 16);
    }


}
