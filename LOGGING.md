# Logging Guide

## Overview

The application now includes comprehensive structured logging using **Serilog** following modern .NET 10 guidelines. This will help diagnose issues when the app runs as an MSI-installed application versus local development.

## Log Locations

Logs are written to multiple sinks for maximum diagnostics capability:

### 1. File Logs (Primary)
**Location:** `%LOCALAPPDATA%\ModernThinkPadLEDsController\Logs\`
- Full path example: `C:\Users\YourName\AppData\Local\ModernThinkPadLEDsController\Logs\`
- Files are named: `app-YYYYMMDD.log` (e.g., `app-20260307.log`)
- **Rolling:** New file created daily
- **Retention:** Last 30 days kept automatically
- **Flushed:** Every 1 second to ensure logs are written even if app crashes

### 2. Console Output
- Visible when running with `dotnet run` in development
- Great for local debugging
- Not available when installed via MSI

### 3. Windows Event Log
- **Source:** ModernThinkPadLEDsController
- **Log Name:** Application
- **Level:** Warning and above only
- Useful for system administrators and MSI-installed scenarios
- View in Windows Event Viewer: `eventvwr.msc`

## Log Levels

- **Verbose:** Detailed I/O operations (port reads/writes) - disabled by default to reduce noise
- **Debug:** Detailed diagnostic information (initialization steps, state changes)
- **Information:** General application flow (startup, shutdown, major operations)
- **Warning:** Unexpected but handled situations (failed operations, missing resources)
- **Error:** Exceptions and failures that prevent specific operations
- **Critical/Fatal:** Fatal errors that cause app shutdown

## Logged Information at Startup

The application logs comprehensive environment details on every startup:

- Application version
- Log directory path
- OS version and architecture (32-bit/64-bit)
- Command line arguments
- Current directory and process path
- User and domain information
- Administrator privilege status
- Processor count and system details

## Key Areas with Logging

### Hardware Initialization
- **LhmDriver:** PawnIO service detection, driver loading
- **EcController:** Embedded Controller communication
- **LedController:** LED state changes, keyboard backlight

### Monitoring Services
- Disk activity state changes
- Microphone mute/unmute events
- Keyboard backlight level changes
- Power events (suspend/resume, lid open/close, fullscreen detection)

### Application Lifecycle
- Single instance check
- Window show/hide events
- Settings save/load operations
- Clean shutdown sequence

### Exception Handling
All unhandled exceptions are logged with full stack traces:
- UI thread exceptions
- Background thread exceptions
- Unobserved task exceptions

## Troubleshooting MSI Installation Issues

If the app works with `dotnet run` but crashes when installed via MSI:

1. **Check the log files first:**
   ```
   %LOCALAPPDATA%\ModernThinkPadLEDsController\Logs\app-YYYYMMDD.log
   ```

2. **Look for:**
   - Last log entry before crash (indicates where it failed)
   - PawnIO service status messages
   - Permission/access denied errors
   - File path differences between dev and installed versions

3. **Check Windows Event Viewer:**
   - Open `eventvwr.msc`
   - Navigate to Windows Logs → Application
   - Look for "ModernThinkPadLEDsController" source entries

4. **Common issues to look for in logs:**
   - "PawnIO service not running" - Driver not installed/started
   - "Failed to open LHM driver" - Permissions or driver issues
   - Working directory mismatches - Embedded resources not found
   - Administrator privilege differences

## Viewing Logs

### Via File Explorer
Press `Win+R`, paste this, and hit Enter:
```
%LOCALAPPDATA%\ModernThinkPadLEDsController\Logs
```

### Via PowerShell
```powershell
# View latest log
Get-Content "$env:LOCALAPPDATA\ModernThinkPadLEDsController\Logs\app-*.log" -Tail 50 -Wait

# Search for errors
Select-String -Path "$env:LOCALAPPDATA\ModernThinkPadLEDsController\Logs\*.log" -Pattern "ERROR|FATAL"
```

### Via Event Viewer
```cmd
eventvwr.msc
```
Navigate to: Windows Logs → Application → Filter by "ModernThinkPadLEDsController"

## Reducing Log Verbosity

Edit `LoggingConfiguration.cs` and change the minimum level:
```csharp
.MinimumLevel.Information()  // Instead of .MinimumLevel.Debug()
```

## Increasing Log Detail

For deep troubleshooting, enable Verbose logging:
```csharp
.MinimumLevel.Verbose()
```
⚠️ **Warning:** Verbose mode logs every single port I/O operation and can generate very large log files.

## Performance Impact

The logging system is designed for minimal performance impact:
- Asynchronous file writes
- Structured logging with message templates (no string interpolation unless needed)
- Automatic batching and buffering
- Logs flushed every 1 second (configurable)

For typical usage, logging overhead is negligible (<1% CPU).
