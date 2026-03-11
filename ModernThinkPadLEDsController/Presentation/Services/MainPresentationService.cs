using System.Windows.Input;
using ModernThinkPadLEDsController.Presentation.ViewModels;
using ModernThinkPadLEDsController.Settings;
using ModernThinkPadLEDsController.Shell;

namespace ModernThinkPadLEDsController.Presentation.Services;

/// <summary>
/// Owns presentation state shared across the main window.
/// </summary>
public sealed class MainPresentationService
{
    private readonly MainViewModel _mainViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly ISettingsRuntimeService _runtime;

    public MainPresentationService(
        MainViewModel mainViewModel,
        SettingsViewModel settingsViewModel,
        ISettingsRuntimeService runtime)
    {
        _mainViewModel = mainViewModel;
        _settingsViewModel = settingsViewModel;
        _runtime = runtime;
    }

    public bool HasDiskModeLeds => _mainViewModel.HasDiskModeLeds;

    public event Action<bool>? DiskModeLedsChanged
    {
        add => _mainViewModel.DiskModeLedsChanged += value;
        remove => _mainViewModel.DiskModeLedsChanged -= value;
    }

    public event Action? LedConfigurationChanged
    {
        add => _mainViewModel.LedConfigurationChanged += value;
        remove => _mainViewModel.LedConfigurationChanged -= value;
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
        _settingsViewModel.SetUpdateRuntimeStateCallback(TriggerLedConfigurationChanged);
    }

    public void SetDiskMonitoringAvailable(bool isAvailable)
    {
        _mainViewModel.DiskMonitoringAvailable = isAvailable;
    }

    public string FormatHotkeyDisplay(HotkeyBinding hotkey)
    {
        return _mainViewModel.FormatHotkeyDisplay(hotkey);
    }

    public void SetKeyboardBrightnessLevel(int level, bool forceWrite = false)
    {
        if (forceWrite)
        {
            // Bypass ViewModel to avoid triggering change handler
            // Call runtime service directly with forceWrite to bypass cache
            _runtime.SetKeyboardBrightnessLevel(level, forceWrite: true);
        }
        else
        {
            // Use normal path through ViewModel for UI-initiated changes
            _settingsViewModel.KeyboardBrightnessLevel = level;
        }
    }

    /// <summary>
    /// Triggers LED configuration changed event to update reapply loop state.
    /// </summary>
    public void TriggerLedConfigurationChanged()
    {
        _mainViewModel.TriggerConfigurationChanged();
    }
}
