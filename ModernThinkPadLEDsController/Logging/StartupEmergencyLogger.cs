using System.IO;

namespace ModernThinkPadLEDsController.Logging;

/// <summary>
/// Writes best-effort startup diagnostics before the main logging pipeline is available.
/// </summary>
public sealed class StartupEmergencyLogger
{
    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ModernThinkPadLEDsController",
        "Logs");

    public static string LogPath { get; } = Path.Combine(LogDirectory, "emergency.log");

    private readonly string _source;

    public StartupEmergencyLogger(string source)
    {
        _source = source;
    }

    public void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{_source}] {message}\n");
        }
        catch
        {
            // Best-effort only during early startup.
        }
    }

    public void LogException(string prefix, Exception ex)
    {
        Log($"{prefix}: {ex.GetType().Name}: {ex.Message}");
        Log($"Stack Trace: {ex.StackTrace}");

        if (ex.InnerException != null)
        {
            Log($"Inner Exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            Log($"Inner Stack Trace: {ex.InnerException.StackTrace}");
        }
    }
}
