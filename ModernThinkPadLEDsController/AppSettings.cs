using System.IO;
using System.Text.Json;

namespace ModernThinkPadLEDsController;

// AppSettings is a simple class that holds every user preference.
// It is saved as a human-readable JSON file in:
//   %APPDATA%\ModernThinkPadLEDsController\settings.json
public sealed class AppSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ModernThinkPadLEDsController",
        "settings.json");

    // --- LED modes ---
    // Each LED has exactly one mode (mutually exclusive).
    public LedMode PowerMode { get; set; } = LedMode.Default;
    public LedMode RedDotMode { get; set; } = LedMode.Default;
    public LedMode MicrophoneMode { get; set; } = LedMode.Default;
    public LedMode SleepMode { get; set; } = LedMode.Default;
    public LedMode FnLockMode { get; set; } = LedMode.Default;

    // --- Hotkey cycle (Win+Shift+K) ---
    // Which states LEDs in HotkeyControlled mode should cycle through.
    public bool HotkeyCycleOn { get; set; } = true;
    public bool HotkeyCycleOff { get; set; } = true;
    public bool HotkeyCycleBlink { get; set; } = false;

    // --- Timing ---
    public int HddPollIntervalMs { get; set; } = 300;

    // --- Keyboard backlight ---
    public bool RememberKeyboardBacklight { get; set; }
    public int SavedKeyboardBacklight { get; set; } // 0=Off, 1=Low, 2=High

    // --- Behaviour ---
    public bool DimLedsWhenFullscreen { get; set; }
    public bool SuppressDiskCounterWarning { get; set; }

    // --- Load/Save ---

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
