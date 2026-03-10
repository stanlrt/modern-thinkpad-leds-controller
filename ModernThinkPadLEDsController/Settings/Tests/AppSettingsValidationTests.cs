using FluentAssertions;
using Xunit;

namespace ModernThinkPadLEDsController.Settings.Tests;

/// <summary>
/// Unit tests for <see cref="AppSettings.Validate"/>.
/// Verifies that interval values are clamped to their safe operating ranges.
/// </summary>
public sealed class AppSettingsValidationTests
{
    // ── BlinkIntervalMs ──────────────────────────────────────────────────

    [Fact]
    public void Validate_BlinkIntervalMs_BelowMin_ClampsToMin()
    {
        AppSettings settings = new() { BlinkIntervalMs = -1 };
        settings.Validate();
        settings.BlinkIntervalMs.Should().Be(AppSettings.MinBlinkIntervalMs);
    }

    [Fact]
    public void Validate_BlinkIntervalMs_Zero_ClampsToMin()
    {
        AppSettings settings = new() { BlinkIntervalMs = 0 };
        settings.Validate();
        settings.BlinkIntervalMs.Should().Be(AppSettings.MinBlinkIntervalMs);
    }

    [Fact]
    public void Validate_BlinkIntervalMs_AboveMax_ClampsToMax()
    {
        AppSettings settings = new() { BlinkIntervalMs = 99_999 };
        settings.Validate();
        settings.BlinkIntervalMs.Should().Be(AppSettings.MaxBlinkIntervalMs);
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
        settings.LedReapplyIntervalMs.Should().Be(AppSettings.MinLedReapplyIntervalMs);
    }

    [Fact]
    public void Validate_LedReapplyIntervalMs_AboveMax_ClampsToMax()
    {
        AppSettings settings = new() { LedReapplyIntervalMs = 50_000 };
        settings.Validate();
        settings.LedReapplyIntervalMs.Should().Be(AppSettings.MaxLedReapplyIntervalMs);
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
        settings.DiskPollIntervalMs.Should().Be(AppSettings.MinDiskPollIntervalMs);
    }

    [Fact]
    public void Validate_DiskPollIntervalMs_Negative_ClampsToMin()
    {
        AppSettings settings = new() { DiskPollIntervalMs = int.MinValue };
        settings.Validate();
        settings.DiskPollIntervalMs.Should().Be(AppSettings.MinDiskPollIntervalMs);
    }

    [Fact]
    public void Validate_DiskPollIntervalMs_AboveMax_ClampsToMax()
    {
        AppSettings settings = new() { DiskPollIntervalMs = 100_000 };
        settings.Validate();
        settings.DiskPollIntervalMs.Should().Be(AppSettings.MaxDiskPollIntervalMs);
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
            BlinkIntervalMs = AppSettings.MinBlinkIntervalMs,
            LedReapplyIntervalMs = AppSettings.MinLedReapplyIntervalMs,
            DiskPollIntervalMs = AppSettings.MinDiskPollIntervalMs,
        };
        settings.Validate();
        settings.BlinkIntervalMs.Should().Be(AppSettings.MinBlinkIntervalMs);
        settings.LedReapplyIntervalMs.Should().Be(AppSettings.MinLedReapplyIntervalMs);
        settings.DiskPollIntervalMs.Should().Be(AppSettings.MinDiskPollIntervalMs);
    }

    [Fact]
    public void Validate_AllIntervalsAtExactMax_AreUnchanged()
    {
        AppSettings settings = new()
        {
            BlinkIntervalMs = AppSettings.MaxBlinkIntervalMs,
            LedReapplyIntervalMs = AppSettings.MaxLedReapplyIntervalMs,
            DiskPollIntervalMs = AppSettings.MaxDiskPollIntervalMs,
        };
        settings.Validate();
        settings.BlinkIntervalMs.Should().Be(AppSettings.MaxBlinkIntervalMs);
        settings.LedReapplyIntervalMs.Should().Be(AppSettings.MaxLedReapplyIntervalMs);
        settings.DiskPollIntervalMs.Should().Be(AppSettings.MaxDiskPollIntervalMs);
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
}
