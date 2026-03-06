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

    // One LedMapping per LED. The View binds to e.g. Power.Mode, RedDot.Mode, etc.
    public LedMapping Power { get; } = new() { Name = "Power" };
    public LedMapping RedDot { get; } = new() { Name = "Red Dot" };
    public LedMapping Microphone { get; } = new() { Name = "Microphone" };
    public LedMapping Sleep { get; } = new() { Name = "Sleep" };
    public LedMapping FnLock { get; } = new() { Name = "Fn Lock" };

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

    // Tracks position within the cycle sequence; -1 means "not yet pressed".
    private int _hotkeyCycleIndex = -1;

    private bool _isFullscreen;
    private KeyboardBacklight _preFullscreenBacklight = KeyboardBacklight.Off;

    // Maps each Led enum value to its LedMapping — used in the apply methods
    // so we don't repeat switch statements.
    private readonly Dictionary<Led, LedMapping> _mappings;

    public MainViewModel(LedController leds)
    {
        _leds = leds;
        Leds = [Power, RedDot, Microphone, Sleep, FnLock];
        _mappings = new Dictionary<Led, LedMapping>
        {
            [Led.Power] = Power,
            [Led.RedDot] = RedDot,
            [Led.Microphone] = Microphone,
            [Led.Sleep] = Sleep,
            [Led.FnLock] = FnLock,
        };

        // When the user changes a mode via the UI, apply it to hardware immediately.
        foreach (var (led, map) in _mappings)
        {
            map.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(LedMapping.Mode)) return;

                switch (map.Mode)
                {
                    case LedMode.On: _leds.SetLed(led, LedState.On); break;
                    case LedMode.Off: _leds.SetLed(led, LedState.Off); break;
                    case LedMode.Blink: _leds.SetLed(led, LedState.Blink); break;
                }

                // Check if disk mode usage changed (none->some or some->none)
                bool hasDiskModesNow = HasDiskModeLeds;
                if (_previousHadDiskModes != hasDiskModesNow)
                {
                    _previousHadDiskModes = hasDiskModesNow;
                    OnPropertyChanged(nameof(HasDiskModeLeds));
                    DiskModeLedsChanged?.Invoke(hasDiskModesNow);
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
            if (map.Mode == LedMode.DiskRead) _leds.SetLed(led, reading ? LedState.On : LedState.Off);
            else if (map.Mode == LedMode.DiskWrite) _leds.SetLed(led, writing ? LedState.On : LedState.Off);
        }
    }

    // Default mode for the Microphone LED means: mirror the system mute state.
    public void OnMicrophoneMuteChanged(bool isMuted)
    {
        if (_isFullscreen) return;
        if (Microphone.Mode == LedMode.Default)
            _leds.SetLed(Led.Microphone, isMuted ? LedState.On : LedState.Off);
    }

    // Called by PowerEventListener when a fullscreen app covers the screen.
    public void OnFullscreenChanged(bool isFullscreen, KeyboardBacklight currentBacklight)
    {
        _isFullscreen = isFullscreen;

        if (isFullscreen)
        {
            _preFullscreenBacklight = currentBacklight;
            _leds.SetKeyboardBacklight(KeyboardBacklight.Off);
            _leds.SetLed(Led.Power, LedState.Off);
            _leds.SetLed(Led.Microphone, LedState.Off);
            _leds.SetLed(Led.FnLock, LedState.Off);
        }
        else
        {
            if (_preFullscreenBacklight != KeyboardBacklight.Off)
                _leds.SetKeyboardBacklight(_preFullscreenBacklight);

            _leds.SetLed(Led.Power, LedState.On);
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
                _leds.SetLed(led, next);
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
                case LedMode.On: _leds.SetLed(led, LedState.On); break;
                case LedMode.Off: _leds.SetLed(led, LedState.Off); break;
                case LedMode.Blink: _leds.SetLed(led, LedState.Blink); break;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Settings round-trip
    // -------------------------------------------------------------------------

    public void LoadFrom(AppSettings s)
    {
        Power.Mode = s.PowerMode;
        RedDot.Mode = s.RedDotMode;
        Microphone.Mode = s.MicrophoneMode;
        Sleep.Mode = s.SleepMode;
        FnLock.Mode = s.FnLockMode;

        HotkeyCycleOn = s.HotkeyCycleOn;
        HotkeyCycleOff = s.HotkeyCycleOff;
        HotkeyCycleBlink = s.HotkeyCycleBlink;

        // Update tracking variable after loading modes
        _previousHadDiskModes = HasDiskModeLeds;
        OnPropertyChanged(nameof(HasDiskModeLeds));
    }

    public void SaveTo(AppSettings s)
    {
        s.PowerMode = Power.Mode;
        s.RedDotMode = RedDot.Mode;
        s.MicrophoneMode = Microphone.Mode;
        s.SleepMode = Sleep.Mode;
        s.FnLockMode = FnLock.Mode;

        s.HotkeyCycleOn = HotkeyCycleOn;
        s.HotkeyCycleOff = HotkeyCycleOff;
        s.HotkeyCycleBlink = HotkeyCycleBlink;
    }
}
