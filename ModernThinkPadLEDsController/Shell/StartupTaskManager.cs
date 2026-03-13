using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Serilog;

namespace ModernThinkPadLEDsController.Shell;

public readonly record struct StartupTaskOperationResult(bool Success, string? ErrorMessage)
{
    public static StartupTaskOperationResult Successful() => new(true, null);

    public static StartupTaskOperationResult Failed(string errorMessage) => new(false, errorMessage);
}

/// <summary>
/// Creates / removes a Windows Task Scheduler entry so the
/// app starts automatically at logon with elevated (admin) privileges.
/// </summary>
internal static class StartupTaskManager
{
    private const string TASK_NAME = "ModernThinkPadLEDsController_Elevated";

    /// <summary>.
    /// </summary>
    /// <returns>
    /// Returns true if our scheduled task is registered on this machine.
    /// </returns>
    public static bool IsRegistered()
    {
        SchtasksProcessResult result = RunSchtasks($"/Query /TN \"{TASK_NAME}\"");

        if (result.ExitCode == 0)
        {
            return true;
        }

        if (result.ExitCode == 1)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            Log.Warning("Failed to query startup task registration: {ErrorMessage}", result.ErrorMessage);
        }

        return false;
    }

    /// <summary>
    /// Register the task. Returns true on success.
    /// executablePath = full path to the .exe (use Environment.ProcessPath).
    /// </summary>
    public static StartupTaskOperationResult Register(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return StartupTaskOperationResult.Failed("The application executable path is unavailable.");
        }

        string? tempXml = null;

        try
        {
            string username = WindowsIdentity.GetCurrent().Name;

            // schtasks.exe /Create /XML requires a file — write it to %TEMP%.
            tempXml = Path.Combine(Path.GetTempPath(), $"mtleds_task_{Guid.NewGuid():N}.xml");
            WriteTaskXml(tempXml, username, executablePath);

            SchtasksProcessResult result = RunSchtasks($"/Create /F /TN \"{TASK_NAME}\" /XML \"{tempXml}\"");
            if (result.ExitCode == 0)
            {
                return StartupTaskOperationResult.Successful();
            }

            string message = result.ErrorMessage ?? "Unknown error while registering the startup task.";
            Log.Warning("Failed to register startup task: {ErrorMessage}", message);
            return StartupTaskOperationResult.Failed(message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to register startup task");
            return StartupTaskOperationResult.Failed(ex.Message);
        }
        finally
        {
            TryDeleteTempFile(tempXml);
        }
    }

    /// <summary>
    /// Remove the task.
    /// </summary>
    /// <returns>Returns true on success.</returns>
    public static StartupTaskOperationResult Unregister()
    {
        SchtasksProcessResult result = RunSchtasks($"/Delete /F /TN \"{TASK_NAME}\"");
        if (result.ExitCode == 0)
        {
            return StartupTaskOperationResult.Successful();
        }

        string message = result.ErrorMessage ?? "Unknown error while removing the startup task.";
        Log.Warning("Failed to remove startup task: {ErrorMessage}", message);
        return StartupTaskOperationResult.Failed(message);
    }

    /// <summary>
    /// Builds the XML that describes the scheduled task.
    /// RunLevel=HighestAvailable is what makes the task run as admin silently.
    /// The app receives --minimized so it starts hidden in the tray.
    /// </summary>
    private static void WriteTaskXml(string filePath, string username, string executablePath)
    {
        string? workingDirectory = Path.GetDirectoryName(executablePath);

        XNamespace taskNamespace = "http://schemas.microsoft.com/windows/2004/02/mit/task";

        // Build the Exec element with Command and Arguments
        XElement execElement = new(taskNamespace + "Exec",
            new XElement(taskNamespace + "Command", executablePath),
            new XElement(taskNamespace + "Arguments", "--minimized"));

        // Add WorkingDirectory if available
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            execElement.Add(new XElement(taskNamespace + "WorkingDirectory", workingDirectory));
        }

        XDocument document = new(
          new XDeclaration("1.0", "utf-16", null),
          new XElement(taskNamespace + "Task",
            new XAttribute("version", "1.3"),
            new XElement(taskNamespace + "Triggers",
              new XElement(taskNamespace + "LogonTrigger",
                new XElement(taskNamespace + "Enabled", true),
                new XElement(taskNamespace + "UserId", username))),
            new XElement(taskNamespace + "Principals",
              new XElement(taskNamespace + "Principal",
                new XAttribute("id", "Author"),
                new XElement(taskNamespace + "UserId", username),
                new XElement(taskNamespace + "LogonType", "InteractiveToken"),
                new XElement(taskNamespace + "RunLevel", "HighestAvailable"))),
            new XElement(taskNamespace + "Settings",
              new XElement(taskNamespace + "MultipleInstancesPolicy", "IgnoreNew"),
              new XElement(taskNamespace + "DisallowStartIfOnBatteries", false),
              new XElement(taskNamespace + "StopIfGoingOnBatteries", false),
              new XElement(taskNamespace + "ExecutionTimeLimit", "PT0S")),
            new XElement(taskNamespace + "Actions", execElement)));

        XmlWriterSettings settings = new()
        {
            Encoding = Encoding.Unicode,
            Indent = true,
        };

        using XmlWriter writer = XmlWriter.Create(filePath, settings);
        document.Save(writer);
    }

    private static void TryDeleteTempFile(string? filePath)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to delete temporary startup task XML file {FilePath}", filePath);
        }
    }

    private static SchtasksProcessResult RunSchtasks(string arguments)
    {
        try
        {
            using Process? proc = Process.Start(new ProcessStartInfo("schtasks.exe")
            {
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });

            if (proc is null)
            {
                return SchtasksProcessResult.Failed("Failed to start schtasks.exe.");
            }

            string standardOutput = proc.StandardOutput.ReadToEnd();
            string standardError = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            string? errorMessage = null;
            if (proc.ExitCode != 0)
            {
                string details = string.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError;
                details = details.Trim();
                errorMessage = string.IsNullOrWhiteSpace(details)
                  ? $"schtasks.exe exited with code {proc.ExitCode}."
                  : details;
            }

            return new SchtasksProcessResult(proc.ExitCode, errorMessage);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to execute schtasks.exe with arguments {Arguments}", arguments);
            return SchtasksProcessResult.Failed(ex.Message);
        }
    }

    private readonly record struct SchtasksProcessResult(int ExitCode, string? ErrorMessage)
    {
        public static SchtasksProcessResult Failed(string errorMessage) => new(-1, errorMessage);
    }
}
