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
    private readonly ILedBlinkController _blinkMonitor;
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

    private static readonly Led[] _fullscreenManagedLeds =
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

    /// <summary>
    /// Internal constructor for testing: accepts an injected blink controller.
    /// </summary>
    internal LedBehaviorService(LedController leds, AppSettings settings, ILedBlinkController blinkMonitor)
    {
        _leds = leds;
        _settings = settings;
        _blinkMonitor = blinkMonitor;
    }

    public void Initialize(IReadOnlyDictionary<Led, LedMapping> mappings)
    {
        Mappings = mappings;
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

        if (_isFullscreen)
        {
            return;
        }

        foreach ((Led led, LedMapping? map) in Mappings)
        {
            if (map.Mode is LedMode.DiskRead or LedMode.DiskWrite)
            {
                ApplyDiskActivityLedState(led, map);
            }
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

    /// <summary>
    /// Mute observations update cached OS state first, then recompute the LED.
    /// Only Default mode follows that cached state; explicit user modes are reapplied.
    /// </summary>
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
        if (!_settings.DimLedsWhenFullscreen)
        {
            return;
        }

        if (_isFullscreen == isFullscreen)
        {
            return;
        }

        _isFullscreen = isFullscreen;

        if (isFullscreen)
        {
            _preFullscreenBacklight = currentBacklight;
            _hasPreFullscreenBacklight = true;
            _blinkMonitor.Pause();
            _leds.SetKeyboardBacklightRaw(0);

            foreach (Led led in _fullscreenManagedLeds)
            {
                if (ShouldDimLedForFullscreen(led))
                {
                    _leds.SetLed(led, LedState.Off, customId: Mappings[led].CustomRegisterId);
                }
            }
        }
        else
        {
            if (_hasPreFullscreenBacklight)
            {
                _leds.SetKeyboardBacklightRaw(_preFullscreenBacklight);
            }

            foreach (Led led in _fullscreenManagedLeds)
            {
                ApplyCurrentLedState(led);
            }

            _blinkMonitor.Resume();
        }
    }

    public void OnHotkeyPressed()
    {
        List<LedState> states = BuildHotkeyCycleStates();
        if (states.Count == 0)
        {
            return;
        }

        _hotkeyCycleIndex = (_hotkeyCycleIndex + 1) % states.Count;
        if (_isFullscreen)
        {
            return;
        }

        LedState next = states[_hotkeyCycleIndex];

        foreach ((Led led, LedMapping? map) in Mappings)
        {
            if (map.Mode != LedMode.HotkeyControlled)
            {
                continue;
            }

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
        {
            ApplyCurrentLedState(led);
        }
    }

    /// <summary>
    /// Event-driven monitor updates remain the primary source of truth.
    /// The periodic loop only backstops LED states that the app owns but
    /// cannot observe being overridden externally.
    /// </summary>
    public void ReapplyManagedStates()
    {
        foreach (Led led in Mappings.Keys)
        {
            if (!ShouldForcePeriodicReapply(led))
            {
                continue;
            }

            ApplyLedStateRespectingFullscreen(led, forceWrite: true);
        }
    }

    public bool NeedsPeriodicReapply()
    {
        foreach (Led led in Mappings.Keys)
        {
            if (ShouldUsePeriodicReapplyBackstop(led))
            {
                return true;
            }
        }

        return false;
    }

    public void UpdateBlinkInterval(int intervalMs)
    {
        _blinkMonitor.SetBlinkInterval(intervalMs);
    }

    public void DisableHardwareActivity()
    {
        _blinkMonitor.ClearAll();
    }

    private IReadOnlyDictionary<Led, LedMapping> Mappings
    {
        get =>
        field ?? throw new InvalidOperationException("LedBehaviorService.Initialize must be called before use."); set;
    }

    private void ApplyLedStateRespectingFullscreen(Led led, bool forceWrite = false)
    {
        if (_isFullscreen && ShouldDimLedForFullscreen(led))
        {
            _blinkMonitor.RemoveBlinkingLed(led);
            _leds.SetLed(led, LedState.Off, customId: Mappings[led].CustomRegisterId, forceWrite: forceWrite);
            return;
        }

        ApplyCurrentLedState(led, forceWrite);
    }

    private List<LedState> BuildHotkeyCycleStates()
    {
        List<LedState> states = new(3);
        if ((_hotkeyCycleOptions & HotkeyCycleOptions.On) != 0)
        {
            states.Add(LedState.On);
        }

        if ((_hotkeyCycleOptions & HotkeyCycleOptions.Off) != 0)
        {
            states.Add(LedState.Off);
        }

        if ((_hotkeyCycleOptions & HotkeyCycleOptions.Blink) != 0)
        {
            states.Add(LedState.Blink);
        }

        return states;
    }

    private LedState? GetCurrentHotkeyCycleState()
    {
        List<LedState> states = BuildHotkeyCycleStates();
        if (states.Count == 0)
        {
            return null;
        }

        if (_hotkeyCycleIndex == -1)
        {
            _hotkeyCycleIndex = 0;
        }

        return states[_hotkeyCycleIndex % states.Count];
    }

    private bool ShouldDimLedForFullscreen(Led led)
    {
        if (Array.IndexOf(_fullscreenManagedLeds, led) < 0)
        {
            return false;
        }

        LedMapping map = Mappings[led];
        if (map.Mode != LedMode.Default)
        {
            return true;
        }

        return led switch
        {
            Led.Mute => _hasObservedSpeakerMuteState,
            Led.Microphone => _hasObservedMicrophoneMuteState,
            _ => false,
        };
    }

    private bool ShouldUsePeriodicReapplyBackstop(Led led)
    {
        if (_isFullscreen && ShouldDimLedForFullscreen(led))
        {
            return true;
        }

        LedMapping map = Mappings[led];
        return map.Mode switch
        {
            LedMode.On => true,
            LedMode.Off => true,
            LedMode.HotkeyControlled => true,
            _ => false,
        };
    }

    private bool ShouldForcePeriodicReapply(Led led)
    {
        if (!ShouldUsePeriodicReapplyBackstop(led))
        {
            return false;
        }

        LedMapping map = Mappings[led];
        return map.Mode switch
        {
            LedMode.HotkeyControlled => GetCurrentHotkeyCycleState() is LedState state && state != LedState.Blink,
            _ => true,
        };
    }

    private void ApplyCurrentLedState(Led led, bool forceWrite = false)
    {
        LedMapping map = Mappings[led];
        _blinkMonitor.RemoveBlinkingLed(led);

        switch (map.Mode)
        {
            case LedMode.Default:
                ApplyDefaultLedState(led, map, forceWrite);
                break;
            case LedMode.On:
                _leds.SetLed(led, LedState.On, customId: map.CustomRegisterId, forceWrite: forceWrite);
                break;
            case LedMode.Off:
                _leds.SetLed(led, LedState.Off, customId: map.CustomRegisterId, forceWrite: forceWrite);
                break;
            case LedMode.Blink:
                _blinkMonitor.AddBlinkingLed(led, map.CustomRegisterId);
                break;
            case LedMode.HotkeyControlled:
                ApplyHotkeyControlledState(led, map, forceWrite);
                break;
            case LedMode.DiskRead:
            case LedMode.DiskWrite:
                ApplyDiskActivityLedState(led, map, forceWrite);
                break;
        }
    }

    private void ApplyDefaultLedState(Led led, LedMapping map, bool forceWrite)
    {
        switch (led)
        {
            case Led.Mute when _hasObservedSpeakerMuteState:
                _leds.SetLed(led, _lastSpeakerMuted ? LedState.On : LedState.Off, customId: map.CustomRegisterId, forceWrite: forceWrite);
                break;
            case Led.Microphone when _hasObservedMicrophoneMuteState:
                _leds.SetLed(led, _lastMicrophoneMuted ? LedState.On : LedState.Off, customId: map.CustomRegisterId, forceWrite: forceWrite);
                break;
        }
    }

    private void ApplyHotkeyControlledState(Led led, LedMapping map, bool forceWrite)
    {
        LedState? state = GetCurrentHotkeyCycleState();
        if (!state.HasValue)
        {
            return;
        }

        if (state.Value == LedState.Blink)
        {
            _blinkMonitor.AddBlinkingLed(led, map.CustomRegisterId);
            return;
        }

        _leds.SetLed(led, state.Value, customId: map.CustomRegisterId, forceWrite: forceWrite);
    }

    private void ApplyDiskActivityLedState(Led led, LedMapping map, bool forceWrite = false)
    {
        bool reading = _lastDiskState is DiskActivityState.Read or DiskActivityState.ReadWrite;
        bool writing = _lastDiskState is DiskActivityState.Write or DiskActivityState.ReadWrite;
        bool shouldBeOn = map.Mode switch
        {
            LedMode.DiskRead => reading,
            LedMode.DiskWrite => writing,
            _ => false,
        };

        _leds.SetLed(led, shouldBeOn ? LedState.On : LedState.Off, customId: map.CustomRegisterId, forceWrite: forceWrite);
    }

    public void Dispose()
    {
        _blinkMonitor.Dispose();
    }
}
