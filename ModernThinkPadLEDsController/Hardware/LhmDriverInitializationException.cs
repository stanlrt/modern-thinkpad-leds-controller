namespace ModernThinkPadLEDsController.Hardware;

/// <summary>
/// Thrown when the LHM (LibreHardwareMonitor) PawnIO driver cannot be opened at startup.
/// This is the signal for the application bootstrap to show the driver setup window.
/// </summary>
public sealed class LhmDriverInitializationException : InvalidOperationException
{
    public LhmDriverInitializationException()
    {
    }

    public LhmDriverInitializationException(string message) : base(message)
    {
    }

    public LhmDriverInitializationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
