using FluentAssertions;
using ModernThinkPadLEDsController.Hardware;
using ModernThinkPadLEDsController.Lighting;
using ModernThinkPadLEDsController.Monitoring;
using ModernThinkPadLEDsController.TestInfrastructure;
using Xunit;

namespace ModernThinkPadLEDsController.Lighting.Tests;

/// <summary>
/// Unit tests for <see cref="LedBehaviorService"/> state-machine logic:
/// ApplyAll, NeedsPeriodicReapply, ReapplyManagedStates, mute observation,
/// hotkey cycling, fullscreen entry/exit, and custom register preservation.
/// </summary>
public sealed class LedBehaviorServiceTests
{
    // Helper: build an enabled LedController backed by FakePortIO.
    private static (LedController Controller, FakePortIO Io) BuildLedController()
    {
        FakePortIO io = new();
        EcController ec = new(io);
        HardwareAccessController access = new(isEnabled: true, driverLoaded: false, startupReason: null);
        return (new LedController(ec, access), io);
    }

    // Helper: build a LedBehaviorService with the injected fake blink controller.
    private static (LedBehaviorService Service, FakeLedBlinkController Blink, LedController Leds, FakePortIO Io)
        Build(AppSettings? settings = null)
    {
        (LedController leds, FakePortIO io) = BuildLedController();
        FakeLedBlinkController blink = new();
        LedBehaviorService svc = new(leds, settings ?? TestSettingsBuilder.Default(), blink);
        return (svc, blink, leds, io);
    }

    // --- ApplyAll ----------------------------------------------------------

    [Fact]
    public void ApplyAll_OnMode_WritesToHardwareForEachLed()
    {
        (LedBehaviorService svc, _, _, FakePortIO io) = Build();
        svc.Initialize(TestSettingsBuilder.AllMappings(LedMode.On));

        svc.ApplyAll();

        io.DataPortWriteCount.Should().BePositive("every On-mode LED should generate an EC write");
    }

    [Fact]
    public void ApplyAll_BlinkMode_RegistersWithBlinkController()
    {
        (LedBehaviorService svc, FakeLedBlinkController blink, _, _) = Build();
        svc.Initialize(TestSettingsBuilder.SingleMapping(Led.Power, LedMode.Blink));

        svc.ApplyAll();

        blink.BlinkingLeds.Should().ContainKey(Led.Power);
    }

    // --- NeedsPeriodicReapply ---------------------------------------------

    [Theory]
    [InlineData(LedMode.On)]
    [InlineData(LedMode.Off)]
    [InlineData(LedMode.HotkeyControlled)]
    public void NeedsPeriodicReapply_AppOwnerModes_ReturnsTrue(LedMode mode)
    {
        (LedBehaviorService svc, _, _, _) = Build();
        svc.Initialize(TestSettingsBuilder.SingleMapping(Led.Power, mode));

        svc.NeedsPeriodicReapply().Should().BeTrue($"{mode} LEDs must be backstopped periodically");
    }

    [Theory]
    [InlineData(LedMode.DiskRead)]
    [InlineData(LedMode.DiskWrite)]
    [InlineData(LedMode.Default)]
    [InlineData(LedMode.Blink)]
    public void NeedsPeriodicReapply_NonBackstopModes_ReturnsFalse(LedMode mode)
    {
        (LedBehaviorService svc, _, _, _) = Build();
        svc.Initialize(TestSettingsBuilder.SingleMapping(Led.Power, mode));

        svc.NeedsPeriodicReapply().Should().BeFalse($"{mode} does not require periodic backstop");
    }

    // --- ReapplyManagedStates ---------------------------------------------

    [Fact]
    public void ReapplyManagedStates_OnMode_ForceWritesLedState()
    {
        (LedBehaviorService svc, _, _, FakePortIO io) = Build();
        svc.Initialize(TestSettingsBuilder.SingleMapping(Led.Power, LedMode.On));

        // Seed cache by first applying normally
        svc.ApplyAll();
        io.Clear();

        // A second normal write would be suppressed by cache; force write bypasses it
        svc.ReapplyManagedStates();

        io.DataPortWriteCount.Should().BePositive("ReapplyManagedStates uses forceWrite");
    }

    [Fact]
    public void ReapplyManagedStates_DiskMode_DoesNotWrite()
    {
        (LedBehaviorService svc, _, _, FakePortIO io) = Build();
        svc.Initialize(TestSettingsBuilder.SingleMapping(Led.Power, LedMode.DiskRead));

        svc.ReapplyManagedStates();

        io.DataPortWriteCount.Should().Be(0, "disk-activity mode is not periodically backstopped");
    }

    [Fact]
    public void ReapplyManagedStates_HotkeyBlinkState_DoesNotForceWrite()
    {
        // When HotkeyControlled LED is in Blink state, blink controller owns it —
        // no force write should happen.
        (LedBehaviorService svc, FakeLedBlinkController blink, _, FakePortIO io) = Build();
        AppSettings settings = TestSettingsBuilder.Default();
        settings.HotkeyCycleOptions = HotkeyCycleOptions.Blink;

        svc.Initialize(TestSettingsBuilder.SingleMapping(Led.Power, LedMode.HotkeyControlled));
        svc.UpdateHotkeyCycleOptions(HotkeyCycleOptions.Blink);
        svc.OnHotkeyPressed(); // cycle to Blink state
        io.Clear();

        svc.ReapplyManagedStates();

        io.DataPortWriteCount.Should().Be(0, "blink-state hotkey LED is owned by the blink controller");
        blink.BlinkingLeds.Should().ContainKey(Led.Power);
    }

    // --- Mute observation -------------------------------------------------

    [Fact]
    public void ObserveMicrophoneMuteState_DefaultMode_UpdatesLedToMatchMuteState()
    {
        (LedBehaviorService svc, _, _, FakePortIO io) = Build();
        svc.Initialize(TestSettingsBuilder.SingleMapping(Led.Microphone, LedMode.Default));

        svc.ObserveMicrophoneMuteState(isMuted: true);

        // Muted → LED On; the write should include the Microphone LED ID (0x0E) | On (0x80) = 0x8E
        byte expectedValue = (byte)((byte)Led.Microphone | (byte)LedState.On);
        io.DataPortWrites.Should().Equal(new byte[] { 0x0C, expectedValue });
    }

    [Fact]
    public void ObserveMicrophoneMuteState_DefaultMode_UpdatesLedToUnmuted()
    {
        (LedBehaviorService svc, _, _, FakePortIO io) = Build();
        svc.Initialize(TestSettingsBuilder.SingleMapping(Led.Microphone, LedMode.Default));

        svc.ObserveMicrophoneMuteState(isMuted: false);

        byte expectedValue = (byte)((byte)Led.Microphone | (byte)LedState.Off);
        io.DataPortWrites.Should().Equal(new byte[] { 0x0C, expectedValue });
    }

    // --- Hotkey cycling ---------------------------------------------------

    [Fact]
    public void OnHotkeyPressed_CyclesHotkeyControlledLedsThrough_OnThenOff()
    {
        (LedBehaviorService svc, _, _, FakePortIO io) = Build();
        svc.Initialize(TestSettingsBuilder.SingleMapping(Led.Power, LedMode.HotkeyControlled));
        svc.UpdateHotkeyCycleOptions(HotkeyCycleOptions.On | HotkeyCycleOptions.Off);

        svc.OnHotkeyPressed(); // → On
        io.Clear();
        svc.OnHotkeyPressed(); // → Off

        // Off state = 0x00 | 0x00 = 0x00; offset = 0x0C
        io.DataPortWrites.Should().Equal(new byte[] { 0x0C, 0x00 });
    }

    [Fact]
    public void OnHotkeyPressed_BlinkInCycle_RegistersWithBlinkController()
    {
        (LedBehaviorService svc, FakeLedBlinkController blink, _, _) = Build();
        svc.Initialize(TestSettingsBuilder.SingleMapping(Led.Power, LedMode.HotkeyControlled));
        svc.UpdateHotkeyCycleOptions(HotkeyCycleOptions.Blink);

        svc.OnHotkeyPressed(); // → Blink

        blink.BlinkingLeds.Should().ContainKey(Led.Power);
    }

    // --- Fullscreen entry and exit ----------------------------------------

    [Fact]
    public void OnFullscreenChanged_Enter_TurnsOffManagedLedsAndPausesBlink()
    {
        AppSettings settings = TestSettingsBuilder.Default();
        settings.DimLedsWhenFullscreen = true;
        (LedBehaviorService svc, FakeLedBlinkController blink, _, FakePortIO io) = Build(settings);
        svc.Initialize(TestSettingsBuilder.SingleMapping(Led.Power, LedMode.On));

        svc.OnFullscreenChanged(isFullscreen: true, currentBacklight: 100);

        blink.IsPaused.Should().BeTrue();
        // LED should have been forced Off
        io.DataPortWriteCount.Should().BePositive("fullscreen entry forces Power LED off");
    }

    [Fact]
    public void OnFullscreenChanged_Exit_ResumesBlink()
    {
        AppSettings settings = TestSettingsBuilder.Default();
        settings.DimLedsWhenFullscreen = true;
        (LedBehaviorService svc, FakeLedBlinkController blink, _, _) = Build(settings);
        svc.Initialize(TestSettingsBuilder.AllMappings(LedMode.On));

        svc.OnFullscreenChanged(isFullscreen: true, currentBacklight: 100);
        svc.OnFullscreenChanged(isFullscreen: false, currentBacklight: 100);

        blink.IsPaused.Should().BeFalse("blink controller should be resumed on fullscreen exit");
    }

    [Fact]
    public void OnFullscreenChanged_DimDisabled_DoesNothing()
    {
        AppSettings settings = TestSettingsBuilder.Default();
        settings.DimLedsWhenFullscreen = false;
        (LedBehaviorService svc, FakeLedBlinkController blink, _, FakePortIO io) = Build(settings);
        svc.Initialize(TestSettingsBuilder.SingleMapping(Led.Power, LedMode.On));
        svc.ApplyAll();
        io.Clear();

        svc.OnFullscreenChanged(isFullscreen: true, currentBacklight: 100);

        blink.IsPaused.Should().BeFalse("dim is disabled — blink should remain unpaused");
        io.DataPortWriteCount.Should().Be(0, "dim is disabled — no LED writes expected");
    }

    // --- Custom register preservation -------------------------------------

    [Fact]
    public void ApplyAll_CustomRegisterId_UsesCustomIdInWrite()
    {
        (LedBehaviorService svc, _, _, FakePortIO io) = Build();
        svc.Initialize(TestSettingsBuilder.SingleMapping(Led.Power, LedMode.On, customId: 0x10));

        svc.ApplyAll();

        // value = 0x10 | 0x80 = 0x90; offset = 0x0C
        io.DataPortWrites.Should().Equal(new byte[] { 0x0C, 0x90 });
    }

    // --- Disk state -------------------------------------------------------

    [Fact]
    public void OnDiskStateChanged_ReadActivity_TurnsOnDiskReadLed()
    {
        (LedBehaviorService svc, _, _, FakePortIO io) = Build();
        svc.Initialize(TestSettingsBuilder.SingleMapping(Led.Power, LedMode.DiskRead));

        svc.OnDiskStateChanged(DiskActivityState.Read);

        byte expectedValue = (byte)((byte)Led.Power | (byte)LedState.On);
        io.DataPortWrites.Should().Equal(new byte[] { 0x0C, expectedValue });
    }

    [Fact]
    public void OnDiskStateChanged_Idle_TurnsOffDiskReadLed()
    {
        (LedBehaviorService svc, _, _, FakePortIO io) = Build();
        svc.Initialize(TestSettingsBuilder.SingleMapping(Led.Power, LedMode.DiskRead));

        svc.OnDiskStateChanged(DiskActivityState.Read);
        io.Clear();
        svc.OnDiskStateChanged(DiskActivityState.Idle);

        byte expectedValue = (byte)((byte)Led.Power | (byte)LedState.Off);
        io.DataPortWrites.Should().Equal(new byte[] { 0x0C, expectedValue });
    }
}
