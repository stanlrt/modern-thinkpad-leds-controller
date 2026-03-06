namespace ModernThinkPadLEDsController.Hardware;

// An interface is a contract. Any class that implements IPortIO must provide
// ReadByte and WriteByte. EcController only knows about IPortIO — it never
// cares which driver is underneath. This makes testing easier and keeps
// hardware details isolated to one place.
public interface IPortIO
{
    byte ReadByte(ushort port);
    void WriteByte(ushort port, byte data);
}
