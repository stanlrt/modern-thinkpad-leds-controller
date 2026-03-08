using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
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
    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const double SCROLL_SENSITIVITY = 3.0;
    private bool _horizontalMouseWheelHookAttached;

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    public MainViewModel MainVm { get; }
    public SettingsViewModel SettingsVm { get; }

    public MainWindow(MainViewModel mainVm, SettingsViewModel settingsVm)
    {
        MainVm = mainVm;
        SettingsVm = settingsVm;
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
            return;

        IntPtr handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
            return;

        HwndSource? source = HwndSource.FromHwnd(handle);
        if (source is null)
            return;

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
        var modifiers = GetActiveModifiers();
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
        HotkeyModifiers win32Modifiers = ToWin32Modifiers(modifiers);

        // Update display
        string displayText = FormatHotkeyDisplay(modifiers, key);
        MainVm.HotkeyDisplayText = displayText;
        MainVm.IsRecordingHotkey = false;

        // Update settings and re-register hotkey
        var app = (App)System.Windows.Application.Current;
        bool success = app.UpdateHotkey(win32Modifiers, key, displayText);

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

    private static string FormatHotkeyDisplay(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();

        if ((modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        if ((modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");

        parts.Add(key == Key.Capital ? "CapsLock" : key.ToString());

        return string.Join(" + ", parts);
    }

    private void UpdateHotkeyDisplayFromSettings()
    {
        var app = (App)System.Windows.Application.Current;
        MainVm.HotkeyDisplayText = app.GetHotkeyDisplayText();
    }

    private static ModifierKeys GetActiveModifiers()
    {
        ModifierKeys modifiers = ModifierKeys.None;

        if (IsKeyDown(VK_CONTROL)) modifiers |= ModifierKeys.Control;
        if (IsKeyDown(VK_MENU)) modifiers |= ModifierKeys.Alt;
        if (IsKeyDown(VK_SHIFT)) modifiers |= ModifierKeys.Shift;
        if (IsKeyDown(VK_LWIN) || IsKeyDown(VK_RWIN)) modifiers |= ModifierKeys.Windows;

        return modifiers;
    }

    private static HotkeyModifiers ToWin32Modifiers(ModifierKeys modifiers)
    {
        HotkeyModifiers win32Modifiers = HotkeyModifiers.None;

        if ((modifiers & ModifierKeys.Alt) != 0) win32Modifiers |= HotkeyModifiers.Alt;
        if ((modifiers & ModifierKeys.Control) != 0) win32Modifiers |= HotkeyModifiers.Control;
        if ((modifiers & ModifierKeys.Shift) != 0) win32Modifiers |= HotkeyModifiers.Shift;
        if ((modifiers & ModifierKeys.Windows) != 0) win32Modifiers |= HotkeyModifiers.Win;

        return win32Modifiers;
    }

    private static bool IsKeyDown(int virtualKey)
    {
        const int KEY_DOWN_MASK = 0x8000;
        return (GetKeyState(virtualKey) & KEY_DOWN_MASK) != 0;
    }

    private static int ExtractMouseWheelDelta(IntPtr wParam)
    {
        const int SHIFT_TO_HIGH_WORD = 16;
        return (short)((long)wParam >> SHIFT_TO_HIGH_WORD);
    }


}
