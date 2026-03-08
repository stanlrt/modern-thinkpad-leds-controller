using Serilog;

namespace ModernThinkPadLEDsController.Hardware;

public enum Led : byte
{
    Power = 0x00,
    Mute = 0x04,
    Sleep = 0x07,
    FnLock = 0x06,
    RedDot = 0x0A,
    Microphone = 0x0E,
    Camera = 0x0F,
}

public enum LedState : byte
{
    Off = 0x00,
    On = 0x80,
    Blink = 0xC0,
}

public enum KeyboardBacklight : byte
{
    Off = 0x00,
    Low = 0x40,
    High = 0x80,
}

public sealed class LedController
{
    // EC register addresses for LEDs and keyboard backlight.
    private const byte TP_LED_OFFSET = 0x0C;
    private const byte TP_KBD_OFFSET = 0x0D;

    private readonly EcController _ec;

    public LedController(EcController ec)
    {
        _ec = ec;
        Log.Debug("LedController initialized");
    }

    public bool SetLed(Led led, LedState state, byte? customId = null)
    {

        byte ledId = customId ?? (byte)led;
        byte value = (byte)(ledId | (byte)state);

        bool success = _ec.WriteByte(TP_LED_OFFSET, value);
        if (success)
            Log.Debug("SetLed({Led}, {State}, customId={CustomId}) SUCCESS", led, state, customId);
        else
            Log.Warning("SetLed({Led}, {State}) FAILED", led, state);

        return success;
    }

    public bool SetKeyboardBacklight(KeyboardBacklight level)
    {
        bool success = _ec.WriteByte(TP_KBD_OFFSET, (byte)level);
        if (success)
            Log.Debug("SetKeyboardBacklight({Level}) SUCCESS", level);
        else
            Log.Warning("SetKeyboardBacklight({Level}) FAILED", level);
        return success;
    }

    // Read the current keyboard backlight level from the EC.
    // The ranges (< 50, 50–100, 100–150) match the legacy GetKeyboardLightlevel() logic.
    public bool GetKeyboardBacklight(out KeyboardBacklight level)
    {
        level = KeyboardBacklight.Off;
        if (!_ec.ReadByte(TP_KBD_OFFSET, out byte raw))
        {
            Log.Warning("GetKeyboardBacklight() - ReadByte failed");
            return false;
        }

        level = raw switch
        {
            >= 100 and < 150 => KeyboardBacklight.High,
            >= 50 and < 100 => KeyboardBacklight.Low,
            _ => KeyboardBacklight.Off,
        };

        Log.Verbose("GetKeyboardBacklight() = {Level} (raw: {Raw})", level, raw);
        return true;
    }
}
