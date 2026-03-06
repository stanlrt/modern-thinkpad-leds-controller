using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ModernThinkPadLEDsController.Hardware;
using ModernThinkPadLEDsController.Monitoring;
using ModernThinkPadLEDsController.Services;

namespace ModernThinkPadLEDsController.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings              _settings;
    private readonly DiskActivityMonitor      _disk;
    private readonly KeyLockMonitor           _keys;
    private readonly KeyboardBacklightMonitor _backlight;
    private readonly PowerEventListener       _power;
    private readonly LedController            _leds;

    // -------------------------------------------------------------------------
    // Timing
    // -------------------------------------------------------------------------

    [ObservableProperty]
    private int _hddPollIntervalMs;

    partial void OnHddPollIntervalMsChanged(int value)
    {
        _disk.UpdateInterval(value);
        _settings.HddPollIntervalMs = value;
    }

    // -------------------------------------------------------------------------
    // Keyboard backlight
    // -------------------------------------------------------------------------

    [ObservableProperty]
    private bool _rememberKeyboardBacklight;

    [ObservableProperty]
    private KeyboardBacklight _keyboardBacklightLevel;

    [ObservableProperty]
    private bool _dimLedsWhenFullscreen;

    partial void OnDimLedsWhenFullscreenChanged(bool value)
    {
        if (value) _power.StartFullscreenPolling();
        else       _power.StopFullscreenPolling();
        _settings.DimLedsWhenFullscreen = value;
    }

    // -------------------------------------------------------------------------
    // Manual keyboard backlight buttons
    // -------------------------------------------------------------------------

    [RelayCommand] private void SetBacklightOff()  => _leds.SetKeyboardBacklight(KeyboardBacklight.Off);
    [RelayCommand] private void SetBacklightLow()  => _leds.SetKeyboardBacklight(KeyboardBacklight.Low);
    [RelayCommand] private void SetBacklightHigh() => _leds.SetKeyboardBacklight(KeyboardBacklight.High);

    // -------------------------------------------------------------------------
    // Startup with Windows
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // Driver info (read-only, shown in the UI for diagnostics)
    // -------------------------------------------------------------------------

    public string DriverStatus { get; } = "InpOut x64 (WHQL-signed)";

    // -------------------------------------------------------------------------
    // Constructor + settings round-trip
    // -------------------------------------------------------------------------

    public SettingsViewModel(
        AppSettings              settings,
        DiskActivityMonitor      disk,
        KeyLockMonitor           keys,
        KeyboardBacklightMonitor backlight,
        PowerEventListener       power,
        LedController            leds)
    {
        _settings  = settings;
        _disk      = disk;
        _keys      = keys;
        _backlight = backlight;
        _power     = power;
        _leds      = leds;
    }

    public void LoadFrom(AppSettings s)
    {
        // Setting properties through the generated setters so partial callbacks fire.
        HddPollIntervalMs        = s.HddPollIntervalMs;
        RememberKeyboardBacklight = s.RememberKeyboardBacklight;
        DimLedsWhenFullscreen    = s.DimLedsWhenFullscreen;
        StartWithWindows         = StartupTaskManager.IsRegistered();
    }

    public void SaveTo(AppSettings s)
    {
        s.HddPollIntervalMs        = HddPollIntervalMs;
        s.RememberKeyboardBacklight = RememberKeyboardBacklight;
        s.DimLedsWhenFullscreen    = DimLedsWhenFullscreen;
    }
}
