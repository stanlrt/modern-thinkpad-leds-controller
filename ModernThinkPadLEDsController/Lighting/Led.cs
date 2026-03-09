namespace ModernThinkPadLEDsController.Lighting;

/// <summary>
/// Identifies the ThinkPad LEDs that the app can control.
/// </summary>
public enum Led : byte
{
    Power = 0x00,
    Mute = 0x04,
    FnLock = 0x06,
    Sleep = 0x07,
    RedDot = 0x0A,
    Microphone = 0x0E,
    Camera = 0x0F,
}
