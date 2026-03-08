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
    private readonly DiskActivityMonitor _disk;
    private readonly LedController _leds;
    private readonly MainViewModel _mainVm;
    private Action? _saveSettingsCallback;

    // Flag to prevent triggering saves during initial load
    private bool _isLoading;


    [ObservableProperty]
    private int _blinkIntervalMs;

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by CommunityToolkit.Mvvm source generator")]
    partial void OnBlinkIntervalMsChanged(int value)
    {
        _mainVm.UpdateBlinkInterval(value);
        _settings.BlinkIntervalMs = value;
        TriggerSave();
    }


    [ObservableProperty]
    private int _hddPollIntervalMs;

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by CommunityToolkit.Mvvm source generator")]
    partial void OnHddPollIntervalMsChanged(int value)
    {
        _disk.UpdateInterval(value);
        _settings.HddPollIntervalMs = value;
        TriggerSave();
    }


    [ObservableProperty]
    private bool _rememberKeyboardBacklight;

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by CommunityToolkit.Mvvm source generator")]
    partial void OnRememberKeyboardBacklightChanged(bool value)
    {
        _settings.RememberKeyboardBacklight = value;
        TriggerSave();
    }

    [ObservableProperty]
    private bool _dimLedsWhenFullscreen;

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by CommunityToolkit.Mvvm source generator")]
    partial void OnDimLedsWhenFullscreenChanged(bool value)
    {
        _settings.DimLedsWhenFullscreen = value;
        TriggerSave();
    }

    [RelayCommand] private void SetBacklightOff() => _leds.SetKeyboardBacklight(KeyboardBacklight.Off);
    [RelayCommand] private void SetBacklightLow() => _leds.SetKeyboardBacklight(KeyboardBacklight.Low);
    [RelayCommand] private void SetBacklightHigh() => _leds.SetKeyboardBacklight(KeyboardBacklight.High);

    [ObservableProperty]
    private bool _startWithWindows;

    [RelayCommand]
    private void ToggleStartup()
    {
        if (StartWithWindows)
        {
            bool ok = StartupTaskManager.Register(Environment.ProcessPath ?? "");
            if (!ok) StartWithWindows = false;
        }
        else
        {
            StartupTaskManager.Unregister();
        }
    }

    [ObservableProperty]
    private bool _persistSettingsOnChange;

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by CommunityToolkit.Mvvm source generator")]
    partial void OnPersistSettingsOnChangeChanged(bool value)
    {
        _settings.PersistSettingsOnChange = value;
        // Don't trigger save for this property change itself to avoid potential issues
    }
    public string DriverStatus { get; } = "PawnIO";

    public string AppVersion { get; } =
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "—";

    // -------------------------------------------------------------------------
    // Constructor + initialization
    // -------------------------------------------------------------------------

    public SettingsViewModel(
        AppSettings settings,
        DiskActivityMonitor disk,
        LedController leds,
        MainViewModel mainVm)
    {
        _settings = settings;
        _disk = disk;
        _leds = leds;
        _mainVm = mainVm;
    }

    /// <summary>
    /// Load settings from AppSettings. Sets _isLoading flag to prevent
    /// triggering saves during initialization.
    /// </summary>
    public void LoadFromSettings()
    {
        _isLoading = true;

        // Use property setters - partial methods will sync to _settings but won't trigger save
        BlinkIntervalMs = _settings.BlinkIntervalMs;
        HddPollIntervalMs = _settings.HddPollIntervalMs;
        RememberKeyboardBacklight = _settings.RememberKeyboardBacklight;
        DimLedsWhenFullscreen = _settings.DimLedsWhenFullscreen;
        PersistSettingsOnChange = _settings.PersistSettingsOnChange;
        StartWithWindows = StartupTaskManager.IsRegistered();

        _isLoading = false;
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
