namespace ModernThinkPadLEDsController.Hardware;

/// <summary>
/// Performs port I/O operations.
/// </summary>
public interface IPortIO
{
    byte ReadByte(ushort port);
    void WriteByte(ushort port, byte data);
}
