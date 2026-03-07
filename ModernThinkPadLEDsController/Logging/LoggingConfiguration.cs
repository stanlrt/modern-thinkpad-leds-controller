using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using System.IO;

namespace ModernThinkPadLEDsController.Logging;

/// <summary>
/// Configures structured logging for the application using Serilog.
/// Logs are written to multiple sinks: file (with rolling), console, and Windows Event Log.
/// </summary>
public static class LoggingConfiguration
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ModernThinkPadLEDsController",
        "Logs");

    private static readonly string EmergencyLogPath = Path.Combine(LogDirectory, "emergency.log");

    /// <summary>
    /// Writes to emergency log file when Serilog hasn't been initialized yet
    /// </summary>
    private static void EmergencyLog(string message)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(EmergencyLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n");
        }
        catch { /* If we can't log, we can't log */ }
    }

    /// <summary>
    /// Configures Serilog with appropriate sinks and formatting for this application.
    /// </summary>
    public static void ConfigureSerilog()
    {
        try
        {
            EmergencyLog("=== ConfigureSerilog() called ===");
            EmergencyLog($"Current Directory: {Environment.CurrentDirectory}");
            EmergencyLog($"Process Path: {Environment.ProcessPath}");
            EmergencyLog($"Log Directory: {LogDirectory}");

            // Ensure log directory exists
            Directory.CreateDirectory(LogDirectory);
            EmergencyLog("Log directory created/verified");

            // Ensure log directory exists
            Directory.CreateDirectory(LogDirectory);
            EmergencyLog("Log directory created/verified");

            var logFilePath = Path.Combine(LogDirectory, "app-.log");
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
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                    shared: false,
                    flushToDiskInterval: TimeSpan.FromSeconds(1))

                // Windows Event Log sink - important for MSI-installed apps
                // This helps diagnose issues when the app is installed as a service or with elevated permissions
                .WriteTo.EventLog(
                    source: "ModernThinkPadLEDsController",
                    logName: "Application",
                    restrictedToMinimumLevel: LogEventLevel.Warning,
                    manageEventSource: false) // Don't try to create event source (requires admin)

                .CreateLogger();

            EmergencyLog("Serilog configured successfully");

            Log.Information("═══════════════════════════════════════════════════════════");
            Log.Information("Modern ThinkPad LEDs Controller Starting");
            Log.Information("Version: {Version}", GetAppVersion());
            Log.Information("Log Directory: {LogDirectory}", LogDirectory);
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
        var version = typeof(LoggingConfiguration).Assembly.GetName().Version;
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
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
