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

    // Set keyboard backlight to any raw byte value (0-255)
    public bool SetKeyboardBacklightRaw(byte level)
    {
        bool success = _ec.WriteByte(TP_KBD_OFFSET, level);
        if (success)
            Log.Debug("SetKeyboardBacklight(raw={Level}) SUCCESS", level);
        else
            Log.Warning("SetKeyboardBacklight(raw={Level}) FAILED", level);
        return success;
    }

    // Read the raw keyboard backlight value (0-255)
    public bool GetKeyboardBacklightRaw(out byte level)
    {
        level = 0;
        if (!_ec.ReadByte(TP_KBD_OFFSET, out level))
        {
            Log.Warning("GetKeyboardBacklightRaw() - ReadByte failed");
            return false;
        }

        Log.Verbose("GetKeyboardBacklightRaw() = {Level}", level);
        return true;
    }
}
