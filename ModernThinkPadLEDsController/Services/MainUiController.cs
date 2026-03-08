using System.Windows;
using System.Windows.Interop;
using ModernThinkPadLEDsController.ViewModels;

namespace ModernThinkPadLEDsController.Services;

public sealed class MainUiController
{
    private readonly MainViewModel _mainVm;
    private readonly SettingsViewModel _settingsVm;
    private readonly MainWindow _mainWindow;

    public MainUiController(
        MainViewModel mainVm,
        SettingsViewModel settingsVm,
        MainWindow mainWindow)
    {
        _mainVm = mainVm;
        _settingsVm = settingsVm;
        _mainWindow = mainWindow;
    }

    public MainWindow Window => _mainWindow;

    public bool HasDiskModeLeds => _mainVm.HasDiskModeLeds;

    public event Action<bool>? DiskModeLedsChanged
    {
        add => _mainVm.DiskModeLedsChanged += value;
        remove => _mainVm.DiskModeLedsChanged -= value;
    }

    public event EventHandler? SourceInitialized
    {
        add => _mainWindow.SourceInitialized += value;
        remove => _mainWindow.SourceInitialized -= value;
    }

    public void LoadFromSettings()
    {
        _mainVm.LoadFromSettings();
        _settingsVm.LoadFromSettings();
    }

    public void SaveToSettings()
    {
        _mainVm.SaveToSettings();
    }

    public void SetSaveCallbacks(Action callback)
    {
        _mainVm.SetSaveCallback(callback);
        _settingsVm.SetSaveCallback(callback);
    }

    public void SetDiskMonitoringAvailable(bool isAvailable)
    {
        _mainVm.DiskMonitoringAvailable = isAvailable;
    }

    public string FormatHotkeyDisplay(int modifiers, int virtualKey)
    {
        return _mainVm.FormatHotkeyDisplay(modifiers, virtualKey);
    }

    public IntPtr EnsureMainWindowHandle()
    {
        return new WindowInteropHelper(_mainWindow).EnsureHandle();
    }

    public void ShowMainWindow()
    {
        _mainWindow.Show();
    }

    public void ShowAndActivateMainWindow()
    {
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    public void Dispatch(Action action)
    {
        _mainWindow.Dispatcher.Invoke(action);
    }

    public void SetKeyboardBrightnessLevel(int level)
    {
        _settingsVm.KeyboardBrightnessLevel = level;
    }
}
