using LibreHardwareMonitor.PawnIo;
using System.ServiceProcess;

namespace ModernThinkPadLEDsController.Hardware;

// LhmDriver wraps LibreHardwareMonitor's PawnIO-based EC port access.
//
// LibreHardwareMonitor uses PawnIO instead of inpout64, providing better security
// through scriptable driver modules. The LpcAcpiEc class gives us direct access
// to I/O ports 0x62 and 0x66 (the Embedded Controller data and command ports).
//
// PawnIO must be installed separately by the user (from https://pawnio.eu/).
// On first use, the driver loads the LpcACPIEC.bin module embedded in LibreHardwareMonitorLib.
public sealed class LhmDriver : IPortIO, IDisposable
{
    private readonly LpcAcpiEc _pawnModule;
    private bool _disposed;

    // TryOpen is the only way to create an LhmDriver.
    // It returns false if PawnIO is not installed or the module fails to load.
    // 'out LhmDriver? driver' is an output parameter — the caller gets the
    // object back through it when the return value is true.
    public static bool TryOpen(out LhmDriver? driver)
    {
        driver = null;

        // Check if PawnIO Windows service is running
        try
        {
            using var service = new ServiceController("pawnio");
            if (service.Status != ServiceControllerStatus.Running)
            {
                // PawnIO service not running - needs installation or start
                return false;
            }
        }
        catch
        {
            // PawnIO service doesn't exist
            return false;
        }

        try
        {
            var pawnModule = new LpcAcpiEc();
            driver = new LhmDriver(pawnModule);
            return true;
        }
        catch
        {
            // Exception during instantiation
            return false;
        }
    }

    private LhmDriver(LpcAcpiEc pawnModule)
    {
        _pawnModule = pawnModule;
    }

    public byte ReadByte(ushort port)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LhmDriver));

        try
        {
            byte result = _pawnModule.ReadPort((byte)port);
            System.Diagnostics.Debug.WriteLine($"DEBUG: ReadPort(0x{port:X2}) = 0x{result:X2}");
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DEBUG: ReadPort(0x{port:X2}) FAILED: {ex.Message}");
            throw;
        }
    }

    public void WriteByte(ushort port, byte data)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LhmDriver));

        try
        {
            _pawnModule.WritePort((byte)port, data);
            System.Diagnostics.Debug.WriteLine($"DEBUG: WritePort(0x{port:X2}, 0x{data:X2}) SUCCESS");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DEBUG: WritePort(0x{port:X2}, 0x{data:X2}) FAILED: {ex.Message}");
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pawnModule.Close();
    }
}
