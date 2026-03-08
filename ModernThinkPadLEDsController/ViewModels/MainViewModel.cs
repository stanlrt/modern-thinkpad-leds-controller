using CommunityToolkit.Mvvm.ComponentModel;
using ModernThinkPadLEDsController.Hardware;
using ModernThinkPadLEDsController.Monitoring;

namespace ModernThinkPadLEDsController.ViewModels;

// MainViewModel is the data layer behind MainWindow.
//
// In MVVM (Model-View-ViewModel):
//   Model      = AppSettings, LedController (the real data / hardware)
//   View       = MainWindow.xaml (pure layout, no logic)
//   ViewModel  = this class (exposes properties and commands the View binds to)
public sealed partial class MainViewModel : ObservableObject
{
    private readonly LedController _leds;
    private readonly AppSettings _settings;
    private Action? _saveSettingsCallback;

    // One LedMapping per LED. The View binds to e.g. Power.Mode, RedDot.Mode, etc.
    public LedMapping Power { get; } = new() { Name = "Power" };
    public LedMapping Mute { get; } = new() { Name = "Mute" };
    public LedMapping RedDot { get; } = new() { Name = "Red Dot" };
    public LedMapping Microphone { get; } = new() { Name = "Microphone" };
    public LedMapping Sleep { get; } = new() { Name = "Sleep" };
    public LedMapping FnLock { get; } = new() { Name = "Fn Lock" };
    public LedMapping Camera { get; } = new() { Name = "Camera" };

    // Ordered list for the XAML ItemsControl — same objects as above.
    public IReadOnlyList<LedMapping> Leds { get; }

    // False when Windows disk performance counters are unavailable on this machine.
    // The View uses this to show a warning in the Disk tab.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DiskMonitoringUnavailable))]
    [NotifyPropertyChangedFor(nameof(DiskRadiosEnabled))]
    private bool _diskMonitoringAvailable = true;

    public bool DiskMonitoringUnavailable => !DiskMonitoringAvailable;

    // Disk radios should only be enabled when monitoring is available.
    public bool DiskRadiosEnabled => DiskMonitoringAvailable;

    // True when at least one LED is using disk monitoring modes.
    public bool HasDiskModeLeds => _mappings.Values.Any(m => m.Mode is LedMode.DiskRead or LedMode.DiskWrite);

    // Event fired when the disk mode LED count changes (0 to 1+ or 1+ to 0)
    public event Action<bool>? DiskModeLedsChanged;

    private bool _previousHadDiskModes = false;

    // --- Hotkey cycle config (Win+Shift+K) ---
    // Which states should LEDs in HotkeyControlled mode cycle through?
    [ObservableProperty] private bool _hotkeyCycleOn = true;
    [ObservableProperty] private bool _hotkeyCycleOff = true;
    [ObservableProperty] private bool _hotkeyCycleBlink = false;

    partial void OnHotkeyCycleOnChanged(bool value)
    {
        TriggerSaveIfEnabled();
    }

    partial void OnHotkeyCycleOffChanged(bool value)
    {
        TriggerSaveIfEnabled();
    }

    partial void OnHotkeyCycleBlinkChanged(bool value)
    {
        TriggerSaveIfEnabled();
    }

    // Tracks position within the cycle sequence; -1 means "not yet pressed".
    private int _hotkeyCycleIndex = -1;

    private bool _isFullscreen;
    private KeyboardBacklight _preFullscreenBacklight = KeyboardBacklight.Off;

    // Helper method to get the current hotkey cycle state
    private LedState? GetCurrentHotkeyCycleState()
    {
        var states = new List<LedState>(3);
        if (HotkeyCycleOn) states.Add(LedState.On);
        if (HotkeyCycleOff) states.Add(LedState.Off);
        if (HotkeyCycleBlink) states.Add(LedState.Blink);
        if (states.Count == 0) return null;

        // If never pressed, initialize to first state
        if (_hotkeyCycleIndex == -1)
            _hotkeyCycleIndex = 0;

        return states[_hotkeyCycleIndex % states.Count];
    }

    // Maps each Led enum value to its LedMapping — used in the apply methods
    // so we don't repeat switch statements.
    private readonly Dictionary<Led, LedMapping> _mappings;

    public MainViewModel(LedController leds, AppSettings settings)
    {
        _leds = leds;
        _settings = settings;
        Leds = [Power, Mute, RedDot, Microphone, Sleep, FnLock, Camera];
        _mappings = new Dictionary<Led, LedMapping>
        {
            [Led.Power] = Power,
            [Led.Mute] = Mute,
            [Led.RedDot] = RedDot,
            [Led.Microphone] = Microphone,
            [Led.Sleep] = Sleep,
            [Led.FnLock] = FnLock,
            [Led.Camera] = Camera,
        };

        // When the user changes a mode via the UI, apply it to hardware immediately.
        foreach (var (led, map) in _mappings)
        {
            map.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(LedMapping.Mode))
                {
                    switch (map.Mode)
                    {
                        case LedMode.On: _leds.SetLed(led, LedState.On, customId: map.CustomRegisterId); break;
                        case LedMode.Off: _leds.SetLed(led, LedState.Off, customId: map.CustomRegisterId); break;
                        case LedMode.Blink: _leds.SetLed(led, LedState.Blink, customId: map.CustomRegisterId); break;
                        case LedMode.HotkeyControlled:
                            var state = GetCurrentHotkeyCycleState();
                            if (state.HasValue)
                                _leds.SetLed(led, state.Value, customId: map.CustomRegisterId);
                            break;
                    }

                    // Check if disk mode usage changed (none->some or some->none)
                    bool hasDiskModesNow = HasDiskModeLeds;
                    if (_previousHadDiskModes != hasDiskModesNow)
                    {
                        _previousHadDiskModes = hasDiskModesNow;
                        OnPropertyChanged(nameof(HasDiskModeLeds));
                        DiskModeLedsChanged?.Invoke(hasDiskModesNow);
                    }

                    TriggerSaveIfEnabled();
                }
                else if (e.PropertyName == nameof(LedMapping.CustomRegisterId))
                {
                    TriggerSaveIfEnabled();
                }
            };
        }

        // Initialize the tracking variable
        _previousHadDiskModes = HasDiskModeLeds;
    }

    // -------------------------------------------------------------------------
    // Monitoring event handlers
    // These are called from App.xaml.cs — always on the UI (Dispatcher) thread.
    // -------------------------------------------------------------------------

    public void OnDiskStateChanged(DiskActivityState state)
    {
        if (_isFullscreen) return;

        bool reading = state is DiskActivityState.Read or DiskActivityState.ReadWrite;
        bool writing = state is DiskActivityState.Write or DiskActivityState.ReadWrite;

        foreach (var (led, map) in _mappings)
        {
            if (map.Mode == LedMode.DiskRead) _leds.SetLed(led, reading ? LedState.On : LedState.Off, customId: map.CustomRegisterId);
            else if (map.Mode == LedMode.DiskWrite) _leds.SetLed(led, writing ? LedState.On : LedState.Off, customId: map.CustomRegisterId);
        }
    }

    // Default mode for the Microphone LED means: mirror the system mute state.
    // If the mode is NOT Default but the mute state changes, the user likely pressed
    // the physical mic button — automatically switch back to Default mode so the OS regains control.
    public void OnMicrophoneMuteChanged(bool isMuted)
    {
        if (_isFullscreen) return;

        if (Microphone.Mode == LedMode.Default)
        {
            _leds.SetLed(Led.Microphone, isMuted ? LedState.On : LedState.Off, customId: Microphone.CustomRegisterId);
        }
        else
        {
            // Mute state changed while in non-Default mode — user pressed the physical button.
            // Automatically revert to Default so OS control is restored.
            Microphone.Mode = LedMode.Default;
            _leds.SetLed(Led.Microphone, isMuted ? LedState.On : LedState.Off, customId: Microphone.CustomRegisterId);
        }
    }

    // Default mode for the Mute LED means: mirror the system speaker mute state.
    // If the mode is NOT Default but the mute state changes, the user likely pressed
    // the physical mute button — automatically switch back to Default mode so the OS regains control.
    public void OnSpeakerMuteChanged(bool isMuted)
    {
        if (_isFullscreen) return;

        if (Mute.Mode == LedMode.Default)
        {
            _leds.SetLed(Led.Mute, isMuted ? LedState.On : LedState.Off, customId: Mute.CustomRegisterId);
        }
        else
        {
            // Mute state changed while in non-Default mode — user pressed the physical button.
            // Automatically revert to Default so OS control is restored.
            Mute.Mode = LedMode.Default;
            _leds.SetLed(Led.Mute, isMuted ? LedState.On : LedState.Off, customId: Mute.CustomRegisterId);
        }
    }

    // Called by PowerEventListener when a fullscreen app covers the screen.
    public void OnFullscreenChanged(bool isFullscreen, KeyboardBacklight currentBacklight)
    {
        _isFullscreen = isFullscreen;

        if (isFullscreen)
        {
            _preFullscreenBacklight = currentBacklight;
            _leds.SetKeyboardBacklight(KeyboardBacklight.Off);
            _leds.SetLed(Led.Power, LedState.Off, customId: Power.CustomRegisterId);
            _leds.SetLed(Led.Mute, LedState.Off, customId: Mute.CustomRegisterId);
            _leds.SetLed(Led.Microphone, LedState.Off, customId: Microphone.CustomRegisterId);
            _leds.SetLed(Led.FnLock, LedState.Off, customId: FnLock.CustomRegisterId);
            _leds.SetLed(Led.Camera, LedState.Off, customId: Camera.CustomRegisterId);
        }
        else
        {
            if (_preFullscreenBacklight != KeyboardBacklight.Off)
                _leds.SetKeyboardBacklight(_preFullscreenBacklight);

            _leds.SetLed(Led.Power, LedState.On, customId: Power.CustomRegisterId);
        }
    }

    // Called by HotkeyService when Win+Shift+K is pressed.
    // Advances all HotkeyControlled LEDs to the next state in the configured cycle.
    public void OnHotkeyPressed()
    {
        var states = new List<LedState>(3);
        if (HotkeyCycleOn) states.Add(LedState.On);
        if (HotkeyCycleOff) states.Add(LedState.Off);
        if (HotkeyCycleBlink) states.Add(LedState.Blink);
        if (states.Count == 0) return;

        _hotkeyCycleIndex = (_hotkeyCycleIndex + 1) % states.Count;
        LedState next = states[_hotkeyCycleIndex];

        foreach (var (led, map) in _mappings)
        {
            if (map.Mode == LedMode.HotkeyControlled)
                _leds.SetLed(led, next, customId: map.CustomRegisterId);
        }
    }

    // -------------------------------------------------------------------------
    // Apply static modes to hardware
    // Called once at startup after LoadFrom() so hardware reflects saved settings.
    // -------------------------------------------------------------------------

    public void ApplyAll()
    {
        foreach (var (led, map) in _mappings)
        {
            switch (map.Mode)
            {
                case LedMode.On: _leds.SetLed(led, LedState.On, customId: map.CustomRegisterId); break;
                case LedMode.Off: _leds.SetLed(led, LedState.Off, customId: map.CustomRegisterId); break;
                case LedMode.Blink: _leds.SetLed(led, LedState.Blink, customId: map.CustomRegisterId); break;
                case LedMode.HotkeyControlled:
                    var state = GetCurrentHotkeyCycleState();
                    if (state.HasValue)
                        _leds.SetLed(led, state.Value, customId: map.CustomRegisterId);
                    break;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Settings round-trip
    // -------------------------------------------------------------------------

    public void LoadFromSettings()
    {
        Power.Mode = _settings.PowerMode;
        Mute.Mode = _settings.MuteMode;
        RedDot.Mode = _settings.RedDotMode;
        Microphone.Mode = _settings.MicrophoneMode;
        Sleep.Mode = _settings.SleepMode;
        FnLock.Mode = _settings.FnLockMode;
        Camera.Mode = _settings.CameraMode;

        Power.CustomRegisterId = _settings.PowerCustomId;
        Mute.CustomRegisterId = _settings.MuteCustomId;
        RedDot.CustomRegisterId = _settings.RedDotCustomId;
        Microphone.CustomRegisterId = _settings.MicrophoneCustomId;
        Sleep.CustomRegisterId = _settings.SleepCustomId;
        FnLock.CustomRegisterId = _settings.FnLockCustomId;
        Camera.CustomRegisterId = _settings.CameraCustomId;

        HotkeyCycleOn = _settings.HotkeyCycleOn;
        HotkeyCycleOff = _settings.HotkeyCycleOff;
        HotkeyCycleBlink = _settings.HotkeyCycleBlink;

        // Update tracking variable after loading modes
        _previousHadDiskModes = HasDiskModeLeds;
        OnPropertyChanged(nameof(HasDiskModeLeds));
    }

    public void SaveToSettings()
    {
        _settings.PowerMode = Power.Mode;
        _settings.MuteMode = Mute.Mode;
        _settings.RedDotMode = RedDot.Mode;
        _settings.MicrophoneMode = Microphone.Mode;
        _settings.SleepMode = Sleep.Mode;
        _settings.FnLockMode = FnLock.Mode;
        _settings.CameraMode = Camera.Mode;

        _settings.PowerCustomId = Power.CustomRegisterId;
        _settings.MuteCustomId = Mute.CustomRegisterId;
        _settings.RedDotCustomId = RedDot.CustomRegisterId;
        _settings.MicrophoneCustomId = Microphone.CustomRegisterId;
        _settings.SleepCustomId = Sleep.CustomRegisterId;
        _settings.FnLockCustomId = FnLock.CustomRegisterId;
        _settings.CameraCustomId = Camera.CustomRegisterId;

        _settings.HotkeyCycleOn = HotkeyCycleOn;
        _settings.HotkeyCycleOff = HotkeyCycleOff;
        _settings.HotkeyCycleBlink = HotkeyCycleBlink;
    }

    // -------------------------------------------------------------------------
    // Settings persistence
    // -------------------------------------------------------------------------

    private void TriggerSaveIfEnabled()
    {
        if (_settings.PersistSettingsOnChange)
        {
            SaveToSettings();
            _saveSettingsCallback?.Invoke();
        }
    }

    public void SetSaveCallback(Action callback)
    {
        _saveSettingsCallback = callback;
    }
}
