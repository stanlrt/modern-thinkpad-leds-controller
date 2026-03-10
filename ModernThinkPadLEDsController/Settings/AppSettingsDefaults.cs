namespace ModernThinkPadLEDsController;

/// <summary>
/// Single source of truth for every AppSettings default value, minimum, and maximum.
/// All other code that needs these boundaries should reference this class rather than
/// defining its own local constants.
/// </summary>
public static class AppSettingsDefaults
{
    // ── Default values ───────────────────────────────────────────────────────

    /// <summary>Default LED blink interval in milliseconds.</summary>
    public const int BlinkIntervalMs = 500;

    /// <summary>Default interval between periodic LED reapply ticks in milliseconds.</summary>
    public const int LedReapplyIntervalMs = 1_000;

    /// <summary>Default disk-activity polling interval in milliseconds.</summary>
    public const int DiskPollIntervalMs = 300;

    /// <summary>Default hotkey cycle options (On and Off are active by default).</summary>
    public const HotkeyCycleOptions DefaultHotkeyCycleOptions = HotkeyCycleOptions.On | HotkeyCycleOptions.Off;

    // ── Minimum values ───────────────────────────────────────────────────────

    /// <summary>Minimum allowed LED blink interval in milliseconds.</summary>
    public const int MinBlinkIntervalMs = 100;

    /// <summary>Minimum allowed LED reapply interval in milliseconds.</summary>
    public const int MinLedReapplyIntervalMs = 250;

    /// <summary>Minimum allowed disk-activity polling interval in milliseconds.</summary>
    public const int MinDiskPollIntervalMs = 100;

    // ── Maximum values ───────────────────────────────────────────────────────

    /// <summary>Maximum allowed LED blink interval in milliseconds.</summary>
    public const int MaxBlinkIntervalMs = 10_000;

    /// <summary>Maximum allowed LED reapply interval in milliseconds.</summary>
    public const int MaxLedReapplyIntervalMs = 10_000;

    /// <summary>Maximum allowed disk-activity polling interval in milliseconds.</summary>
    public const int MaxDiskPollIntervalMs = 10_000;
}
