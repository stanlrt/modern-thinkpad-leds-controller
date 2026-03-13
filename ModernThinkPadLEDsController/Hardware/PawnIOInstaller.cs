using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace ModernThinkPadLEDsController.Hardware;

/// <summary>
/// Helper class to manage PawnIO installation and verification.
/// </summary>
public static class PawnIOInstaller
{
    private const string INSTALLER_RESOURCE_NAME = "ModernThinkPadLEDsController.Resources.PawnIO-Setup.exe";
    private const string TEMP_INSTALLER_NAME = "PawnIO-Setup.exe";

    private static readonly ILogger _logger = LoggerFactory.Create(_ => { }).CreateLogger("PawnIOInstaller");

    public static bool IsPawnIOInstalled()
    {
        try
        {
            if (!LhmDriver.TryOpen(out LhmDriver? driver) || driver is null)
            {
                return false;
            }

            using (driver)
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to check if PawnIO is installed");
            return false;
        }
    }

    /// <summary>
    /// Extracts and launches the PawnIO installer.
    /// </summary>
    /// <returns>true if installer was launched successfully, false if extraction/launch failed</returns>
    public static bool InstallPawnIO()
    {
        try
        {
            _logger.LogInformation("Starting PawnIO installer extraction");

            // Extract installer from embedded resources to temp directory
            string tempPath = Path.Combine(Path.GetTempPath(), TEMP_INSTALLER_NAME);

            using (Stream? resourceStream = typeof(PawnIOInstaller).Assembly.GetManifestResourceStream(INSTALLER_RESOURCE_NAME))
            {
                if (resourceStream == null)
                {
                    _logger.LogInformation("Embedded installer not found in resources");
                    return false;
                }

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Extracting installer to: {TempPath}", tempPath);
                }

                using (FileStream fileStream = File.Create(tempPath))
                {
                    resourceStream.CopyTo(fileStream);
                }
            }

            _logger.LogInformation("Launching PawnIO installer with elevation");

            ProcessStartInfo startInfo = new()
            {
                FileName = tempPath,
                UseShellExecute = true,
                Verb = "runas" // Request elevation
            };

            Process? process = Process.Start(startInfo);
            if (process != null)
            {
                _logger.LogInformation("Waiting for installer to complete");

                process.WaitForExit();

                int exitCode = process.ExitCode;
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Installer completed with exit code: {ExitCode}", exitCode);
                }

                try
                {
                    File.Delete(tempPath);
                    _logger.LogDebug("Cleaned up temporary installer file");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary installer file: {TempPath}", tempPath);
                }

                return exitCode == 0;
            }

            _logger.LogWarning("Failed to start installer process");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install PawnIO");
            return false;
        }
    }
}
