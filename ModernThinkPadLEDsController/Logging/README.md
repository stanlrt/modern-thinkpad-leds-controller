# Logging

Application logging setup and logging policy using **Serilog**.

## Log destination files

### Primary

- **Location:** `%LOCALAPPDATA%\ModernThinkPadLEDsController\Logs\`
- **Name:** `app-YYYYMMDD.log` (e.g., `app-20260307.log`)
- **Rolling:** New file created daily
- **Retention:** Last 30 days kept automatically
- **Flushed:** Every 1 second to ensure logs are written even if app crashes

### Windows Event Log

- **Location:** Windows Event Viewer: `eventvwr.msc`
- **Source:** ModernThinkPadLEDsController
- **Log Name:** Application
- **Level:** Warning and above only
- Useful for system administrators and MSI-installed scenarios

## Log Levels

- **Verbose:** Detailed I/O operations (port reads/writes) - disabled by default to reduce noise
- **Debug:** Detailed diagnostic information (initialization steps, state changes)
- **Information:** General application flow (startup, shutdown, major operations)
- **Warning:** Unexpected but handled situations (failed operations, missing resources)
- **Error:** Exceptions and failures that prevent specific operations
- **Critical/Fatal:** Fatal errors that cause app shutdown

The level can be changed in `LoggingConfiguration.cs`:

```csharp
.MinimumLevel.Information()
```
