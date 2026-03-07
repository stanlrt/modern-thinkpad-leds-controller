using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace ModernThinkPadLEDsController.Hardware;

// PawnIOInstaller handles automatic installation of the PawnIO driver.
//
// PawnIO is distributed under GPL-2.0 and bundled with this application
// for user convenience. The installer is extracted from embedded resources
// and launched with administrator privileges when needed.
public static class PawnIOInstaller
{
    private const string InstallerResourceName = "ModernThinkPadLEDsController.Resources.PawnIO-Setup.exe";
    private const string TempInstallerName = "PawnIO-Setup.exe";

    private static readonly ILogger _logger = LoggerFactory.Create(builder =>
    {
        builder.AddDebug();
    }).CreateLogger("PawnIOInstaller");

    // Check if PawnIO is already installed by attempting to open the driver
    public static bool IsPawnIOInstalled()
    {
        try
        {
            return LhmDriver.TryOpen(out var driver) && driver != null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to check if PawnIO is installed");
            return false;
        }
    }

    // Extract and launch the PawnIO installer
    // Returns true if installer was launched successfully, false if extraction/launch failed
    public static bool InstallPawnIO()
    {
        try
        {
            _logger.LogInformation("Starting PawnIO installer extraction");

            // Extract installer from embedded resources to temp directory
            var tempPath = Path.Combine(Path.GetTempPath(), TempInstallerName);

            using (var resourceStream = typeof(PawnIOInstaller).Assembly.GetManifestResourceStream(InstallerResourceName))
            {
                if (resourceStream == null)
                {
                    // Installer not embedded - user needs to download manually
                    _logger.LogInformation("Embedded installer not found in resources");
                    return false;
                }

                _logger.LogDebug("Extracting installer to: {TempPath}", tempPath);

                using (var fileStream = File.Create(tempPath))
                {
                    resourceStream.CopyTo(fileStream);
                }
            }

            _logger.LogInformation("Launching PawnIO installer with elevation");

            // Launch the installer with admin privileges
            var startInfo = new ProcessStartInfo
            {
                FileName = tempPath,
                UseShellExecute = true,
                Verb = "runas" // Request elevation
            };

            var process = Process.Start(startInfo);
            if (process != null)
            {
                _logger.LogInformation("Waiting for installer to complete");

                // Wait for installer to complete
                process.WaitForExit();

                var exitCode = process.ExitCode;
                _logger.LogInformation("Installer completed with exit code: {ExitCode}", exitCode);

                // Clean up temp file
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
