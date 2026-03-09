using ModernThinkPadLEDsController.Lighting;

namespace ModernThinkPadLEDsController.TestInfrastructure;

// TODO: Temporary fake ILedBlinkController for deterministic tests.
//       Replace with a mock-framework stub once a mocking library is adopted.

/// <summary>
/// Records blink registration calls without starting any background loop.
/// </summary>
internal sealed class FakeLedBlinkController : ILedBlinkController
{
    private readonly Dictionary<Led, byte?> _blinking = new();

    /// <summary>Currently registered blinking LEDs and their custom IDs.</summary>
    public IReadOnlyDictionary<Led, byte?> BlinkingLeds => _blinking;

    public bool IsPaused { get; private set; }

    public void AddBlinkingLed(Led led, byte? customId) => _blinking[led] = customId;

    public void RemoveBlinkingLed(Led led) => _blinking.Remove(led);

    public void ClearAll() => _blinking.Clear();

    public void Pause() => IsPaused = true;

    public void Resume() => IsPaused = false;

    public void SetBlinkInterval(int intervalMs) { }

    public void Dispose() { }
}
