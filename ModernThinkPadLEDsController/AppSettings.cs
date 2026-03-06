using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModernThinkPadLEDsController;

// AppSettings is a simple class that holds every user preference.
// It is saved as a human-readable JSON file in:
//   %APPDATA%\ModernThinkPadLEDsController\settings.json
//
// JsonSerializer (built into .NET) converts the class to/from JSON automatically.
// Each property becomes one line in the file, e.g.:
//   "HddReadDot": true,
public sealed class AppSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ModernThinkPadLEDsController",
        "settings.json");

    // --- Disk activity → LED mapping ---
    // "Which LEDs should light up when the disk is being READ?"
    public bool HddReadPower      { get; set; }
    public bool HddReadRedDot     { get; set; }
    public bool HddReadMicrophone { get; set; }
    public bool HddReadSleep      { get; set; }
    public bool HddReadFn         { get; set; }

    // "Which LEDs should light up when the disk is being WRITTEN?"
    public bool HddWritePower      { get; set; }
    public bool HddWriteRedDot     { get; set; }
    public bool HddWriteMicrophone { get; set; }
    public bool HddWriteSleep      { get; set; }
    public bool HddWriteFn         { get; set; }

    // --- Caps Lock → LED mapping ---
    public bool CapsLockPower      { get; set; }
    public bool CapsLockRedDot     { get; set; }
    public bool CapsLockMicrophone { get; set; }
    public bool CapsLockSleep      { get; set; }
    public bool CapsLockFn         { get; set; }

    // --- Num Lock → LED mapping ---
    public bool NumLockPower      { get; set; }
    public bool NumLockRedDot     { get; set; }
    public bool NumLockMicrophone { get; set; }
    public bool NumLockSleep      { get; set; }
    public bool NumLockFn         { get; set; }

    // --- LED inversion ---
    // When enabled for a LED, On and Off are swapped everywhere.
    // Useful if you prefer an LED to be off during activity and on during idle.
    public bool InvertPower      { get; set; }
    public bool InvertRedDot     { get; set; }
    public bool InvertMicrophone { get; set; }
    public bool InvertSleep      { get; set; }
    public bool InvertFn         { get; set; }

    // --- Timing ---
    public int HddPollIntervalMs { get; set; } = 100;

    // --- Keyboard backlight ---
    public bool RememberKeyboardBacklight { get; set; }
    public int  SavedKeyboardBacklight    { get; set; } // 0=Off, 1=Low, 2=High

    // --- Behaviour toggles ---
    public bool DisableDiskMonitoring      { get; set; }
    public bool DisableKeyMonitoring       { get; set; }
    public bool DisableMicMonitoring       { get; set; }
    public bool DimLedsWhenFullscreen      { get; set; }
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
