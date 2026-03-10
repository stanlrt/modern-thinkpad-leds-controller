namespace ModernThinkPadLEDsController.Lighting;

/// <summary>
/// Abstraction over blink-loop behavior, allowing injection of a test double.
/// </summary>
internal interface ILedBlinkController : IDisposable
{
    void AddBlinkingLed(Led led, byte? customId);
    void RemoveBlinkingLed(Led led);
    void ClearAll();
    void Pause();
    void Resume();
    void SetBlinkInterval(int intervalMs);
}
