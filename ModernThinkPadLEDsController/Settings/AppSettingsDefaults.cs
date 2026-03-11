using ModernThinkPadLEDsController.Lighting;
using ModernThinkPadLEDsController.Shell;

namespace ModernThinkPadLEDsController;

/// <summary>
/// Single source of truth for persisted, user-facing AppSettings fallback values and
/// validation bounds.
/// These values are applied only when AppSettings is first created or when a saved
/// settings.json does not contain a newer property yet. Existing values already present
/// in the user's saved settings.json are not overwritten.
/// </summary>
public static class AppSettingsDefaults
{
    /// <summary>Default mode for each configurable LED.</summary>
    public const LedMode LED_MODE = LedMode.Default;

    /// <summary>Default LED blink interval in milliseconds.</summary>
    public const int BLINK_INTERVAL_MS = 500;

    /// <summary>Default interval between periodic LED reapply ticks in milliseconds.</summary>
    public const int LED_REAPPLY_INTERVAL_MS = 500;

    /// <summary>Default disk-activity polling interval in milliseconds.</summary>
    public const int DISK_POLL_INTERVAL_MS = 300;

    /// <summary>Default hotkey cycle options (On and Off are active by default).</summary>
    public const HotkeyCycleOptions HOTKEY_CYCLE_OPTIONS = HotkeyCycleOptions.On | HotkeyCycleOptions.Off;

    /// <summary>Default modifier flags for the global hotkey.</summary>
    public const HotkeyModifiers HOTKEY_MODIFIERS = HotkeyModifiers.Win | HotkeyModifiers.Shift;

    /// <summary>Default virtual-key code for the global hotkey (K).</summary>
    public const int HOTKEY_VIRTUAL_KEY = 0x4B;

    /// <summary>Default setting for whether to remember keyboard backlight level.</summary>
    public const bool REMEMBER_KEYBOARD_BACKLIGHT = false;

    /// <summary>Default setting for whether to periodically enforce keyboard backlight level.</summary>
    public const bool ENFORCE_KEYBOARD_BACKLIGHT = false;

    /// <summary>Default setting for whether to dim LEDs when a fullscreen app is active.</summary>
    public const bool DIM_LEDS_WHEN_FULLSCREEN = false;

    /// <summary>Default setting for whether to suppress disk counter warnings.</summary>
    public const bool SUPPRESS_DISK_COUNTER_WARNING = false;

    /// <summary>Default setting for whether to persist changes immediately on change, or only on exit.</summary>
    public const bool PERSIST_SETTINGS_ON_CHANGE = true;

    /// <summary>Default setting for whether to enable hardware access at all. Disabling is not recommended.</summary>
    public const bool ENABLE_HARDWARE_ACCESS = true;

    /// <summary>Minimum allowed LED blink interval in milliseconds.</summary>
    public const int MIN_BLINK_INTERVAL_MS = 100;

    /// <summary>Minimum allowed LED reapply interval in milliseconds.</summary>
    public const int MIN_LED_REAPPLY_INTERVAL_MS = 250;

    /// <summary>Minimum allowed disk-activity polling interval in milliseconds.</summary>
    public const int MIN_DISK_POLL_INTERVAL_MS = 100;


    /// <summary>Maximum allowed LED blink interval in milliseconds.</summary>
    public const int MAX_BLINK_INTERVAL_MS = 10_000;

    /// <summary>Maximum allowed LED reapply interval in milliseconds.</summary>
    public const int MAX_LED_REAPPLY_INTERVAL_MS = 10_000;

    /// <summary>Maximum allowed disk-activity polling interval in milliseconds.</summary>
    public const int MAX_DISK_POLL_INTERVAL_MS = 10_000;

    /// <summary>
    /// Creates the default global hotkey binding.
    /// Returns a new instance each time to avoid sharing mutable settings state.
    /// </summary>
    public static HotkeyBinding CreateDefaultHotkeyBinding() => new()
    {
        Modifiers = HOTKEY_MODIFIERS,
        VirtualKey = HOTKEY_VIRTUAL_KEY,
    };
}
