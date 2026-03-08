using ModernThinkPadLEDsController.Presentation.ViewModels;
using System.Windows.Input;
using ModernThinkPadLEDsController.Shell;

namespace ModernThinkPadLEDsController.Presentation.Services;

/// <summary>
/// Owns presentation state shared across the main window.
/// </summary>
public sealed class MainPresentationService
{
    private readonly MainViewModel _mainViewModel;
    private readonly SettingsViewModel _settingsViewModel;

    public MainPresentationService(
        MainViewModel mainViewModel,
        SettingsViewModel settingsViewModel)
    {
        _mainViewModel = mainViewModel;
        _settingsViewModel = settingsViewModel;
    }

    public bool HasDiskModeLeds => _mainViewModel.HasDiskModeLeds;

    public event Action<bool>? DiskModeLedsChanged
    {
        add => _mainViewModel.DiskModeLedsChanged += value;
        remove => _mainViewModel.DiskModeLedsChanged -= value;
    }

    public void LoadFromSettings()
    {
        _mainViewModel.LoadFromSettings();
        _settingsViewModel.LoadFromSettings();
    }

    public void SaveToSettings()
    {
        _mainViewModel.SaveToSettings();
    }

    public void SetSaveCallbacks(Action callback)
    {
        _mainViewModel.SetSaveCallback(callback);
        _settingsViewModel.SetSaveCallback(callback);
    }

    public void SetDiskMonitoringAvailable(bool isAvailable)
    {
        _mainViewModel.DiskMonitoringAvailable = isAvailable;
    }

    public string FormatHotkeyDisplay(HotkeyModifiers modifiers, Key key)
    {
        return _mainViewModel.FormatHotkeyDisplay(modifiers, key);
    }

    public void SetKeyboardBrightnessLevel(int level)
    {
        _settingsViewModel.KeyboardBrightnessLevel = level;
    }
}
