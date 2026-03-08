using ModernThinkPadLEDsController.Hardware;
using ModernThinkPadLEDsController.Monitoring;

namespace ModernThinkPadLEDsController.Lighting;

/// <summary>
/// Applies LED behavior from settings and live monitor state.
/// </summary>
public sealed class LedBehaviorService : IDisposable
{
    private readonly LedController _leds;
    private readonly AppSettings _settings;
    private readonly LedBlinkController _blinkMonitor;

    private IReadOnlyDictionary<Led, LedMapping>? _mappings;

    private HotkeyCycleOptions _hotkeyCycleOptions = HotkeyCycleOptions.On | HotkeyCycleOptions.Off;
    private int _hotkeyCycleIndex = -1;

    private bool _isFullscreen;
    private byte _preFullscreenBacklight;
    private bool _hasPreFullscreenBacklight;
    private DiskActivityState _lastDiskState = DiskActivityState.Idle;
    private bool _lastMicrophoneMuted;
    private bool _hasObservedMicrophoneMuteState;
    private bool _lastSpeakerMuted;
    private bool _hasObservedSpeakerMuteState;

    private static readonly Led[] FullscreenManagedLeds =
    [
        Led.Power,
        Led.Mute,
        Led.Microphone,
        Led.FnLock,
        Led.Camera,
    ];

    public LedBehaviorService(LedController leds, AppSettings settings)
    {
        _leds = leds;
        _settings = settings;
        _blinkMonitor = new LedBlinkController(leds, settings.BlinkIntervalMs);
    }

    public void Initialize(IReadOnlyDictionary<Led, LedMapping> mappings)
    {
        _mappings = mappings;
    }

    public void UpdateHotkeyCycleOptions(HotkeyCycleOptions hotkeyCycleOptions)
    {
        _hotkeyCycleOptions = hotkeyCycleOptions;
    }

    public void OnLedModeChanged(Led led)
    {
        ApplyLedStateRespectingFullscreen(led);
    }

    public void OnCustomRegisterIdChanged(Led led)
    {
        ApplyLedStateRespectingFullscreen(led);
    }

    public void OnDiskStateChanged(DiskActivityState state)
    {
        _lastDiskState = state;

        if (_isFullscreen) return;

        foreach (var (led, map) in Mappings)
        {
            if (map.Mode is LedMode.DiskRead or LedMode.DiskWrite)
                ApplyDiskActivityLedState(led, map);
        }
    }

    public void ObserveMicrophoneMuteState(bool isMuted)
    {
        UpdateObservedMuteState(Led.Microphone, isMuted);
    }

    public void ObserveSpeakerMuteState(bool isMuted)
    {
        UpdateObservedMuteState(Led.Mute, isMuted);
    }

    // Mute observations update cached OS state first, then recompute the LED.
    // Only Default mode follows that cached state; explicit user modes are reapplied.
    private void UpdateObservedMuteState(Led led, bool isMuted)
    {
        bool isMicrophone = led == Led.Microphone;

        if (isMicrophone)
        {
            _lastMicrophoneMuted = isMuted;
            _hasObservedMicrophoneMuteState = true;
        }
        else
        {
            _lastSpeakerMuted = isMuted;
            _hasObservedSpeakerMuteState = true;
        }

        ApplyLedStateRespectingFullscreen(led);
    }

    public void OnFullscreenChanged(bool isFullscreen, byte currentBacklight)
    {
        if (!_settings.DimLedsWhenFullscreen) return;
        if (_isFullscreen == isFullscreen) return;

        _isFullscreen = isFullscreen;

        if (isFullscreen)
        {
            _preFullscreenBacklight = currentBacklight;
            _hasPreFullscreenBacklight = true;
            _blinkMonitor.Pause();
            _leds.SetKeyboardBacklightRaw(0);

            foreach (Led led in FullscreenManagedLeds)
            {
                if (ShouldDimLedForFullscreen(led))
                    _leds.SetLed(led, LedState.Off, customId: Mappings[led].CustomRegisterId);
            }
        }
        else
        {
            if (_hasPreFullscreenBacklight)
                _leds.SetKeyboardBacklightRaw(_preFullscreenBacklight);

            foreach (Led led in FullscreenManagedLeds)
                ApplyCurrentLedState(led);

            _blinkMonitor.Resume();
        }
    }

    public void OnHotkeyPressed()
    {
        List<LedState> states = BuildHotkeyCycleStates();
        if (states.Count == 0) return;

        _hotkeyCycleIndex = (_hotkeyCycleIndex + 1) % states.Count;
        if (_isFullscreen) return;

        LedState next = states[_hotkeyCycleIndex];

        foreach (var (led, map) in Mappings)
        {
            if (map.Mode != LedMode.HotkeyControlled)
                continue;

            if (next == LedState.Blink)
            {
                _blinkMonitor.AddBlinkingLed(led, map.CustomRegisterId);
            }
            else
            {
                _blinkMonitor.RemoveBlinkingLed(led);
                _leds.SetLed(led, next, customId: map.CustomRegisterId);
            }
        }
    }

    public void ApplyAll()
    {
        foreach (Led led in Mappings.Keys)
            ApplyCurrentLedState(led);
    }

    public void UpdateBlinkInterval(int intervalMs)
    {
        _blinkMonitor.SetBlinkInterval(intervalMs);
    }

    public void DisableHardwareActivity()
    {
        _blinkMonitor.ClearAll();
    }

    private IReadOnlyDictionary<Led, LedMapping> Mappings =>
        _mappings ?? throw new InvalidOperationException("LedBehaviorService.Initialize must be called before use.");

    private void ApplyLedStateRespectingFullscreen(Led led)
    {
        if (_isFullscreen && ShouldDimLedForFullscreen(led))
        {
            _blinkMonitor.RemoveBlinkingLed(led);
            _leds.SetLed(led, LedState.Off, customId: Mappings[led].CustomRegisterId);
            return;
        }

        ApplyCurrentLedState(led);
    }

    private List<LedState> BuildHotkeyCycleStates()
    {
        List<LedState> states = new(3);
        if ((_hotkeyCycleOptions & HotkeyCycleOptions.On) != 0) states.Add(LedState.On);
        if ((_hotkeyCycleOptions & HotkeyCycleOptions.Off) != 0) states.Add(LedState.Off);
        if ((_hotkeyCycleOptions & HotkeyCycleOptions.Blink) != 0) states.Add(LedState.Blink);
        return states;
    }

    private LedState? GetCurrentHotkeyCycleState()
    {
        List<LedState> states = BuildHotkeyCycleStates();
        if (states.Count == 0) return null;

        if (_hotkeyCycleIndex == -1)
            _hotkeyCycleIndex = 0;

        return states[_hotkeyCycleIndex % states.Count];
    }

    private bool ShouldDimLedForFullscreen(Led led)
    {
        if (Array.IndexOf(FullscreenManagedLeds, led) < 0)
            return false;

        LedMapping map = Mappings[led];
        if (map.Mode != LedMode.Default)
            return true;

        return led switch
        {
            Led.Mute => _hasObservedSpeakerMuteState,
            Led.Microphone => _hasObservedMicrophoneMuteState,
            _ => false,
        };
    }

    private void ApplyCurrentLedState(Led led)
    {
        LedMapping map = Mappings[led];
        _blinkMonitor.RemoveBlinkingLed(led);

        switch (map.Mode)
        {
            case LedMode.Default:
                ApplyDefaultLedState(led, map);
                break;
            case LedMode.On:
                _leds.SetLed(led, LedState.On, customId: map.CustomRegisterId);
                break;
            case LedMode.Off:
                _leds.SetLed(led, LedState.Off, customId: map.CustomRegisterId);
                break;
            case LedMode.Blink:
                _blinkMonitor.AddBlinkingLed(led, map.CustomRegisterId);
                break;
            case LedMode.HotkeyControlled:
                ApplyHotkeyControlledState(led, map);
                break;
            case LedMode.DiskRead:
            case LedMode.DiskWrite:
                ApplyDiskActivityLedState(led, map);
                break;
        }
    }

    private void ApplyDefaultLedState(Led led, LedMapping map)
    {
        switch (led)
        {
            case Led.Mute when _hasObservedSpeakerMuteState:
                _leds.SetLed(led, _lastSpeakerMuted ? LedState.On : LedState.Off, customId: map.CustomRegisterId);
                break;
            case Led.Microphone when _hasObservedMicrophoneMuteState:
                _leds.SetLed(led, _lastMicrophoneMuted ? LedState.On : LedState.Off, customId: map.CustomRegisterId);
                break;
        }
    }

    private void ApplyHotkeyControlledState(Led led, LedMapping map)
    {
        LedState? state = GetCurrentHotkeyCycleState();
        if (!state.HasValue)
            return;

        if (state.Value == LedState.Blink)
        {
            _blinkMonitor.AddBlinkingLed(led, map.CustomRegisterId);
            return;
        }

        _leds.SetLed(led, state.Value, customId: map.CustomRegisterId);
    }

    private void ApplyDiskActivityLedState(Led led, LedMapping map)
    {
        bool reading = _lastDiskState is DiskActivityState.Read or DiskActivityState.ReadWrite;
        bool writing = _lastDiskState is DiskActivityState.Write or DiskActivityState.ReadWrite;
        bool shouldBeOn = map.Mode switch
        {
            LedMode.DiskRead => reading,
            LedMode.DiskWrite => writing,
            _ => false,
        };

        _leds.SetLed(led, shouldBeOn ? LedState.On : LedState.Off, customId: map.CustomRegisterId);
    }

    public void Dispose()
    {
        _blinkMonitor.Dispose();
    }
}
