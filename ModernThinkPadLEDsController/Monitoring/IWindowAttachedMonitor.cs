using System.Windows;

namespace ModernThinkPadLEDsController.Monitoring;

/// <summary>
/// Represents a monitor that must attach to a window to receive notifications.
/// </summary>
public interface IWindowAttachedMonitor : IDisposable
{
    void Attach(Window window);
}
