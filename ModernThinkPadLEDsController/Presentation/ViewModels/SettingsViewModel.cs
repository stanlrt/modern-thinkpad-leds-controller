using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ModernThinkPadLEDsController.Hardware;
using ModernThinkPadLEDsController.Logging;
using ModernThinkPadLEDsController.Settings;
using ModernThinkPadLEDsController.Shell;
using Serilog;
using Serilog.Events;

namespace ModernThinkPadLEDsController.Presentation.ViewModels;

/// <summary>
/// Exposes application settings to the settings UI.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly ISettingsRuntimeService _runtime;
    private Action? _saveSettingsCallback;
    private Action? _updateRuntimeStateCallback;

    /// <summary>
    /// Flag to prevent triggering saves during initial load
    /// </summary>
    private bool _isLoading;


    [ObservableProperty]
    private int _blinkIntervalMs;

    partial void OnBlinkIntervalMsChanged(int value)
    {
        if (_isLoading)
            return;

        _runtime.UpdateBlinkInterval(value);
        _settings.BlinkIntervalMs = value;
        TriggerSave();
    }


    [ObservableProperty]
    private int _ledReapplyIntervalMs;

    partial void OnLedReapplyIntervalMsChanged(int value)
    {
        if (_isLoading)
            return;

        _runtime.UpdateLedReapplyInterval(value);
        TriggerSave();
    }


    [ObservableProperty]
    private int _diskPollIntervalMs;

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

    partial void OnRememberKeyboardBacklightChanged(bool value)
    {
        if (_isLoading)
            return;

        _settings.RememberKeyboardBacklight = value;
        TriggerSave();
    }

    [ObservableProperty]
    private bool _enforceKeyboardBacklight;

    partial void OnEnforceKeyboardBacklightChanged(bool value)
    {
        if (_isLoading)
            return;

        _settings.EnforceKeyboardBacklight = value;
        OnPropertyChanged(nameof(ShowKeyboardBrightnessSlider));
        _updateRuntimeStateCallback?.Invoke();
        TriggerSave();
    }

    [ObservableProperty]
    private int _keyboardBrightnessLevel;

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
    public int KeyboardBrightnessPercent => KeyboardBrightnessLevel * 100 / 255;

    /// <summary>
    /// Returns true if keyboard brightness slider should be shown (when enforcement is enabled).
    /// </summary>
    public bool ShowKeyboardBrightnessSlider => EnforceKeyboardBacklight;

    /// <summary>
    /// Returns true if keyboard brightness is set to a low value that may appear off on some ThinkPads.
    /// </summary>
    public bool ShowKeyboardBrightnessWarning => KeyboardBrightnessLevel > 0 && KeyboardBrightnessLevel < 64;

    [ObservableProperty]
    private bool _dimLedsWhenFullscreen;

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

    partial void OnPersistSettingsOnChangeChanged(bool value)
    {
        if (_isLoading)
            return;

        _settings.PersistSettingsOnChange = value;
        // Don't trigger save for this property change itself to avoid potential issues
    }

    [ObservableProperty]
    private bool _enableHardwareAccess;

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

    [ObservableProperty]
    private LogEventLevel _logLevel = AppSettingsDefaults.LOG_LEVEL;

    partial void OnLogLevelChanged(LogEventLevel value)
    {
        if (_isLoading)
            return;

        LoggingConfiguration.SetLogLevel(value);
        _settings.LogLevel = value;
        TriggerSave();
    }

    /// <summary>
    /// Available log levels sorted from most detailed to least detailed.
    /// </summary>
    public LogEventLevel[] AvailableLogLevels { get; } = { LogEventLevel.Verbose, LogEventLevel.Debug, LogEventLevel.Information, LogEventLevel.Warning, LogEventLevel.Error };

    [RelayCommand]
    private void OpenLogFolder()
    {
        try
        {
            LoggingConfiguration.OpenLogFolder();
        }
        catch (Exception ex)
        {
            // Log the error and show a warning message to the user
            Log.Error(ex, "Failed to open log folder");
            HardwareAccessWarningMessage = "Failed to open log folder: " + ex.Message;
        }
    }

    public string AppVersion { get; } =
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "—";


    public int MinBlinkIntervalMs => AppSettingsDefaults.MIN_BLINK_INTERVAL_MS;
    public int MaxBlinkIntervalMs => AppSettingsDefaults.MAX_BLINK_INTERVAL_MS;

    public int MinLedReapplyIntervalMs => AppSettingsDefaults.MIN_LED_REAPPLY_INTERVAL_MS;
    public int MaxLedReapplyIntervalMs => AppSettingsDefaults.MAX_LED_REAPPLY_INTERVAL_MS;

    public int MinDiskPollIntervalMs => AppSettingsDefaults.MIN_DISK_POLL_INTERVAL_MS;
    public int MaxDiskPollIntervalMs => AppSettingsDefaults.MAX_DISK_POLL_INTERVAL_MS;

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
            LedReapplyIntervalMs = _settings.LedReapplyIntervalMs;
            DiskPollIntervalMs = Math.Max(AppSettingsDefaults.MIN_DISK_POLL_INTERVAL_MS, _settings.DiskPollIntervalMs);
            RememberKeyboardBacklight = _settings.RememberKeyboardBacklight;
            EnforceKeyboardBacklight = _settings.EnforceKeyboardBacklight;

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
            LogLevel = _settings.LogLevel;
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
    public void SetUpdateRuntimeStateCallback(Action callback)
    {
        _updateRuntimeStateCallback = callback;
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
