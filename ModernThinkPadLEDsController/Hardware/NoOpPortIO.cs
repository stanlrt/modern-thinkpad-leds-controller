namespace ModernThinkPadLEDsController.Hardware;

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
