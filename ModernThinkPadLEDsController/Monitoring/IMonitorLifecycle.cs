namespace ModernThinkPadLEDsController.Monitoring;

/// <summary>
/// Represents a monitor that can be started and stopped at runtime.
/// </summary>
public interface ILifecycleMonitor : IDisposable
{
    void Start();

    void Stop();
}
