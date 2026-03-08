using System.Threading;
using Serilog;

namespace ModernThinkPadLEDsController.Hardware;

public readonly record struct HardwareAccessPreferenceChangeResult(bool AppliedImmediately, string? Message);

public sealed class HardwareAccessController
{
    private int _isEnabled;
    private int _suppressedOperationLogged;

    public HardwareAccessController(bool isEnabled, bool driverLoaded, string? startupReason)
    {
        _isEnabled = isEnabled ? 1 : 0;
        DriverLoaded = driverLoaded;
        StartupReason = startupReason;
    }

    public bool DriverLoaded { get; }

    public string? StartupReason { get; }

    public bool IsEnabled => Volatile.Read(ref _isEnabled) != 0;

    public bool DisableForSession()
    {
        return Interlocked.Exchange(ref _isEnabled, 0) != 0;
    }

    public string GetStatusDescription()
    {
        if (IsEnabled)
            return DriverLoaded ? "PawnIO active" : "Hardware access requested, restart required";

        if (DriverLoaded)
            return "Hardware access disabled for this session";

        return StartupReason is null
            ? "Hardware access disabled"
            : $"Hardware access disabled ({StartupReason})";
    }

    public void LogSuppressedOperationOnce(string operationName)
    {
        if (Interlocked.Exchange(ref _suppressedOperationLogged, 1) != 0)
            return;

        Log.Warning(
            "Hardware access is disabled; suppressing EC operations. First blocked operation: {Operation}. Status: {Status}",
            operationName,
            GetStatusDescription());
    }
}
