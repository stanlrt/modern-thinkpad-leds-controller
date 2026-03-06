namespace ModernThinkPadLEDsController.Hardware;

// These enums replace the raw byte constants in the legacy code.
// Instead of writing (byte)(0x0A | 0x80) everywhere, you write
// SetLed(Led.RedDot, LedState.On) and the intent is obvious.

public enum Led : byte
{
    Power      = 0x00,
    Sleep      = 0x07,
    FnLock     = 0x06,
    RedDot     = 0x0A,
    Microphone = 0x0E,
}

public enum LedState : byte
{
    Off   = 0x00,
    On    = 0x80,
    Blink = 0xC0,
}

public enum KeyboardBacklight : byte
{
    Off  = 0x00,
    Low  = 0x40,
    High = 0x80,
}

public sealed class LedController
{
    // EC register addresses for LEDs and keyboard backlight.
    // Writing led_id | power_state to 0x0C controls a specific LED.
    // Writing a level byte to 0x0D controls keyboard backlight brightness.
    private const byte TP_LED_OFFSET = 0x0C;
    private const byte TP_KBD_OFFSET = 0x0D;

    private readonly EcController _ec;

    public LedController(EcController ec) => _ec = ec;

    // Set a named LED to a specific state.
    // invertState: when true, On becomes Off and Off becomes On.
    //   Used for the "invert" feature — e.g. power LED off while
    //   system is active, on only during disk idle.
    public bool SetLed(Led led, LedState state, bool invertState = false)
    {
        if (invertState && state != LedState.Blink)
            state = state == LedState.On ? LedState.Off : LedState.On;

        byte value = (byte)((byte)led | (byte)state);
        return _ec.WriteByte(TP_LED_OFFSET, value);
    }

    // Set a LED by raw ID (for the custom EC write dialog).
    public bool SetLedRaw(byte ledId, LedState state)
    {
        byte value = (byte)(ledId | (byte)state);
        return _ec.WriteByte(TP_LED_OFFSET, value);
    }

    public bool SetKeyboardBacklight(KeyboardBacklight level)
        => _ec.WriteByte(TP_KBD_OFFSET, (byte)level);

    // Read the current keyboard backlight level from the EC.
    // The ranges (< 50, 50–100, 100–150) match the legacy GetKeyboardLightlevel() logic.
    public bool GetKeyboardBacklight(out KeyboardBacklight level)
    {
        level = KeyboardBacklight.Off;
        if (!_ec.ReadByte(TP_KBD_OFFSET, out byte raw))
            return false;

        level = raw switch
        {
            >= 100 and < 150 => KeyboardBacklight.High,
            >= 50  and < 100 => KeyboardBacklight.Low,
            _                => KeyboardBacklight.Off,
        };
        return true;
    }
}
