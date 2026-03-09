using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;
using ModernThinkPadLEDsController.Hardware;
using ModernThinkPadLEDsController.Lighting;
using ModernThinkPadLEDsController.Shell;

namespace ModernThinkPadLEDsController.Presentation.ViewModels;

/// <summary>
/// Exposes the main window LED configuration state.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly LedBehaviorService _ledBehavior;
    private readonly AppSettings _settings;
    private Action? _saveSettingsCallback;
    private bool _isLoading;

    /// <summary>
    /// One LedMapping per LED. The View binds to e.g. Power.Mode, RedDot.Mode, etc.
    /// </summary>
    public LedMapping Power { get; } = new() { Name = "Power" };
    public LedMapping Mute { get; } = new() { Name = "Mute" };
    public LedMapping RedDot { get; } = new() { Name = "Red Dot" };
    public LedMapping Microphone { get; } = new() { Name = "Microphone" };
    public LedMapping Sleep { get; } = new() { Name = "Sleep" };
    public LedMapping FnLock { get; } = new() { Name = "Fn Lock" };
    public LedMapping Camera { get; } = new() { Name = "Camera" };

    /// <summary>
    /// Ordered list for the XAML ItemsControl — same objects as above.
    /// </summary>
    public IReadOnlyList<LedMapping> Leds { get; }

    /// <summary>
    /// False when Windows disk performance counters are unavailable on this machine.
    /// The View uses this to show a warning in the Disk tab.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DiskMonitoringUnavailable))]
    [NotifyPropertyChangedFor(nameof(DiskRadiosEnabled))]
    private bool _diskMonitoringAvailable;

    public bool DiskMonitoringUnavailable => !DiskMonitoringAvailable;

    /// <summary>
    /// Disk radios should only be enabled when monitoring is available.
    /// </summary>
    public bool DiskRadiosEnabled => DiskMonitoringAvailable;

    /// <summary>
    /// True when at least one LED is using disk monitoring modes.
    /// </summary>
    public bool HasDiskModeLeds => _mappings.Values.Any(m => m.Mode is LedMode.DiskRead or LedMode.DiskWrite);

    /// <summary>
    /// Event fired when the disk mode LED count changes (0 to 1+ or 1+ to 0)
    /// </summary>
    public event Action<bool>? DiskModeLedsChanged;

    public event Action? LedConfigurationChanged;

    private bool _previousHadDiskModes;

    /// <summary>
    /// --- Hotkey cycle config ---
    /// Which states should LEDs in HotkeyControlled mode cycle through?
    /// </summary>
    [ObservableProperty] private bool _hotkeyCycleOn = true;
    [ObservableProperty] private bool _hotkeyCycleOff = true;
    [ObservableProperty] private bool _hotkeyCycleBlink;

    /// <summary>
    /// Display text for the current hotkey combination (e.g., "Win + Shift + K")
    /// </summary>
    [ObservableProperty] private string _hotkeyDisplayText = "Win + Shift + K";

    /// <summary>
    /// Flag to indicate when user is recording a new hotkey
    /// </summary>
    [ObservableProperty] private bool _isRecordingHotkey;

    /// <summary>
    /// Warning message for hotkey issues (e.g., no modifiers, conflicts)
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHotkeyWarning))]
    private string? _hotkeyWarningMessage;

    /// <summary>
    /// Whether to show the warning box
    /// </summary>
    public bool HasHotkeyWarning => !string.IsNullOrEmpty(HotkeyWarningMessage);

    /// <summary>
    /// Maps each Led enum value to its LedMapping — used to keep UI configuration
    /// aligned with hardware behavior updates handled by LedBehaviorService.
    /// </summary>
    private readonly Dictionary<Led, LedMapping> _mappings;

    public MainViewModel(LedBehaviorService ledBehavior, AppSettings settings)
    {
        _ledBehavior = ledBehavior;
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

        _ledBehavior.Initialize(_mappings);
        UpdateHotkeyCycleBehavior();

        // When the user changes a mode via the UI, apply it to hardware immediately.
        foreach ((Led led, LedMapping? map) in _mappings)
        {
            map.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(LedMapping.Mode))
                {
                    if (_isLoading)
                    {
                        return;
                    }

                    _ledBehavior.OnLedModeChanged(led);

                    bool hasDiskModesNow = HasDiskModeLeds;
                    if (_previousHadDiskModes != hasDiskModesNow)
                    {
                        _previousHadDiskModes = hasDiskModesNow;
                        OnPropertyChanged(nameof(HasDiskModeLeds));
                        DiskModeLedsChanged?.Invoke(hasDiskModesNow);
                    }

                    LedConfigurationChanged?.Invoke();

                    TriggerSaveIfEnabled();
                }
                else if (e.PropertyName == nameof(LedMapping.CustomRegisterId))
                {
                    if (_isLoading)
                    {
                        return;
                    }

                    _ledBehavior.OnCustomRegisterIdChanged(led);
                    TriggerSaveIfEnabled();
                }
            };
        }

        _previousHadDiskModes = HasDiskModeLeds;
    }

    partial void OnHotkeyCycleOnChanged(bool value)
    {
        UpdateHotkeyCycleBehavior();
        LedConfigurationChanged?.Invoke();
        TriggerSaveIfEnabled();
    }

    partial void OnHotkeyCycleOffChanged(bool value)
    {
        UpdateHotkeyCycleBehavior();
        LedConfigurationChanged?.Invoke();
        TriggerSaveIfEnabled();
    }

    partial void OnHotkeyCycleBlinkChanged(bool value)
    {
        UpdateHotkeyCycleBehavior();
        LedConfigurationChanged?.Invoke();
        TriggerSaveIfEnabled();
    }

    // -------------------------------------------------------------------------
    // Settings round-trip
    // -------------------------------------------------------------------------

    public void LoadFromSettings()
    {
        _isLoading = true;

        try
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

            HotkeyCycleOptions hotkeyCycleOptions = _settings.HotkeyCycleOptions;
            HotkeyCycleOn = (hotkeyCycleOptions & HotkeyCycleOptions.On) != 0;
            HotkeyCycleOff = (hotkeyCycleOptions & HotkeyCycleOptions.Off) != 0;
            HotkeyCycleBlink = (hotkeyCycleOptions & HotkeyCycleOptions.Blink) != 0;

            HotkeyDisplayText = FormatHotkeyDisplay(_settings.Hotkey);

            _previousHadDiskModes = HasDiskModeLeds;
            OnPropertyChanged(nameof(HasDiskModeLeds));
        }
        finally
        {
            _isLoading = false;
        }
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

        _settings.HotkeyCycleOptions = GetHotkeyCycleOptions();
    }

    // -------------------------------------------------------------------------
    // Settings persistence
    // -------------------------------------------------------------------------

    private void TriggerSaveIfEnabled()
    {
        if (!_isLoading && _settings.PersistSettingsOnChange)
        {
            SaveToSettings();
            _saveSettingsCallback?.Invoke();
        }
    }

    public void SetSaveCallback(Action callback)
    {
        _saveSettingsCallback = callback;
    }

    // -------------------------------------------------------------------------
    // Hotkey display formatting
    // -------------------------------------------------------------------------

    /// <summary>
    /// Formats Win32 modifier flags and a WPF key into a human-readable string.
    /// </summary>
    public string FormatHotkeyDisplay(HotkeyBinding hotkey)
    {
        List<string> parts = new List<string>();

        if ((hotkey.Modifiers & HotkeyModifiers.Control) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((hotkey.Modifiers & HotkeyModifiers.Alt) != 0)
        {
            parts.Add("Alt");
        }

        if ((hotkey.Modifiers & HotkeyModifiers.Shift) != 0)
        {
            parts.Add("Shift");
        }

        if ((hotkey.Modifiers & HotkeyModifiers.Win) != 0)
        {
            parts.Add("Win");
        }

        parts.Add(GetKeyName(hotkey.Key));

        return string.Join(" + ", parts);
    }

    private void UpdateHotkeyCycleBehavior()
    {
        _ledBehavior.UpdateHotkeyCycleOptions(GetHotkeyCycleOptions());
    }

    private HotkeyCycleOptions GetHotkeyCycleOptions()
    {
        HotkeyCycleOptions options = HotkeyCycleOptions.None;
        if (HotkeyCycleOn)
        {
            options |= HotkeyCycleOptions.On;
        }

        if (HotkeyCycleOff)
        {
            options |= HotkeyCycleOptions.Off;
        }

        if (HotkeyCycleBlink)
        {
            options |= HotkeyCycleOptions.Blink;
        }

        return options;
    }

    private static string GetKeyName(Key key)
    {
        return key switch
        {
            Key.Capital => "CapsLock",
            Key.Space => "Space",
            Key.Prior => "PageUp",
            Key.Next => "PageDown",
            Key.Return => "Enter",
            _ => key.ToString()
        };
    }
}
