using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ModernThinkPadLEDsController.Hardware;
using ModernThinkPadLEDsController.Monitoring;

namespace ModernThinkPadLEDsController.ViewModels;

// MainViewModel is the data layer behind MainWindow.
//
// In MVVM (Model-View-ViewModel):
//   Model      = AppSettings, LedController (the real data / hardware)
//   View       = MainWindow.xaml (pure layout, no logic)
//   ViewModel  = this class (exposes properties and commands the View binds to)
//
// The View never calls hardware code directly. It binds to properties here,
// and the ViewModel translates user intent into LedController calls.
public sealed partial class MainViewModel : ObservableObject
{
    private readonly LedController _leds;

    // One LedMapping per LED. The View binds to e.g. Power.HddRead, RedDot.Invert, etc.
    public LedMapping Power      { get; } = new();
    public LedMapping RedDot     { get; } = new();
    public LedMapping Microphone { get; } = new();
    public LedMapping Sleep      { get; } = new();
    public LedMapping FnLock     { get; } = new();

    // Disk monitoring is available only if Windows perf counters are active.
    [ObservableProperty] private bool _diskMonitoringAvailable = true;
    [ObservableProperty] private bool _diskMonitoringEnabled   = true;

    // KeyMonitoringEnabled drives whether the hook is active.
    [ObservableProperty] private bool _keyMonitoringEnabled = true;
    [ObservableProperty] private bool _micMonitoringEnabled  = true;

    // Tracks the last LED state set by each source so fullscreen restore works.
    private bool _isFullscreen;
    private KeyboardBacklight _preFullscreenBacklight = KeyboardBacklight.Off;

    // Maps each Led enum value to its LedMapping — used in the apply methods
    // so we don't repeat switch statements.
    private IReadOnlyDictionary<Led, LedMapping> Mappings => _mappings;
    private readonly Dictionary<Led, LedMapping> _mappings;

    public MainViewModel(LedController leds)
    {
        _leds = leds;
        _mappings = new Dictionary<Led, LedMapping>
        {
            [Led.Power]      = Power,
            [Led.RedDot]     = RedDot,
            [Led.Microphone] = Microphone,
            [Led.Sleep]      = Sleep,
            [Led.FnLock]     = FnLock,
        };
    }

    // -------------------------------------------------------------------------
    // Monitoring event handlers
    // These are called from App.xaml.cs — always on the UI (Dispatcher) thread.
    // -------------------------------------------------------------------------

    public void OnDiskStateChanged(DiskActivityState state)
    {
        if (!DiskMonitoringEnabled || _isFullscreen) return;

        bool reading = state is DiskActivityState.Read or DiskActivityState.ReadWrite;
        bool writing = state is DiskActivityState.Write or DiskActivityState.ReadWrite;

        foreach (var (led, map) in _mappings)
        {
            if (!map.HddRead && !map.HddWrite) continue;  // LED not mapped to disk — don't touch it
            bool active = (reading && map.HddRead) || (writing && map.HddWrite);
            _leds.SetLed(led, active ? LedState.On : LedState.Off, invertState: map.Invert);
        }
    }

    public void OnCapsLockChanged(bool isOn)
    {
        if (!KeyMonitoringEnabled) return;
        foreach (var (led, map) in _mappings)
        {
            if (!map.CapsLock) continue;
            _leds.SetLed(led, isOn ? LedState.On : LedState.Off, invertState: map.Invert);
        }
    }

    public void OnNumLockChanged(bool isOn)
    {
        if (!KeyMonitoringEnabled) return;
        foreach (var (led, map) in _mappings)
        {
            if (!map.NumLock) continue;
            _leds.SetLed(led, isOn ? LedState.On : LedState.Off, invertState: map.Invert);
        }
    }

    // Mic muted → LED on; mic active → LED off.
    // Only runs when mic monitoring is enabled — otherwise manual LED Control commands stick.
    public void OnMicrophoneMuteChanged(bool isMuted)
    {
        if (!MicMonitoringEnabled || _isFullscreen) return;
        _leds.SetLed(Led.Microphone, isMuted ? LedState.On : LedState.Off);
    }

    // Called by PowerEventListener when a fullscreen app covers the screen.
    // Dims the keyboard backlight and turns off the cosmetic LEDs.
    // invertState: false — the fullscreen dim bypasses user inversion (same as legacy).
    public void OnFullscreenChanged(bool isFullscreen, KeyboardBacklight currentBacklight)
    {
        _isFullscreen = isFullscreen;

        if (isFullscreen)
        {
            _preFullscreenBacklight = currentBacklight;
            _leds.SetKeyboardBacklight(KeyboardBacklight.Off);
            _leds.SetLed(Led.Power,      LedState.Off);
            _leds.SetLed(Led.Microphone, LedState.Off);
            _leds.SetLed(Led.FnLock,     LedState.Off);
        }
        else
        {
            // Restore keyboard backlight only if it wasn't already off before fullscreen.
            if (_preFullscreenBacklight != KeyboardBacklight.Off)
                _leds.SetKeyboardBacklight(_preFullscreenBacklight);

            _leds.SetLed(Led.Power, LedState.On);
        }
    }

    // -------------------------------------------------------------------------
    // Manual LED commands — bound to the On / Off / Blink buttons in the UI.
    // [RelayCommand] generates a public PowerOnCommand, PowerOffCommand, etc.
    // that implement ICommand (what WPF buttons require).
    // -------------------------------------------------------------------------

    [RelayCommand] private void PowerOn()      => _leds.SetLed(Led.Power,      LedState.On,    Power.Invert);
    [RelayCommand] private void PowerOff()     => _leds.SetLed(Led.Power,      LedState.Off,   Power.Invert);
    [RelayCommand] private void PowerBlink()   => _leds.SetLed(Led.Power,      LedState.Blink);

    [RelayCommand] private void RedDotOn()     => _leds.SetLed(Led.RedDot,     LedState.On,    RedDot.Invert);
    [RelayCommand] private void RedDotOff()    => _leds.SetLed(Led.RedDot,     LedState.Off,   RedDot.Invert);
    [RelayCommand] private void RedDotBlink()  => _leds.SetLed(Led.RedDot,     LedState.Blink);

    [RelayCommand] private void MicOn()        => _leds.SetLed(Led.Microphone, LedState.On,    Microphone.Invert);
    [RelayCommand] private void MicOff()       => _leds.SetLed(Led.Microphone, LedState.Off,   Microphone.Invert);
    [RelayCommand] private void MicBlink()     => _leds.SetLed(Led.Microphone, LedState.Blink);

    [RelayCommand] private void SleepOn()      => _leds.SetLed(Led.Sleep,      LedState.On,    Sleep.Invert);
    [RelayCommand] private void SleepOff()     => _leds.SetLed(Led.Sleep,      LedState.Off,   Sleep.Invert);
    [RelayCommand] private void SleepBlink()   => _leds.SetLed(Led.Sleep,      LedState.Blink);

    [RelayCommand] private void FnLockOn()     => _leds.SetLed(Led.FnLock,     LedState.On,    FnLock.Invert);
    [RelayCommand] private void FnLockOff()    => _leds.SetLed(Led.FnLock,     LedState.Off,   FnLock.Invert);
    [RelayCommand] private void FnLockBlink()  => _leds.SetLed(Led.FnLock,     LedState.Blink);

    // -------------------------------------------------------------------------
    // Settings round-trip
    // -------------------------------------------------------------------------

    public void LoadFrom(AppSettings s)
    {
        Power.HddRead   = s.HddReadPower;      Power.HddWrite   = s.HddWritePower;
        Power.CapsLock  = s.CapsLockPower;     Power.NumLock    = s.NumLockPower;
        Power.Invert    = s.InvertPower;

        RedDot.HddRead  = s.HddReadRedDot;     RedDot.HddWrite  = s.HddWriteRedDot;
        RedDot.CapsLock = s.CapsLockRedDot;    RedDot.NumLock   = s.NumLockRedDot;
        RedDot.Invert   = s.InvertRedDot;

        Microphone.HddRead  = s.HddReadMicrophone;  Microphone.HddWrite  = s.HddWriteMicrophone;
        Microphone.CapsLock = s.CapsLockMicrophone; Microphone.NumLock   = s.NumLockMicrophone;
        Microphone.Invert   = s.InvertMicrophone;

        Sleep.HddRead   = s.HddReadSleep;      Sleep.HddWrite   = s.HddWriteSleep;
        Sleep.CapsLock  = s.CapsLockSleep;     Sleep.NumLock    = s.NumLockSleep;
        Sleep.Invert    = s.InvertSleep;

        FnLock.HddRead  = s.HddReadFn;         FnLock.HddWrite  = s.HddWriteFn;
        FnLock.CapsLock = s.CapsLockFn;        FnLock.NumLock   = s.NumLockFn;
        FnLock.Invert   = s.InvertFn;

        DiskMonitoringEnabled = !s.DisableDiskMonitoring;
        KeyMonitoringEnabled  = !s.DisableKeyMonitoring;
        MicMonitoringEnabled  = !s.DisableMicMonitoring;
    }

    public void SaveTo(AppSettings s)
    {
        s.HddReadPower      = Power.HddRead;      s.HddWritePower      = Power.HddWrite;
        s.CapsLockPower     = Power.CapsLock;     s.NumLockPower       = Power.NumLock;
        s.InvertPower       = Power.Invert;

        s.HddReadRedDot     = RedDot.HddRead;     s.HddWriteRedDot     = RedDot.HddWrite;
        s.CapsLockRedDot    = RedDot.CapsLock;    s.NumLockRedDot      = RedDot.NumLock;
        s.InvertRedDot      = RedDot.Invert;

        s.HddReadMicrophone = Microphone.HddRead; s.HddWriteMicrophone = Microphone.HddWrite;
        s.CapsLockMicrophone= Microphone.CapsLock;s.NumLockMicrophone  = Microphone.NumLock;
        s.InvertMicrophone  = Microphone.Invert;

        s.HddReadSleep      = Sleep.HddRead;      s.HddWriteSleep      = Sleep.HddWrite;
        s.CapsLockSleep     = Sleep.CapsLock;     s.NumLockSleep       = Sleep.NumLock;
        s.InvertSleep       = Sleep.Invert;

        s.HddReadFn         = FnLock.HddRead;     s.HddWriteFn         = FnLock.HddWrite;
        s.CapsLockFn        = FnLock.CapsLock;    s.NumLockFn          = FnLock.NumLock;
        s.InvertFn          = FnLock.Invert;

        s.DisableDiskMonitoring = !DiskMonitoringEnabled;
        s.DisableKeyMonitoring  = !KeyMonitoringEnabled;
        s.DisableMicMonitoring  = !MicMonitoringEnabled;
    }
}
