using Serilog;

namespace ModernThinkPadLEDsController.Hardware;

// EcController talks to the ThinkPad Embedded Controller (EC) over I/O ports.
//
// The EC is a small microcontroller inside the laptop that manages LEDs, fans,
// thermal sensors, the keyboard backlight, and other low-level hardware.
// Communication uses a two-port handshake protocol:
//   0x62 = data port  (read/write values)
//   0x66 = control port (send commands, read status flags)
//
// This code is ported verbatim from Form1.cs (~lines 40-200).
// The only change is swapping the WinRing0/TVicPort branching for IPortIO calls.
public sealed class EcController
{
    private const ushort EC_DATAPORT = 0x62;
    private const ushort EC_CTRLPORT = 0x66;

    private const int EC_STAT_OBF = 0x01;   // Output Buffer Full  — EC has data waiting for us
    private const int EC_STAT_IBF = 0x02;   // Input Buffer Full   — EC is still processing our last write

    private const byte EC_CTRLPORT_READ = 0x80;  // Command: we want to READ a register
    private const byte EC_CTRLPORT_WRITE = 0x81;  // Command: we want to WRITE a register

    private readonly IPortIO _io;

    public EcController(IPortIO io)
    {
        _io = io;
        Log.Debug("EcController initialized");
    }

    // Wait until the EC's status flags match the desired state.
    // 'bits'  — which flag bits to watch (OBF, IBF, or both)
    // 'onoff' — false = wait for them to be CLEAR, true = wait for them to be SET
    private bool WaitPortStatus(int bits, bool onoff = false, int timeout = 1000)
    {
        const int tick = 10;
        for (int elapsed = 0; elapsed < timeout; elapsed += tick)
        {
            try
            {
                byte status = _io.ReadByte(EC_CTRLPORT);
                bool flagSet = (status & bits) != 0;
                if (flagSet == onoff) return true;
            }
            catch { return false; }

            Thread.Sleep(tick);
        }
        return true; // timed out but we continue — matches legacy behaviour
    }

    private bool WritePort(ushort port, byte data)
    {
        try { _io.WriteByte(port, data); return true; }
        catch { return false; }
    }

    private bool ReadPort(ushort port, out byte data)
    {
        data = 0;
        try { data = _io.ReadByte(port); return true; }
        catch { return false; }
    }

    // Read one byte from EC register 'offset'.
    // The EC requires a specific 4-step handshake before it puts the value on the data port.
    public bool ReadByte(byte offset, out byte data)
    {
        data = 0xFF;

        if (!WaitPortStatus(EC_STAT_IBF | EC_STAT_OBF))
        {
            Log.Warning("EC ReadByte(0x{Offset:X2}) - Failed to wait for buffers clear", offset);
            return false;
        }
        if (!WritePort(EC_CTRLPORT, EC_CTRLPORT_READ)) return false;
        if (!WaitPortStatus(EC_STAT_IBF)) return false;
        if (!WritePort(EC_DATAPORT, offset)) return false;
        if (!WaitPortStatus(EC_STAT_IBF)) return false;

        bool success = ReadPort(EC_DATAPORT, out data);
        if (success)
            Log.Verbose("EC ReadByte(0x{Offset:X2}) = 0x{Data:X2}", offset, data);
        else
            Log.Warning("EC ReadByte(0x{Offset:X2}) failed", offset);

        return success;
    }

    // Write one byte to EC register 'offset'.
    // Similar 5-step handshake — extra step to deliver the data byte.
    public bool WriteByte(byte offset, byte data)
    {
        if (!WaitPortStatus(EC_STAT_IBF | EC_STAT_OBF))
        {
            Log.Warning("EC WriteByte(0x{Offset:X2}, 0x{Data:X2}) - Failed to wait for buffers clear", offset, data);
            return false;
        }
        if (!WritePort(EC_CTRLPORT, EC_CTRLPORT_WRITE)) return false;
        if (!WaitPortStatus(EC_STAT_IBF)) return false;
        if (!WritePort(EC_DATAPORT, offset)) return false;
        if (!WaitPortStatus(EC_STAT_IBF)) return false;
        if (!WritePort(EC_DATAPORT, data)) return false;

        bool success = WaitPortStatus(EC_STAT_IBF);
        if (success)
            Log.Verbose("EC WriteByte(0x{Offset:X2}, 0x{Data:X2}) SUCCESS", offset, data);
        else
            Log.Warning("EC WriteByte(0x{Offset:X2}, 0x{Data:X2}) FAILED", offset, data);

        return success;
    }
}
