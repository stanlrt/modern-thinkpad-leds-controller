using System.IO;
using System.Text.Json;

namespace ModernThinkPadLEDsController;

/// <summary>
/// Represents the application settings, stored in JSON at %APPDATA%\ModernThinkPadLEDsController\settings.json.
/// </summary>
public sealed class AppSettings
{
    private static readonly string FilePath = Path.Combine(
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

    public bool HotkeyCycleOn { get; set; } = true;
    public bool HotkeyCycleOff { get; set; } = true;
    public bool HotkeyCycleBlink { get; set; } = false;

    public int BlinkIntervalMs { get; set; } = 500;
    public int HddPollIntervalMs { get; set; } = 300;

    public bool RememberKeyboardBacklight { get; set; }
    public int SavedKeyboardBacklight { get; set; } // 0=Off, 1=Low, 2=High

    public bool DimLedsWhenFullscreen { get; set; }
    public bool SuppressDiskCounterWarning { get; set; }
    public bool PersistSettingsOnChange { get; set; } = true;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { /* first run or corrupt file — use defaults */ }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, options));
        }
        catch { /* saving is best-effort — never crash the app */ }
    }
}
