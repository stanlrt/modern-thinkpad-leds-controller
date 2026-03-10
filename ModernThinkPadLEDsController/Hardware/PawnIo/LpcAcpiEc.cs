using System.Reflection;

namespace ModernThinkPadLEDsController.Hardware.PawnIo;

/// <summary>
/// Provides access to the Embedded Controller (EC) via PawnIO's LPC ACPI EC module.
/// Extracted from LibreHardwareMonitor to avoid full library dependency.
/// </summary>
/// <remarks>
/// This code is derived from LibreHardwareMonitor (MPL-2.0 license).
/// Original source: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor
/// </remarks>
internal sealed class LpcAcpiEc : IDisposable
{
    private readonly PawnIoDriver _pawnIO;

    public LpcAcpiEc()
    {
        // Load the LpcACPIEC.bin module from embedded resources
        _pawnIO = PawnIoDriver.LoadModuleFromResource(
            Assembly.GetExecutingAssembly(),
            "ModernThinkPadLEDsController.Resources.PawnIo.LpcACPIEC.bin");
    }

    /// <summary>
    /// Gets a value indicating whether the EC module is loaded and ready.
    /// </summary>
    public bool IsLoaded => _pawnIO.IsLoaded;

    /// <summary>
    /// Reads a byte from the specified EC port.
    /// </summary>
    public byte ReadPort(byte port)
    {
        long[] inArray = new long[1];
        inArray[0] = port;
        long[] outArray = _pawnIO.Execute("ioctl_pio_read", inArray, 1);
        return (byte)outArray[0];
    }

    /// <summary>
    /// Writes a byte to the specified EC port.
    /// </summary>
    public void WritePort(byte port, byte value)
    {
        long[] inArray = new long[2];
        inArray[0] = port;
        inArray[1] = value;
        _pawnIO.Execute("ioctl_pio_write", inArray, 0);
    }

    public void Dispose()
    {
        _pawnIO?.Dispose();
    }
}
