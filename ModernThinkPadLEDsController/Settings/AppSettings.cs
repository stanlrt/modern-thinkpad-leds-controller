using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;
using ModernThinkPadLEDsController.Lighting;
using ModernThinkPadLEDsController.Shell;
using Serilog;

namespace ModernThinkPadLEDsController;

/// <summary>
/// Represents the application settings, stored in JSON at %APPDATA%\ModernThinkPadLEDsController\settings.json.
/// </summary>
public sealed class AppSettings
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ModernThinkPadLEDsController",
        "settings.json");

    public LedMode PowerMode { get; set; } = LedMode.Default;
    public LedMode MuteMode { get; set; } = LedMode.Default;
    public LedMode RedDotMode { get; set; } = LedMode.Default;
    public LedMode MicrophoneMode { get; set; } = LedMode.Default;
    public LedMode SleepMode { get; set; } = LedMode.Default;
    public LedMode FnLockMode { get; set; } = LedMode.Default;
    public LedMode CameraMode { get; set; } = LedMode.Default;

    public byte? PowerCustomId { get; set; }
    public byte? MuteCustomId { get; set; }
    public byte? RedDotCustomId { get; set; }
    public byte? MicrophoneCustomId { get; set; }
    public byte? SleepCustomId { get; set; }
    public byte? FnLockCustomId { get; set; }
    public byte? CameraCustomId { get; set; }

    public HotkeyCycleOptions HotkeyCycleOptions { get; set; } = HotkeyCycleOptions.On | HotkeyCycleOptions.Off;

    /// <summary>
    /// Gets or sets the global hotkey binding.
    /// Default is Win + Shift + K.
    /// </summary>
    public HotkeyBinding Hotkey { get; set; } = new();

    public int BlinkIntervalMs { get; set; } = 500;
    public int LedReapplyIntervalMs { get; set; } = 1000;
    public int DiskPollIntervalMs { get; set; } = 300;

    public bool RememberKeyboardBacklight { get; set; }

    /// <summary>
    /// Raw brightness value (0-255); null means no saved value yet
    /// </summary>
    public int? SavedKeyboardBacklight { get; set; }

    public bool DimLedsWhenFullscreen { get; set; }
    public bool SuppressDiskCounterWarning { get; set; }
    public bool PersistSettingsOnChange { get; set; } = true;
    public bool EnableHardwareAccess { get; set; } = true;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                string json = File.ReadAllText(_filePath);
                AppSettings settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                settings.Hotkey ??= new HotkeyBinding();
                settings.Validate();
                return settings;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load settings from {SettingsPath}; using defaults", _filePath);
            PreserveUnreadableSettingsFile();
        }

        return new AppSettings();
    }

    /// <summary>
    /// Clamps interval settings to safe operating ranges.
    /// Prevents monitors from crashing or spinning with extreme or negative values
    /// that could have been written by a hand-edited settings file.
    /// </summary>
    public void Validate()
    {
        BlinkIntervalMs = Math.Clamp(BlinkIntervalMs, MinBlinkIntervalMs, MaxBlinkIntervalMs);
        LedReapplyIntervalMs = Math.Clamp(LedReapplyIntervalMs, MinLedReapplyIntervalMs, MaxLedReapplyIntervalMs);
        DiskPollIntervalMs = Math.Clamp(DiskPollIntervalMs, MinDiskPollIntervalMs, MaxDiskPollIntervalMs);
    }

    public const int MinBlinkIntervalMs = 100;
    public const int MaxBlinkIntervalMs = 10_000;
    public const int MinLedReapplyIntervalMs = 250;
    public const int MaxLedReapplyIntervalMs = 10_000;
    public const int MinDiskPollIntervalMs = 100;
    public const int MaxDiskPollIntervalMs = 10_000;

    public bool Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            JsonSerializerOptions options = new() { WriteIndented = true };
            File.WriteAllText(_filePath, JsonSerializer.Serialize(this, options));
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save settings to {SettingsPath}", _filePath);
            return false;
        }
    }

    private static void PreserveUnreadableSettingsFile()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return;
            }

            string directory = Path.GetDirectoryName(_filePath)!;
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string backupPath = Path.Combine(directory, $"settings.corrupt.{timestamp}.json");

            for (int suffix = 1; File.Exists(backupPath); suffix++)
            {
                backupPath = Path.Combine(directory, $"settings.corrupt.{timestamp}.{suffix}.json");
            }

            File.Move(_filePath, backupPath);
            Log.Warning("Preserved unreadable settings file at {BackupPath}", backupPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to preserve unreadable settings file {SettingsPath}", _filePath);
        }
    }

}
