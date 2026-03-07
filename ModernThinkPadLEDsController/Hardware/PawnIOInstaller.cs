using System.Diagnostics;
using System.IO;

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

    // Check if PawnIO is already installed by attempting to open the driver
    public static bool IsPawnIOInstalled()
    {
        try
        {
            return LhmDriver.TryOpen(out var driver) && driver != null;
        }
        catch
        {
            return false;
        }
    }

    // Extract and launch the PawnIO installer
    // Returns true if installer was launched successfully, false if extraction/launch failed
    public static bool InstallPawnIO()
    {
        try
        {
            // Extract installer from embedded resources to temp directory
            var tempPath = Path.Combine(Path.GetTempPath(), TempInstallerName);

            using (var resourceStream = typeof(PawnIOInstaller).Assembly.GetManifestResourceStream(InstallerResourceName))
            {
                if (resourceStream == null)
                {
                    // Installer not embedded - user needs to download manually
                    return false;
                }

                using (var fileStream = File.Create(tempPath))
                {
                    resourceStream.CopyTo(fileStream);
                }
            }

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
                // Wait for installer to complete
                process.WaitForExit();

                // Clean up temp file
                try { File.Delete(tempPath); } catch { /* Best effort */ }

                return process.ExitCode == 0;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
