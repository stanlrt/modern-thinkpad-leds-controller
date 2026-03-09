using ModernThinkPadLEDsController.Lighting;

namespace ModernThinkPadLEDsController.TestInfrastructure;

/// <summary>
/// Builds deterministic <see cref="AppSettings"/> instances for use in tests.
/// </summary>
internal static class TestSettingsBuilder
{
    /// <summary>Returns a fresh <see cref="AppSettings"/> with default values.</summary>
    public static AppSettings Default() => new();

    /// <summary>Returns settings with every LED mode set to <paramref name="mode"/>.</summary>
    public static AppSettings AllLedsMode(LedMode mode) => new()
    {
        PowerMode = mode,
        MuteMode = mode,
        RedDotMode = mode,
        MicrophoneMode = mode,
        SleepMode = mode,
        FnLockMode = mode,
        CameraMode = mode,
    };

    /// <summary>Builds a mapping dictionary with a single LED entry.</summary>
    public static IReadOnlyDictionary<Led, LedMapping> SingleMapping(
        Led led,
        LedMode mode,
        byte? customId = null)
        => new Dictionary<Led, LedMapping>
        {
            [led] = new LedMapping { Mode = mode, CustomRegisterId = customId },
        };

    /// <summary>Builds a mapping dictionary with all standard LEDs set to <paramref name="mode"/>.</summary>
    public static IReadOnlyDictionary<Led, LedMapping> AllMappings(LedMode mode) =>
        new Dictionary<Led, LedMapping>
        {
            [Led.Power] = new LedMapping { Mode = mode },
            [Led.Mute] = new LedMapping { Mode = mode },
            [Led.RedDot] = new LedMapping { Mode = mode },
            [Led.Microphone] = new LedMapping { Mode = mode },
            [Led.Sleep] = new LedMapping { Mode = mode },
            [Led.FnLock] = new LedMapping { Mode = mode },
            [Led.Camera] = new LedMapping { Mode = mode },
        };
}
