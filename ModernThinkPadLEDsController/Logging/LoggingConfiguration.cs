using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using System.IO;
using System.Security.Principal;

namespace ModernThinkPadLEDsController.Logging;

/// <summary>
/// Configures structured logging for the application using Serilog.
/// Logs are written to multiple sinks: file (with rolling), console, and Windows Event Log.
/// </summary>
public static class LoggingConfiguration
{
    private static readonly string _logDirectory = StartupEmergencyLogger.LogDirectory;
    private static readonly StartupEmergencyLogger _emergencyLogger = new(nameof(LoggingConfiguration));

    /// <summary>
    /// Writes to emergency log file when Serilog hasn't been initialized yet
    /// </summary>
    private static void EmergencyLog(string message)
    {
        _emergencyLogger.Log(message);
    }

    /// <summary>
    /// Configures Serilog with appropriate sinks and formatting for this application.
    /// </summary>
    /// <exception cref="InvalidOperationException">Fatal errors that cannot be recovered</exception>
    public static void ConfigureSerilog()
    {
        try
        {
            EmergencyLog("=== ConfigureSerilog() called ===");
            EmergencyLog($"Current Directory: {Environment.CurrentDirectory}");
            EmergencyLog($"Process Path: {Environment.ProcessPath}");
            EmergencyLog($"Log Directory: {_logDirectory}");

            // Ensure log directory exists
            Directory.CreateDirectory(_logDirectory);
            EmergencyLog("Log directory created/verified");

            // Ensure log directory exists
            Directory.CreateDirectory(_logDirectory);
            EmergencyLog("Log directory created/verified");

            string logFilePath = Path.Combine(_logDirectory, "app-.log");
            EmergencyLog($"Log file path: {logFilePath}");

            EmergencyLog("Configuring Serilog LoggerConfiguration...");
            Log.Logger = new LoggerConfiguration()
                // Set minimum log level - Information for production, Debug for troubleshooting
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)

                // Enrich logs with additional context
                .Enrich.FromLogContext()
                .Enrich.WithProcessId()
                .Enrich.WithMachineName()

                // Console sink - useful when running with dotnet run
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")

                // File sink - rolling daily, keep 30 days of logs
                .WriteTo.File(
                    path: logFilePath,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                    shared: false,
                    flushToDiskInterval: TimeSpan.FromSeconds(1),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30)

                // Windows Event Log sink - important for MSI-installed apps
                // This helps diagnose issues when the app is installed as a service or with elevated permissions
                .WriteTo.EventLog(
                    source: "ModernThinkPadLEDsController",
                    logName: "Application",
                    manageEventSource: false,
                    restrictedToMinimumLevel: LogEventLevel.Warning) // Don't try to create event source (requires admin)

                .CreateLogger();

            EmergencyLog("Serilog configured successfully");

            Log.Information("═══════════════════════════════════════════════════════════");
            Log.Information("Modern ThinkPad LEDs Controller Starting");
            Log.Information("Version: {Version}", GetAppVersion());
            Log.Information("Log Directory: {LogDirectory}", _logDirectory);
            Log.Information("OS Version: {OSVersion}", Environment.OSVersion);
            Log.Information("Is 64-bit Process: {Is64Bit}", Environment.Is64BitProcess);
            Log.Information("Command Line: {CommandLine}", Environment.CommandLine);
            Log.Information("Current Directory: {CurrentDirectory}", Environment.CurrentDirectory);
            Log.Information("═══════════════════════════════════════════════════════════");

            EmergencyLog("Initial log messages written successfully");
        }
        catch (Exception ex)
        {
            EmergencyLog($"FATAL ERROR configuring Serilog: {ex.GetType().Name}: {ex.Message}");
            EmergencyLog($"Stack Trace: {ex.StackTrace}");

            // Rethrow so the app shows the error
            throw new InvalidOperationException("Failed to initialize logging system. See emergency.log for details.", ex);
        }
    }

    /// <summary>
    /// Adds Serilog to the host builder's logging pipeline.
    /// </summary>
    public static IHostBuilder ConfigureLogging(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseSerilog();
    }

    /// <summary>
    /// Ensures logs are flushed before application exit.
    /// Always call this in finally blocks or shutdown handlers.
    /// </summary>
    public static void CloseAndFlush()
    {
        Log.Information("Application shutting down");
        Log.CloseAndFlush();
    }

    /// <summary>
    /// Gets the application version from assembly metadata.
    /// </summary>
    private static string GetAppVersion()
    {
        Version? version = typeof(LoggingConfiguration).Assembly.GetName().Version;
        return version?.ToString() ?? "Unknown";
    }

    /// <summary>
    /// Logs detailed environment information for troubleshooting.
    /// Call this during startup to capture the execution environment.
    /// </summary>
    public static void LogEnvironmentDetails()
    {
        Log.Debug("=== Detailed Environment Information ===");
        Log.Debug("CLR Version: {ClrVersion}", Environment.Version);
        Log.Debug("User: {UserName}", Environment.UserName);
        Log.Debug("User Domain: {UserDomain}", Environment.UserDomainName);
        Log.Debug("Is Administrator: {IsAdmin}", IsRunningAsAdministrator());
        Log.Debug("Working Directory: {WorkingDir}", Environment.CurrentDirectory);
        Log.Debug("Process Path: {ProcessPath}", Environment.ProcessPath);
        Log.Debug("System Directory: {SystemDir}", Environment.SystemDirectory);
        Log.Debug("Processor Count: {ProcessorCount}", Environment.ProcessorCount);
        Log.Debug("System Page Size: {PageSize}", Environment.SystemPageSize);
        Log.Debug("=== End Environment Information ===");
    }

    private static bool IsRunningAsAdministrator()
    {
        try
        {
            WindowsIdentity identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
