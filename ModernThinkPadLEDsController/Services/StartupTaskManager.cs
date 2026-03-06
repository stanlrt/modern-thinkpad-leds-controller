using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;

namespace ModernThinkPadLEDsController.Services;

// StartupTaskManager creates / removes a Windows Task Scheduler entry so the
// app starts automatically at logon with elevated (admin) privileges.
//
// Why Task Scheduler instead of the registry Run key?
//   The registry Run key starts the app at login but Windows will show a UAC
//   prompt every time because the app requires admin rights.
//   A scheduled task with RunLevel=HighestAvailable starts the app as admin
//   silently — no prompt, because the task was already approved once when it
//   was created (which required admin rights to do).
internal static class StartupTaskManager
{
    private const string TaskName = "ModernThinkPadLEDsController_Elevated";

    // Returns true if our scheduled task is registered on this machine.
    public static bool IsRegistered()
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo("schtasks.exe")
            {
                Arguments = $"/Query /TN \"{TaskName}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            })!;
            proc.WaitForExit();
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

    // Register the task. Returns true on success.
    // executablePath = full path to the .exe (use Environment.ProcessPath).
    public static bool Register(string executablePath)
    {
        try
        {
            string username = WindowsIdentity.GetCurrent().Name;
            string xml = BuildTaskXml(username, executablePath);

            // schtasks.exe /Create /XML requires a file — write it to %TEMP%.
            string tempXml = Path.Combine(Path.GetTempPath(), "mtleds_task.xml");
            File.WriteAllText(tempXml, xml, Encoding.Unicode);

            using var proc = Process.Start(new ProcessStartInfo("schtasks.exe")
            {
                Arguments = $"/Create /F /TN \"{TaskName}\" /XML \"{tempXml}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            })!;
            proc.WaitForExit();
            File.Delete(tempXml);

            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

    // Remove the task. Returns true on success.
    public static bool Unregister()
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo("schtasks.exe")
            {
                Arguments = $"/Delete /F /TN \"{TaskName}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            })!;
            proc.WaitForExit();
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

    // Builds the XML that describes the scheduled task.
    // RunLevel=HighestAvailable is what makes the task run as admin silently.
    // The app receives --minimized so it starts hidden in the tray.
    private static string BuildTaskXml(string username, string executablePath) => $"""
        <?xml version="1.0" encoding="UTF-16"?>
        <Task version="1.3" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
          <Triggers>
            <LogonTrigger>
              <Enabled>true</Enabled>
              <UserId>{username}</UserId>
            </LogonTrigger>
          </Triggers>
          <Principals>
            <Principal id="Author">
              <UserId>{username}</UserId>
              <LogonType>InteractiveToken</LogonType>
              <RunLevel>HighestAvailable</RunLevel>
            </Principal>
          </Principals>
          <Settings>
            <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
            <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
            <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
            <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
          </Settings>
          <Actions>
            <Exec>
              <Command>{executablePath}</Command>
              <Arguments>--minimized</Arguments>
            </Exec>
          </Actions>
        </Task>
        """;
}
