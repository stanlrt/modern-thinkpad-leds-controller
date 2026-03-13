namespace ModernThinkPadLEDsController.TestInfrastructure;

// TODO: Temporary fake IPortIO implementation for deterministic tests.
//       Replace with a mock-framework stub once a mocking library is adopted.

/// <summary>
/// Records all port I/O writes and returns configurable values for reads.
/// Status reads (port 0x66) always return 0x00 so the EC handshake
/// succeeds immediately without timing out.
/// </summary>
internal sealed class FakePortIO : Hardware.IPortIO
{
    private const ushort EC_CTRLPORT = 0x66;
    private const ushort EC_DATAPORT = 0x62;

    private readonly List<(ushort Port, byte Data)> _writes = [];

    /// <summary>All port writes recorded in order.</summary>
    public IReadOnlyList<(ushort Port, byte Data)> AllWrites => _writes;

    /// <summary>
    /// Value returned when the EC data port (0x62) is read.
    /// Useful for faking <see cref="Hardware.EcController.ReadByte"/> results.
    /// </summary>
    public byte DataReadValue { get; set; } = 0x00;

    /// <summary>Count of bytes written to EC data port (0x62).</summary>
    public int DataPortWriteCount => _writes.Count(w => w.Port == EC_DATAPORT);

    /// <summary>All bytes written to EC data port (0x62), in order.</summary>
    public IReadOnlyList<byte> DataPortWrites =>
        _writes.Where(w => w.Port == EC_DATAPORT).Select(w => w.Data).ToArray();

    /// <inheritdoc />
    public byte ReadByte(ushort port)
    {
        // Returning 0x00 for status reads means IBF and OBF are both clear,
        // so every WaitPortStatus(…, onoff: false) succeeds immediately.
        if (port == EC_CTRLPORT)
        {
            return 0x00;
        }

        return DataReadValue;
    }

    /// <inheritdoc />
    public void WriteByte(ushort port, byte data) => _writes.Add((port, data));

    /// <summary>Clear all recorded writes.</summary>
    public void Clear() => _writes.Clear();
}
