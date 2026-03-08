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

    public HotkeyCycleOptions HotkeyCycleOptions { get; set; } = HotkeyCycleOptions.On | HotkeyCycleOptions.Off;

    // Hotkey configuration: modifier keys and virtual key code
    // Defaults to Win + Shift + K (MOD_WIN | MOD_SHIFT = 0x000C, VK_K = 0x4B)
    public int HotkeyModifiers { get; set; } = 0x000C; // MOD_WIN | MOD_SHIFT
    public int HotkeyVirtualKey { get; set; } = 0x4B;  // VK_K

    public int BlinkIntervalMs { get; set; } = 500;
    public int DiskPollIntervalMs { get; set; } = 300;

    public bool RememberKeyboardBacklight { get; set; }
    public int? SavedKeyboardBacklight { get; set; } // Raw brightness value (0-255); null means no saved value yet

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
                var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                settings.MigrateLegacyDiskPollInterval(json);
                settings.MigrateLegacyHotkeyCycleOptions(json);
                return settings;
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

    private void MigrateLegacyDiskPollInterval(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty(nameof(DiskPollIntervalMs), out _))
                return;

            if (root.TryGetProperty("HddPollIntervalMs", out var legacyInterval) &&
                legacyInterval.ValueKind == JsonValueKind.Number &&
                legacyInterval.TryGetInt32(out int legacyIntervalMs))
            {
                DiskPollIntervalMs = legacyIntervalMs;
            }
        }
        catch (JsonException)
        {
            // Ignore migration errors and keep the parsed/default value.
        }
    }

    private void MigrateLegacyHotkeyCycleOptions(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty(nameof(HotkeyCycleOptions), out _))
                return;

            bool foundLegacyValue = false;
            HotkeyCycleOptions options = HotkeyCycleOptions.None;

            if (root.TryGetProperty("HotkeyCycleOn", out var onProperty) &&
                onProperty.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                foundLegacyValue = true;
                if (onProperty.GetBoolean())
                    options |= HotkeyCycleOptions.On;
            }

            if (root.TryGetProperty("HotkeyCycleOff", out var offProperty) &&
                offProperty.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                foundLegacyValue = true;
                if (offProperty.GetBoolean())
                    options |= HotkeyCycleOptions.Off;
            }

            if (root.TryGetProperty("HotkeyCycleBlink", out var blinkProperty) &&
                blinkProperty.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                foundLegacyValue = true;
                if (blinkProperty.GetBoolean())
                    options |= HotkeyCycleOptions.Blink;
            }

            if (foundLegacyValue)
                HotkeyCycleOptions = options;
        }
        catch (JsonException)
        {
            // Ignore migration errors and keep the parsed/default value.
        }
    }
}
