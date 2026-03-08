using CommunityToolkit.Mvvm.ComponentModel;
using ModernThinkPadLEDsController.Hardware;
using ModernThinkPadLEDsController.Services;

namespace ModernThinkPadLEDsController.ViewModels;

// MainViewModel is the data layer behind MainWindow.
//
// In MVVM (Model-View-ViewModel):
//   Model      = AppSettings
//   View       = MainWindow.xaml (pure layout, no logic)
//   ViewModel  = this class (exposes properties and commands the View binds to)
public sealed partial class MainViewModel : ObservableObject
{
    private readonly LedBehaviorService _ledBehavior;
    private readonly AppSettings _settings;
    private Action? _saveSettingsCallback;
    private bool _isLoading;

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
    private bool _diskMonitoringAvailable;

    public bool DiskMonitoringUnavailable => !DiskMonitoringAvailable;

    // Disk radios should only be enabled when monitoring is available.
    public bool DiskRadiosEnabled => DiskMonitoringAvailable;

    // True when at least one LED is using disk monitoring modes.
    public bool HasDiskModeLeds => _mappings.Values.Any(m => m.Mode is LedMode.DiskRead or LedMode.DiskWrite);

    // Event fired when the disk mode LED count changes (0 to 1+ or 1+ to 0)
    public event Action<bool>? DiskModeLedsChanged;

    private bool _previousHadDiskModes;

    // --- Hotkey cycle config ---
    // Which states should LEDs in HotkeyControlled mode cycle through?
    [ObservableProperty] private bool _hotkeyCycleOn = true;
    [ObservableProperty] private bool _hotkeyCycleOff = true;
    [ObservableProperty] private bool _hotkeyCycleBlink;

    // Display text for the current hotkey combination (e.g., "Win + Shift + K")
    [ObservableProperty] private string _hotkeyDisplayText = "Win + Shift + K";

    // Flag to indicate when user is recording a new hotkey
    [ObservableProperty] private bool _isRecordingHotkey;

    // Warning message for hotkey issues (e.g., no modifiers, conflicts)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHotkeyWarning))]
    private string? _hotkeyWarningMessage;

    // Whether to show the warning box
    public bool HasHotkeyWarning => !string.IsNullOrEmpty(HotkeyWarningMessage);

    // Maps each Led enum value to its LedMapping — used to keep UI configuration
    // aligned with hardware behavior updates handled by LedBehaviorService.
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
        foreach (var (led, map) in _mappings)
        {
            map.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(LedMapping.Mode))
                {
                    _ledBehavior.OnLedModeChanged(led);

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
        TriggerSaveIfEnabled();
    }

    partial void OnHotkeyCycleOffChanged(bool value)
    {
        UpdateHotkeyCycleBehavior();
        TriggerSaveIfEnabled();
    }

    partial void OnHotkeyCycleBlinkChanged(bool value)
    {
        UpdateHotkeyCycleBehavior();
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

            HotkeyDisplayText = FormatHotkeyDisplay(_settings.HotkeyModifiers, _settings.HotkeyVirtualKey);

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
    /// Formats Win32 modifier flags and virtual key code into a human-readable string.
    /// </summary>
    public string FormatHotkeyDisplay(int modifiers, int virtualKey)
    {
        var parts = new List<string>();

        if ((modifiers & 0x0002) != 0) parts.Add("Ctrl");
        if ((modifiers & 0x0001) != 0) parts.Add("Alt");
        if ((modifiers & 0x0004) != 0) parts.Add("Shift");
        if ((modifiers & 0x0008) != 0) parts.Add("Win");

        parts.Add(GetKeyName(virtualKey));

        return string.Join(" + ", parts);
    }

    private void UpdateHotkeyCycleBehavior()
    {
        _ledBehavior.UpdateHotkeyCycleOptions(GetHotkeyCycleOptions());
    }

    private HotkeyCycleOptions GetHotkeyCycleOptions()
    {
        HotkeyCycleOptions options = HotkeyCycleOptions.None;
        if (HotkeyCycleOn) options |= HotkeyCycleOptions.On;
        if (HotkeyCycleOff) options |= HotkeyCycleOptions.Off;
        if (HotkeyCycleBlink) options |= HotkeyCycleOptions.Blink;
        return options;
    }

    private static string GetKeyName(int virtualKey)
    {
        if (virtualKey >= 0x41 && virtualKey <= 0x5A)
            return ((char)virtualKey).ToString();
        if (virtualKey >= 0x30 && virtualKey <= 0x39)
            return ((char)virtualKey).ToString();
        if (virtualKey >= 0x70 && virtualKey <= 0x87)
            return $"F{virtualKey - 0x6F}";

        return virtualKey switch
        {
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x14 => "CapsLock",
            0x1B => "Escape",
            0x20 => "Space",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0x2D => "Insert",
            0x2E => "Delete",
            _ => $"Key{virtualKey:X}"
        };
    }
}
