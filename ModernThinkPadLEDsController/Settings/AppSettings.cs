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
/// Property initializers in this type are fallback values only. They are used for first-run
/// settings creation and for newly added properties missing from an older saved settings.json.
/// When a value already exists in the user's saved settings.json, deserialization preserves it.
/// </summary>
public sealed class AppSettings
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ModernThinkPadLEDsController",
        "settings.json");

    public LedMode PowerMode { get; set; } = AppSettingsDefaults.LED_MODE;
    public LedMode MuteMode { get; set; } = AppSettingsDefaults.LED_MODE;
    public LedMode RedDotMode { get; set; } = AppSettingsDefaults.LED_MODE;
    public LedMode MicrophoneMode { get; set; } = AppSettingsDefaults.LED_MODE;
    public LedMode SleepMode { get; set; } = AppSettingsDefaults.LED_MODE;
    public LedMode FnLockMode { get; set; } = AppSettingsDefaults.LED_MODE;
    public LedMode CameraMode { get; set; } = AppSettingsDefaults.LED_MODE;

    public byte? PowerCustomId { get; set; }
    public byte? MuteCustomId { get; set; }
    public byte? RedDotCustomId { get; set; }
    public byte? MicrophoneCustomId { get; set; }
    public byte? SleepCustomId { get; set; }
    public byte? FnLockCustomId { get; set; }
    public byte? CameraCustomId { get; set; }

    public HotkeyCycleOptions HotkeyCycleOptions { get; set; } = AppSettingsDefaults.HOTKEY_CYCLE_OPTIONS;

    /// <summary>
    /// Gets or sets the global hotkey binding.
    /// The fallback default is Win + Shift + K when no saved hotkey exists yet.
    /// </summary>
    public HotkeyBinding Hotkey { get; set; } = AppSettingsDefaults.CreateDefaultHotkeyBinding();

    public int BlinkIntervalMs { get; set; } = AppSettingsDefaults.BLINK_INTERVAL_MS;
    public int LedReapplyIntervalMs { get; set; } = AppSettingsDefaults.LED_REAPPLY_INTERVAL_MS;
    public int DiskPollIntervalMs { get; set; } = AppSettingsDefaults.DISK_POLL_INTERVAL_MS;

    public bool RememberKeyboardBacklight { get; set; } = AppSettingsDefaults.REMEMBER_KEYBOARD_BACKLIGHT;

    /// <summary>
    /// When enabled, the keyboard backlight level is periodically re-applied every LED-interval
    /// to prevent external changes from overriding the user's preferred level.
    /// </summary>
    public bool EnforceKeyboardBacklight { get; set; } = AppSettingsDefaults.ENFORCE_KEYBOARD_BACKLIGHT;

    /// <summary>
    /// Raw brightness value (0-255); null means no saved value yet
    /// </summary>
    public int? SavedKeyboardBacklight { get; set; }

    public bool DimLedsWhenFullscreen { get; set; } = AppSettingsDefaults.DIM_LEDS_WHEN_FULLSCREEN;
    public bool SuppressDiskCounterWarning { get; set; } = AppSettingsDefaults.SUPPRESS_DISK_COUNTER_WARNING;
    public bool PersistSettingsOnChange { get; set; } = AppSettingsDefaults.PERSIST_SETTINGS_ON_CHANGE;
    public bool EnableHardwareAccess { get; set; } = AppSettingsDefaults.ENABLE_HARDWARE_ACCESS;
    public string LogLevel { get; set; } = AppSettingsDefaults.LOG_LEVEL;

    public static AppSettings Load()
        => LoadFromPath(_filePath);

    internal static AppSettings LoadFromPath(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                return DeserializeAndNormalize(json);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load settings from {SettingsPath}; preserving file and using fallback defaults", filePath);
            PreserveUnreadableSettingsFile(filePath);
        }

        return CreateDefaultSettings();
    }

    internal static AppSettings DeserializeAndNormalize(string json)
    {
        AppSettings settings = JsonSerializer.Deserialize<AppSettings>(json) ?? CreateDefaultSettings();
        settings.Hotkey ??= AppSettingsDefaults.CreateDefaultHotkeyBinding();
        settings.Validate();
        return settings;
    }

    /// <summary>
    /// Clamps interval settings to safe operating ranges.
    /// Prevents monitors from crashing or spinning with extreme or negative values
    /// that could have been written by a hand-edited settings file.
    /// </summary>
    public void Validate()
    {
        BlinkIntervalMs = Math.Clamp(BlinkIntervalMs, AppSettingsDefaults.MIN_BLINK_INTERVAL_MS, AppSettingsDefaults.MAX_BLINK_INTERVAL_MS);
        LedReapplyIntervalMs = Math.Clamp(LedReapplyIntervalMs, AppSettingsDefaults.MIN_LED_REAPPLY_INTERVAL_MS, AppSettingsDefaults.MAX_LED_REAPPLY_INTERVAL_MS);
        DiskPollIntervalMs = Math.Clamp(DiskPollIntervalMs, AppSettingsDefaults.MIN_DISK_POLL_INTERVAL_MS, AppSettingsDefaults.MAX_DISK_POLL_INTERVAL_MS);
    }

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

    private static AppSettings CreateDefaultSettings() => new();

    private static void PreserveUnreadableSettingsFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            string directory = Path.GetDirectoryName(filePath)!;
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string backupPath = Path.Combine(directory, $"settings.corrupt.{timestamp}.json");

            for (int suffix = 1; File.Exists(backupPath); suffix++)
            {
                backupPath = Path.Combine(directory, $"settings.corrupt.{timestamp}.{suffix}.json");
            }

            File.Move(filePath, backupPath);
            Log.Warning("Preserved unreadable settings file at {BackupPath}", backupPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to preserve unreadable settings file {SettingsPath}", filePath);
        }
    }

}
