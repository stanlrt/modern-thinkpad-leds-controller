namespace ModernThinkPadLEDsController.Hardware;

/// <summary>
/// Ignores all port I/O when hardware access is disabled.
/// </summary>
public sealed class NoOpPortIO : IPortIO
{
    public byte ReadByte(ushort port)
    {
        return 0;
    }

    public void WriteByte(ushort port, byte data)
    {
    }
}
