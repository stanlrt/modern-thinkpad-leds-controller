# Monitoring

OS and hardware state observers that publish events.

Polling observers implement `IMonitorLifecycle` so they share the same start/stop contract.
Window-hook observers implement `IWindowAttachedMonitor` when they need a WPF window handle before they can receive notifications.
