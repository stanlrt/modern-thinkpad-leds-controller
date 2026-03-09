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
/// <summary>
/// Reads and writes ThinkPad EC registers through port I/O.
/// </summary>
public sealed class EcController
{
    private const ushort EC_DATAPORT = 0x62;
    private const ushort EC_CTRLPORT = 0x66;

    [Flags]
#pragma warning disable RCS1237 // [deprecated] Use bit shift operator
    private enum EcStatusFlags : byte
    {
        None = 0,
        OutputBufferFull = 0x01,
        InputBufferFull = 0x02,
    }
#pragma warning restore RCS1237 // [deprecated] Use bit shift operator
    /// <summary>
    /// Command: we want to READ a register
    /// </summary>
    private const byte EC_CTRLPORT_READ = 0x80;
    /// <summary>
    /// Command: we want to WRITE a register
    /// </summary>
    private const byte EC_CTRLPORT_WRITE = 0x81;

    private readonly IPortIO _io;
    private readonly object _transactionLock = new();

    public EcController(IPortIO io)
    {
        _io = io;
        Log.Debug("EcController initialized");
    }

    /// <summary>
    /// Wait until the EC's status flags match the desired state.
    /// 'bits'  — which flag bits to watch (OBF, IBF, or both)
    /// 'onoff' — false = wait for them to be CLEAR, true = wait for them to be SET
    /// </summary>
    private bool WaitPortStatus(EcStatusFlags bits, bool onoff = false, int timeout = 1000)
    {
        const int tick = 10;
        for (int elapsed = 0; elapsed < timeout; elapsed += tick)
        {
            try
            {
                byte statusByte = _io.ReadByte(EC_CTRLPORT);
                EcStatusFlags status = (EcStatusFlags)statusByte;
                bool flagSet = (status & bits) != EcStatusFlags.None;
                if (flagSet == onoff)
                {
                    return true;
                }
            }
            catch { return false; }

            Thread.Sleep(tick);
        }

        Log.Warning(
            "EC WaitPortStatus timed out after {TimeoutMs} ms for bits 0x{Bits:X2} expecting {State}",
            timeout,
            (byte)bits,
            onoff ? "set" : "clear");

        return false;
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

    /// <summary>
    /// Read one byte from EC register 'offset'.
    /// The EC requires a specific 4-step handshake before it puts the value on the data port.
    /// </summary>
    public bool ReadByte(byte offset, out byte data)
    {
        data = 0xFF;

        lock (_transactionLock)
        {
            if (!WaitPortStatus(EcStatusFlags.InputBufferFull | EcStatusFlags.OutputBufferFull))
            {
                Log.Warning("EC ReadByte(0x{Offset:X2}) - Failed to wait for buffers clear", offset);
                return false;
            }
            if (!WritePort(EC_CTRLPORT, EC_CTRLPORT_READ))
            {
                return false;
            }

            if (!WaitPortStatus(EcStatusFlags.InputBufferFull))
            {
                return false;
            }

            if (!WritePort(EC_DATAPORT, offset))
            {
                return false;
            }

            if (!WaitPortStatus(EcStatusFlags.InputBufferFull))
            {
                return false;
            }

            bool success = ReadPort(EC_DATAPORT, out data);
            if (success)
            {
                Log.Verbose("EC ReadByte(0x{Offset:X2}) = 0x{Data:X2}", offset, data);
            }
            else
            {
                Log.Warning("EC ReadByte(0x{Offset:X2}) failed", offset);
            }

            return success;
        }
    }

    /// <summary>
    /// Write one byte to EC register 'offset'.
    /// Similar 5-step handshake — extra step to deliver the data byte.
    /// </summary>
    public bool WriteByte(byte offset, byte data)
    {
        lock (_transactionLock)
        {
            if (!WaitPortStatus(EcStatusFlags.InputBufferFull | EcStatusFlags.OutputBufferFull))
            {
                Log.Warning("EC WriteByte(0x{Offset:X2}, 0x{Data:X2}) - Failed to wait for buffers clear", offset, data);
                return false;
            }
            if (!WritePort(EC_CTRLPORT, EC_CTRLPORT_WRITE))
            {
                return false;
            }

            if (!WaitPortStatus(EcStatusFlags.InputBufferFull))
            {
                return false;
            }

            if (!WritePort(EC_DATAPORT, offset))
            {
                return false;
            }

            if (!WaitPortStatus(EcStatusFlags.InputBufferFull))
            {
                return false;
            }

            if (!WritePort(EC_DATAPORT, data))
            {
                return false;
            }

            bool success = WaitPortStatus(EcStatusFlags.InputBufferFull);
            if (success)
            {
                Log.Verbose("EC WriteByte(0x{Offset:X2}, 0x{Data:X2}) SUCCESS", offset, data);
            }
            else
            {
                Log.Warning("EC WriteByte(0x{Offset:X2}, 0x{Data:X2}) FAILED", offset, data);
            }

            return success;
        }
    }
}
