using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ModernThinkPadLEDsController.Hardware;
using ModernThinkPadLEDsController.Monitoring;
using ModernThinkPadLEDsController.Services;

namespace ModernThinkPadLEDsController.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly ISettingsRuntimeService _runtime;
    private Action? _saveSettingsCallback;

    // Flag to prevent triggering saves during initial load
    private bool _isLoading;


    [ObservableProperty]
    private int _blinkIntervalMs;

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by CommunityToolkit.Mvvm source generator")]
    partial void OnBlinkIntervalMsChanged(int value)
    {
        if (_isLoading)
            return;

        _runtime.UpdateBlinkInterval(value);
        _settings.BlinkIntervalMs = value;
        TriggerSave();
    }


    [ObservableProperty]
    private int _diskPollIntervalMs;

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by CommunityToolkit.Mvvm source generator")]
    partial void OnDiskPollIntervalMsChanged(int value)
    {
        if (_isLoading)
            return;

        _runtime.UpdateDiskPollInterval(value);
        _settings.DiskPollIntervalMs = value;
        TriggerSave();
    }


    [ObservableProperty]
    private bool _rememberKeyboardBacklight;

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by CommunityToolkit.Mvvm source generator")]
    partial void OnRememberKeyboardBacklightChanged(bool value)
    {
        if (_isLoading)
            return;

        _settings.RememberKeyboardBacklight = value;
        TriggerSave();
    }

    [ObservableProperty]
    private int _keyboardBrightnessLevel;

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by CommunityToolkit.Mvvm source generator")]
    partial void OnKeyboardBrightnessLevelChanged(int value)
    {
        // Update warning visibility and percentage display when level changes
        OnPropertyChanged(nameof(ShowKeyboardBrightnessWarning));
        OnPropertyChanged(nameof(KeyboardBrightnessPercent));

        if (_isLoading)
            return;

        _runtime.SetKeyboardBrightnessLevel(value);
        _settings.SavedKeyboardBacklight = value;

        TriggerSave();
    }

    /// <summary>
    /// Returns the keyboard brightness as a percentage (0-100%).
    /// </summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Property depends on KeyboardBrightnessLevel instance state")]
    public int KeyboardBrightnessPercent => KeyboardBrightnessLevel * 100 / 255;

    /// <summary>
    /// Returns true if keyboard brightness is set to a low value that may appear off on some ThinkPads.
    /// </summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Property depends on KeyboardBrightnessLevel instance state")]
    public bool ShowKeyboardBrightnessWarning => KeyboardBrightnessLevel > 0 && KeyboardBrightnessLevel < 64;

    [ObservableProperty]
    private bool _dimLedsWhenFullscreen;

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by CommunityToolkit.Mvvm source generator")]
    partial void OnDimLedsWhenFullscreenChanged(bool value)
    {
        if (_isLoading)
            return;

        _settings.DimLedsWhenFullscreen = value;

        _runtime.SetFullscreenPollingEnabled(value);

        TriggerSave();
    }

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStartupTaskWarning))]
    private string? _startupTaskWarningMessage;

    public bool HasStartupTaskWarning => !string.IsNullOrWhiteSpace(StartupTaskWarningMessage);

    [RelayCommand]
    private void ToggleStartup()
    {
        StartupTaskOperationResult result = _runtime.SetStartupEnabled(StartWithWindows);
        if (!result.Success)
        {
            StartWithWindows = !StartWithWindows;
            StartupTaskWarningMessage = result.ErrorMessage;
            return;
        }

        StartupTaskWarningMessage = null;
    }

    [ObservableProperty]
    private bool _persistSettingsOnChange;

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by CommunityToolkit.Mvvm source generator")]
    partial void OnPersistSettingsOnChangeChanged(bool value)
    {
        if (_isLoading)
            return;

        _settings.PersistSettingsOnChange = value;
        // Don't trigger save for this property change itself to avoid potential issues
    }

    [ObservableProperty]
    private bool _enableHardwareAccess;

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by CommunityToolkit.Mvvm source generator")]
    partial void OnEnableHardwareAccessChanged(bool value)
    {
        if (_isLoading)
            return;

        HardwareAccessPreferenceChangeResult result = _runtime.SetHardwareAccessEnabled(value);
        _settings.EnableHardwareAccess = value;
        DriverStatus = _runtime.GetHardwareAccessStatus();
        HardwareAccessWarningMessage = result.Message;
        TriggerSave();
    }

    [ObservableProperty]
    private string _driverStatus = "PawnIO";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHardwareAccessWarning))]
    private string? _hardwareAccessWarningMessage;

    public bool HasHardwareAccessWarning => !string.IsNullOrWhiteSpace(HardwareAccessWarningMessage);

    public string AppVersion { get; } =
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "—";

    // -------------------------------------------------------------------------
    // Constructor + initialization
    // -------------------------------------------------------------------------

    public SettingsViewModel(
        AppSettings settings,
        ISettingsRuntimeService runtime)
    {
        _settings = settings;
        _runtime = runtime;
    }

    /// <summary>
    /// Load settings from AppSettings without triggering runtime effects.
    /// Startup orchestration applies the effective runtime state explicitly after load.
    /// </summary>
    public void LoadFromSettings()
    {
        _isLoading = true;

        try
        {
            BlinkIntervalMs = _settings.BlinkIntervalMs;
            DiskPollIntervalMs = Math.Max(DiskActivityMonitor.MinIntervalMs, _settings.DiskPollIntervalMs);
            RememberKeyboardBacklight = _settings.RememberKeyboardBacklight;

            // When no brightness has been persisted yet, read the current hardware level
            // to seed the UI without writing the value back to hardware during startup.
            if (_settings.SavedKeyboardBacklight is null && _runtime.TryGetCurrentKeyboardBrightness(out byte currentLevel))
            {
                _settings.SavedKeyboardBacklight = currentLevel;
                KeyboardBrightnessLevel = currentLevel;
            }
            else
            {
                KeyboardBrightnessLevel = _settings.SavedKeyboardBacklight ?? 0;
            }

            DimLedsWhenFullscreen = _settings.DimLedsWhenFullscreen;
            PersistSettingsOnChange = _settings.PersistSettingsOnChange;
            EnableHardwareAccess = _settings.EnableHardwareAccess;
            DriverStatus = _runtime.GetHardwareAccessStatus();
            HardwareAccessWarningMessage = null;
            StartWithWindows = _runtime.IsStartupEnabled();
            StartupTaskWarningMessage = null;
        }
        finally
        {
            _isLoading = false;
        }
    }

    public void SetSaveCallback(Action callback)
    {
        _saveSettingsCallback = callback;
    }

    /// <summary>
    /// Trigger save callback if PersistSettingsOnChange is enabled and not currently loading.
    /// </summary>
    private void TriggerSave()
    {
        if (!_isLoading && PersistSettingsOnChange)
        {
            _saveSettingsCallback?.Invoke();
        }
    }
}
