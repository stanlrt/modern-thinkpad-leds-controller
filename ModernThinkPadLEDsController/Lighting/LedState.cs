namespace ModernThinkPadLEDsController.Lighting;

/// <summary>
/// Represents the hardware state written to a single LED.
/// </summary>
public enum LedState : byte
{
    Off = 0x00,
    On = 0x80,
    Blink = 0xC0,
}
