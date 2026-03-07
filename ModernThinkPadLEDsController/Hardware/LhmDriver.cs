using LibreHardwareMonitor.PawnIo;
using System.ServiceProcess;
using Serilog;

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

        Log.Debug("Attempting to open LHM driver (PawnIO)");

        // Check if PawnIO Windows service is running
        try
        {
            using var service = new ServiceController("pawnio");
            var status = service.Status;
            Log.Information("PawnIO service status: {Status}", status);

            if (status != ServiceControllerStatus.Running)
            {
                Log.Warning("PawnIO service not running - needs installation or start. Current status: {Status}", status);
                return false;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PawnIO service does not exist or cannot be accessed");
            return false;
        }

        try
        {
            Log.Debug("Instantiating LpcAcpiEc module");
            var pawnModule = new LpcAcpiEc();
            driver = new LhmDriver(pawnModule);
            Log.Information("LHM driver opened successfully");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to instantiate LpcAcpiEc module");
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
            Log.Verbose("ReadPort(0x{Port:X2}) = 0x{Result:X2}", port, result);
            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ReadPort(0x{Port:X2}) FAILED", port);
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
            Log.Verbose("WritePort(0x{Port:X2}, 0x{Data:X2}) SUCCESS", port, data);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "WritePort(0x{Port:X2}, 0x{Data:X2}) FAILED", port, data);
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Log.Debug("Closing LHM driver");
        _pawnModule.Close();
        Log.Information("LHM driver disposed");
    }
}
