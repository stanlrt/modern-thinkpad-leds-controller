using FluentAssertions;
using ModernThinkPadLEDsController.Lighting;
using ModernThinkPadLEDsController.Shell;
using Xunit;

namespace ModernThinkPadLEDsController.Settings.Tests;

/// <summary>
/// Unit tests for <see cref="AppSettings.Validate"/>.
/// Verifies that interval values are clamped to their safe operating ranges.
/// </summary>
public sealed class AppSettingsValidationTests
{
    [Fact]
    public void LoadFromMissingPath_UsesCentralizedFallbackDefaults()
    {
        string settingsPath = CreateTempSettingsPath();

        try
        {
            AppSettings settings = AppSettings.LoadFromPath(settingsPath);

            AssertUsesCentralizedFallbackDefaults(settings);
        }
        finally
        {
            DeleteTempSettingsPath(settingsPath);
        }
    }

    [Fact]
    public void LoadFromPath_MissingFieldsAndNullHotkey_UsesFallbacksWithoutThrowing()
    {
        string settingsPath = CreateTempSettingsPath();
        const string json = """
            {
              "PowerMode": 1,
              "Hotkey": null,
              "PersistSettingsOnChange": false
            }
            """;

        try
        {
            File.WriteAllText(settingsPath, json);

            AppSettings settings = AppSettings.LoadFromPath(settingsPath);

            settings.PowerMode.Should().Be((LedMode)1);
            settings.MuteMode.Should().Be(AppSettingsDefaults.LED_MODE);
            settings.Hotkey.Should().NotBeNull();
            settings.Hotkey.Modifiers.Should().Be(AppSettingsDefaults.HOTKEY_MODIFIERS);
            settings.Hotkey.VirtualKey.Should().Be(AppSettingsDefaults.HOTKEY_VIRTUAL_KEY);
            settings.PersistSettingsOnChange.Should().BeFalse();
            settings.EnableHardwareAccess.Should().Be(AppSettingsDefaults.ENABLE_HARDWARE_ACCESS);
        }
        finally
        {
            DeleteTempSettingsPath(settingsPath);
        }
    }

    [Fact]
    public void LoadFromPath_WithExplicitValues_PreservesSavedValues()
    {
        string settingsPath = CreateTempSettingsPath();
        const string json = """
            {
              "PowerMode": 1,
              "HotkeyCycleOptions": 4,
              "Hotkey": {
                "Modifiers": 1,
                "VirtualKey": 65
              },
              "BlinkIntervalMs": 900,
              "LedReapplyIntervalMs": 1200,
              "DiskPollIntervalMs": 450,
              "RememberKeyboardBacklight": true,
              "DimLedsWhenFullscreen": true,
              "SuppressDiskCounterWarning": true,
              "PersistSettingsOnChange": false,
              "EnableHardwareAccess": false
            }
            """;

        try
        {
            File.WriteAllText(settingsPath, json);

            AppSettings settings = AppSettings.LoadFromPath(settingsPath);

            settings.PowerMode.Should().Be((LedMode)1);
            settings.HotkeyCycleOptions.Should().Be((HotkeyCycleOptions)4);
            settings.Hotkey.Modifiers.Should().Be((HotkeyModifiers)1);
            settings.Hotkey.VirtualKey.Should().Be(65);
            settings.BlinkIntervalMs.Should().Be(900);
            settings.LedReapplyIntervalMs.Should().Be(1200);
            settings.DiskPollIntervalMs.Should().Be(450);
            settings.RememberKeyboardBacklight.Should().BeTrue();
            settings.DimLedsWhenFullscreen.Should().BeTrue();
            settings.SuppressDiskCounterWarning.Should().BeTrue();
            settings.PersistSettingsOnChange.Should().BeFalse();
            settings.EnableHardwareAccess.Should().BeFalse();
        }
        finally
        {
            DeleteTempSettingsPath(settingsPath);
        }
    }

    [Fact]
    public void LoadFromPath_OutOfRangeIntervals_AreClampedDuringInitialization()
    {
        string settingsPath = CreateTempSettingsPath();
        const string json = """
            {
              "BlinkIntervalMs": 1,
              "LedReapplyIntervalMs": 99999,
              "DiskPollIntervalMs": -50
            }
            """;

        try
        {
            File.WriteAllText(settingsPath, json);

            AppSettings settings = AppSettings.LoadFromPath(settingsPath);

            settings.BlinkIntervalMs.Should().Be(AppSettingsDefaults.MIN_BLINK_INTERVAL_MS);
            settings.LedReapplyIntervalMs.Should().Be(AppSettingsDefaults.MAX_LED_REAPPLY_INTERVAL_MS);
            settings.DiskPollIntervalMs.Should().Be(AppSettingsDefaults.MIN_DISK_POLL_INTERVAL_MS);
        }
        finally
        {
            DeleteTempSettingsPath(settingsPath);
        }
    }

    [Fact]
    public void LoadFromPath_InvalidJson_PreservesCorruptFileAndReturnsFallbackDefaults()
    {
        string settingsPath = CreateTempSettingsPath();

        try
        {
            File.WriteAllText(settingsPath, "{ invalid json");

            AppSettings settings = AppSettings.LoadFromPath(settingsPath);

            AssertUsesCentralizedFallbackDefaults(settings);
            File.Exists(settingsPath).Should().BeFalse();
            Directory.GetFiles(Path.GetDirectoryName(settingsPath)!, "settings.corrupt.*.json").Should().ContainSingle();
        }
        finally
        {
            DeleteTempSettingsPath(settingsPath);
        }
    }

    // ── BlinkIntervalMs ──────────────────────────────────────────────────

    [Fact]
    public void Validate_BlinkIntervalMs_BelowMin_ClampsToMin()
    {
        AppSettings settings = new() { BlinkIntervalMs = -1 };
        settings.Validate();
        settings.BlinkIntervalMs.Should().Be(AppSettingsDefaults.MIN_BLINK_INTERVAL_MS);
    }

    [Fact]
    public void Validate_BlinkIntervalMs_Zero_ClampsToMin()
    {
        AppSettings settings = new() { BlinkIntervalMs = 0 };
        settings.Validate();
        settings.BlinkIntervalMs.Should().Be(AppSettingsDefaults.MIN_BLINK_INTERVAL_MS);
    }

    [Fact]
    public void Validate_BlinkIntervalMs_AboveMax_ClampsToMax()
    {
        AppSettings settings = new() { BlinkIntervalMs = 99_999 };
        settings.Validate();
        settings.BlinkIntervalMs.Should().Be(AppSettingsDefaults.MAX_BLINK_INTERVAL_MS);
    }

    [Fact]
    public void Validate_BlinkIntervalMs_ValidValue_Unchanged()
    {
        AppSettings settings = new() { BlinkIntervalMs = 500 };
        settings.Validate();
        settings.BlinkIntervalMs.Should().Be(500);
    }

    // ── LedReapplyIntervalMs ─────────────────────────────────────────────

    [Fact]
    public void Validate_LedReapplyIntervalMs_BelowMin_ClampsToMin()
    {
        AppSettings settings = new() { LedReapplyIntervalMs = 10 };
        settings.Validate();
        settings.LedReapplyIntervalMs.Should().Be(AppSettingsDefaults.MIN_LED_REAPPLY_INTERVAL_MS);
    }

    [Fact]
    public void Validate_LedReapplyIntervalMs_AboveMax_ClampsToMax()
    {
        AppSettings settings = new() { LedReapplyIntervalMs = 50_000 };
        settings.Validate();
        settings.LedReapplyIntervalMs.Should().Be(AppSettingsDefaults.MAX_LED_REAPPLY_INTERVAL_MS);
    }

    [Fact]
    public void Validate_LedReapplyIntervalMs_ValidValue_Unchanged()
    {
        AppSettings settings = new() { LedReapplyIntervalMs = 1000 };
        settings.Validate();
        settings.LedReapplyIntervalMs.Should().Be(1000);
    }

    // ── DiskPollIntervalMs ───────────────────────────────────────────────

    [Fact]
    public void Validate_DiskPollIntervalMs_BelowMin_ClampsToMin()
    {
        AppSettings settings = new() { DiskPollIntervalMs = 50 };
        settings.Validate();
        settings.DiskPollIntervalMs.Should().Be(AppSettingsDefaults.MIN_DISK_POLL_INTERVAL_MS);
    }

    [Fact]
    public void Validate_DiskPollIntervalMs_Negative_ClampsToMin()
    {
        AppSettings settings = new() { DiskPollIntervalMs = int.MinValue };
        settings.Validate();
        settings.DiskPollIntervalMs.Should().Be(AppSettingsDefaults.MIN_DISK_POLL_INTERVAL_MS);
    }

    [Fact]
    public void Validate_DiskPollIntervalMs_AboveMax_ClampsToMax()
    {
        AppSettings settings = new() { DiskPollIntervalMs = 100_000 };
        settings.Validate();
        settings.DiskPollIntervalMs.Should().Be(AppSettingsDefaults.MAX_DISK_POLL_INTERVAL_MS);
    }

    [Fact]
    public void Validate_DiskPollIntervalMs_ValidValue_Unchanged()
    {
        AppSettings settings = new() { DiskPollIntervalMs = 300 };
        settings.Validate();
        settings.DiskPollIntervalMs.Should().Be(300);
    }

    // ── Boundary values ──────────────────────────────────────────────────

    [Fact]
    public void Validate_AllIntervalsAtExactMin_AreUnchanged()
    {
        AppSettings settings = new()
        {
            BlinkIntervalMs = AppSettingsDefaults.MIN_BLINK_INTERVAL_MS,
            LedReapplyIntervalMs = AppSettingsDefaults.MIN_LED_REAPPLY_INTERVAL_MS,
            DiskPollIntervalMs = AppSettingsDefaults.MIN_DISK_POLL_INTERVAL_MS,
        };
        settings.Validate();
        settings.BlinkIntervalMs.Should().Be(AppSettingsDefaults.MIN_BLINK_INTERVAL_MS);
        settings.LedReapplyIntervalMs.Should().Be(AppSettingsDefaults.MIN_LED_REAPPLY_INTERVAL_MS);
        settings.DiskPollIntervalMs.Should().Be(AppSettingsDefaults.MIN_DISK_POLL_INTERVAL_MS);
    }

    [Fact]
    public void Validate_AllIntervalsAtExactMax_AreUnchanged()
    {
        AppSettings settings = new()
        {
            BlinkIntervalMs = AppSettingsDefaults.MAX_BLINK_INTERVAL_MS,
            LedReapplyIntervalMs = AppSettingsDefaults.MAX_LED_REAPPLY_INTERVAL_MS,
            DiskPollIntervalMs = AppSettingsDefaults.MAX_DISK_POLL_INTERVAL_MS,
        };
        settings.Validate();
        settings.BlinkIntervalMs.Should().Be(AppSettingsDefaults.MAX_BLINK_INTERVAL_MS);
        settings.LedReapplyIntervalMs.Should().Be(AppSettingsDefaults.MAX_LED_REAPPLY_INTERVAL_MS);
        settings.DiskPollIntervalMs.Should().Be(AppSettingsDefaults.MAX_DISK_POLL_INTERVAL_MS);
    }

    // ── Default settings pass validation ─────────────────────────────────

    [Fact]
    public void Validate_DefaultSettings_AllValuesRemainUnchanged()
    {
        AppSettings settings = new();
        int originalBlink = settings.BlinkIntervalMs;
        int originalReapply = settings.LedReapplyIntervalMs;
        int originalDisk = settings.DiskPollIntervalMs;

        settings.Validate();

        settings.BlinkIntervalMs.Should().Be(originalBlink);
        settings.LedReapplyIntervalMs.Should().Be(originalReapply);
        settings.DiskPollIntervalMs.Should().Be(originalDisk);
    }

    private static void AssertUsesCentralizedFallbackDefaults(AppSettings settings)
    {
        settings.PowerMode.Should().Be(AppSettingsDefaults.LED_MODE);
        settings.MuteMode.Should().Be(AppSettingsDefaults.LED_MODE);
        settings.RedDotMode.Should().Be(AppSettingsDefaults.LED_MODE);
        settings.MicrophoneMode.Should().Be(AppSettingsDefaults.LED_MODE);
        settings.SleepMode.Should().Be(AppSettingsDefaults.LED_MODE);
        settings.FnLockMode.Should().Be(AppSettingsDefaults.LED_MODE);
        settings.CameraMode.Should().Be(AppSettingsDefaults.LED_MODE);
        settings.HotkeyCycleOptions.Should().Be(AppSettingsDefaults.HOTKEY_CYCLE_OPTIONS);
        settings.Hotkey.Should().NotBeNull();
        settings.Hotkey.Modifiers.Should().Be(AppSettingsDefaults.HOTKEY_MODIFIERS);
        settings.Hotkey.VirtualKey.Should().Be(AppSettingsDefaults.HOTKEY_VIRTUAL_KEY);
        settings.BlinkIntervalMs.Should().Be(AppSettingsDefaults.BLINK_INTERVAL_MS);
        settings.LedReapplyIntervalMs.Should().Be(AppSettingsDefaults.LED_REAPPLY_INTERVAL_MS);
        settings.DiskPollIntervalMs.Should().Be(AppSettingsDefaults.DISK_POLL_INTERVAL_MS);
        settings.RememberKeyboardBacklight.Should().Be(AppSettingsDefaults.REMEMBER_KEYBOARD_BACKLIGHT);
        settings.DimLedsWhenFullscreen.Should().Be(AppSettingsDefaults.DIM_LEDS_WHEN_FULLSCREEN);
        settings.SuppressDiskCounterWarning.Should().Be(AppSettingsDefaults.SUPPRESS_DISK_COUNTER_WARNING);
        settings.PersistSettingsOnChange.Should().Be(AppSettingsDefaults.PERSIST_SETTINGS_ON_CHANGE);
        settings.EnableHardwareAccess.Should().Be(AppSettingsDefaults.ENABLE_HARDWARE_ACCESS);
    }

    private static string CreateTempSettingsPath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ModernThinkPadLEDsController.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "settings.json");
    }

    private static void DeleteTempSettingsPath(string settingsPath)
    {
        string directory = Path.GetDirectoryName(settingsPath)!;

        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
