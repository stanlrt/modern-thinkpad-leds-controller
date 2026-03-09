using FluentAssertions;
using ModernThinkPadLEDsController.Hardware;
using ModernThinkPadLEDsController.Lighting;
using ModernThinkPadLEDsController.TestInfrastructure;
using Xunit;

namespace ModernThinkPadLEDsController.Lighting.Tests;

/// <summary>
/// Component tests that compose the real <see cref="LedBehaviorService"/> and
/// <see cref="LedController"/> with fake hardware and a fake blink controller.
/// These tests cover multi-step scenarios and startup ordering behavior.
/// </summary>
public sealed class LedBehaviorComponentTests
{
    private static (LedBehaviorService Service, FakeLedBlinkController Blink, FakePortIO Io)
        Build(AppSettings? settings = null)
    {
        FakePortIO io = new();
        EcController ec = new(io);
        HardwareAccessController access = new(isEnabled: true, driverLoaded: false, startupReason: null);
        LedController leds = new(ec, access);
        FakeLedBlinkController blink = new();
        LedBehaviorService svc = new(leds, settings ?? TestSettingsBuilder.Default(), blink);
        return (svc, blink, io);
    }

    // --- External override reassertion ------------------------------------

    [Fact]
    public void ReapplyManagedStates_AfterExternalOverride_ReassertsManagedState()
    {
        // Simulates EC being reset by firmware: cache has the LED on, but after
        // ReapplyManagedStates the correct byte must be force-written again.
        (LedBehaviorService svc, _, FakePortIO io) = Build();
        svc.Initialize(TestSettingsBuilder.SingleMapping(Led.Power, LedMode.On));

        svc.ApplyAll(); // first write, fills cache
        io.Clear();

        svc.ReapplyManagedStates(); // second write (forceWrite) even though cached value matches

        io.DataPortWriteCount.Should().BePositive("ReapplyManagedStates must bypass the cache");
    }

    // --- Fullscreen dimming persistence -----------------------------------

    [Fact]
    public void FullscreenDimming_ExitRestoresPreFullscreenBacklightLevel()
    {
        AppSettings settings = TestSettingsBuilder.Default();
        settings.DimLedsWhenFullscreen = true;
        (LedBehaviorService svc, _, FakePortIO io) = Build(settings);
        svc.Initialize(TestSettingsBuilder.AllMappings(LedMode.On));

        const byte savedLevel = 80;
        svc.OnFullscreenChanged(isFullscreen: true, currentBacklight: savedLevel);
        io.Clear();

        svc.OnFullscreenChanged(isFullscreen: false, currentBacklight: 0);

        // Keyboard backlight restore write: offset=0x0D, level=80
        io.DataPortWrites.Should().Contain(0x0D, "keyboard backlight register");
        io.DataPortWrites.Should().Contain(savedLevel, "saved backlight level must be restored");
    }

    // --- Mixed-mode reapply rules -----------------------------------------

    [Fact]
    public void ReapplyManagedStates_MixedModes_OnlyReappliesEligibleModes()
    {
        (LedBehaviorService svc, _, FakePortIO io) = Build();
        var mappings = new Dictionary<Led, LedMapping>
        {
            [Led.Power] = new LedMapping { Mode = LedMode.On },
            [Led.Mute] = new LedMapping { Mode = LedMode.DiskRead },
        };
        svc.Initialize(mappings);

        svc.ApplyAll();
        io.Clear();

        svc.ReapplyManagedStates();

        // Only Power (On mode) should be force-written; Mute (DiskRead) must not
        // DataPortWrites come in pairs (offset, value); with one EC write → 2 entries
        io.DataPortWriteCount.Should().Be(2,
            "only the On-mode LED generates one EC write (offset + value)");
    }

    // --- Startup ordering -------------------------------------------------

    [Fact]
    public void Initialize_MustBeCalled_BeforeApplyAll()
    {
        (LedBehaviorService svc, _, _) = Build();
        svc.Initialize(TestSettingsBuilder.AllMappings(LedMode.On));

        // Calling ApplyAll after Initialize should succeed without throwing
        Action act = () => svc.ApplyAll();
        act.Should().NotThrow();
    }

    [Fact]
    public void ApplyAll_BeforeInitialize_ThrowsInvalidOperationException()
    {
        (LedBehaviorService svc, _, _) = Build();

        Action act = () => svc.ApplyAll();

        act.Should().Throw<InvalidOperationException>("Initialize must be called before use");
    }
}
