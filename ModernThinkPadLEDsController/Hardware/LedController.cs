using Serilog;
using ModernThinkPadLEDsController.Lighting;

namespace ModernThinkPadLEDsController.Hardware;

/// <summary>
/// Applies LED and keyboard backlight writes through the EC.
/// </summary>
public sealed class LedController
{
    /// <summary>
    /// EC register addresses for LEDs and keyboard backlight.
    /// </summary>
    private const byte TP_LED_OFFSET = 0x0C;
    private const byte TP_KBD_OFFSET = 0x0D;

    private readonly EcController _ec;
    private readonly HardwareAccessController _hardwareAccess;
    private readonly object _cacheLock = new();
    private readonly Dictionary<byte, LedState> _lastLedStates = new();
    private byte? _lastKeyboardBacklightLevel;

    public LedController(EcController ec, HardwareAccessController hardwareAccess)
    {
        _ec = ec;
        _hardwareAccess = hardwareAccess;
        Log.Debug("LedController initialized");
    }

    public bool SetLed(Led led, LedState state, byte? customId = null)
    {
        if (!_hardwareAccess.IsEnabled)
        {
            _hardwareAccess.LogSuppressedOperationOnce("SetLed");
            return false;
        }

        byte ledId = customId ?? (byte)led;
        byte value = (byte)(ledId | (byte)state);

        lock (_cacheLock)
        {
            if (_lastLedStates.TryGetValue(ledId, out LedState lastState) && lastState == state)
            {
                return true;
            }
        }

        bool success = _ec.WriteByte(TP_LED_OFFSET, value);
        if (success)
        {
            lock (_cacheLock)
            {
                _lastLedStates[ledId] = state;
            }

            Log.Debug("SetLed({Led}, {State}, customId={CustomId}) SUCCESS", led, state, customId);
        }
        else
        {
            Log.Warning("SetLed({Led}, {State}) FAILED", led, state);
        }

        return success;
    }

    /// <summary>
    /// Set keyboard backlight to any raw byte value (0-255)
    /// </summary>
    public bool SetKeyboardBacklightRaw(byte level)
    {
        if (!_hardwareAccess.IsEnabled)
        {
            _hardwareAccess.LogSuppressedOperationOnce("SetKeyboardBacklightRaw");
            return false;
        }

        lock (_cacheLock)
        {
            if (_lastKeyboardBacklightLevel == level)
            {
                return true;
            }
        }

        bool success = _ec.WriteByte(TP_KBD_OFFSET, level);
        if (success)
        {
            lock (_cacheLock)
            {
                _lastKeyboardBacklightLevel = level;
            }

            Log.Debug("SetKeyboardBacklight(raw={Level}) SUCCESS", level);
        }
        else
        {
            Log.Warning("SetKeyboardBacklight(raw={Level}) FAILED", level);
        }

        return success;
    }

    /// <summary>
    /// Read the raw keyboard backlight value (0-255)
    /// </summary>
    public bool GetKeyboardBacklightRaw(out byte level)
    {
        level = 0;
        if (!_hardwareAccess.IsEnabled)
        {
            _hardwareAccess.LogSuppressedOperationOnce("GetKeyboardBacklightRaw");
            return false;
        }

        if (!_ec.ReadByte(TP_KBD_OFFSET, out level))
        {
            Log.Warning("GetKeyboardBacklightRaw() - ReadByte failed");
            return false;
        }

        lock (_cacheLock)
        {
            _lastKeyboardBacklightLevel = level;
        }

        Log.Verbose("GetKeyboardBacklightRaw() = {Level}", level);
        return true;
    }
}
