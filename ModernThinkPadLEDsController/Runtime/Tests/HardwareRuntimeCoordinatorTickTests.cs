using FluentAssertions;
using ModernThinkPadLEDsController.Hardware;
using ModernThinkPadLEDsController.Lighting;
using ModernThinkPadLEDsController.TestInfrastructure;
using Xunit;

namespace ModernThinkPadLEDsController.Runtime.Tests;

/// <summary>
/// Unit tests for <see cref="HardwareRuntimeCoordinator.ExecuteReapplyTick"/>.
/// Tests exercise the extracted tick method directly, avoiding any real Task.Delay timing.
/// </summary>
public sealed class HardwareRuntimeCoordinatorTickTests
{
    private static (LedController Leds, FakePortIO Io, HardwareAccessController Access) BuildLedController(bool hardwareEnabled = true)
    {
        FakePortIO io = new();
        EcController ec = new(io);
        HardwareAccessController access = new(hardwareEnabled, driverLoaded: false, startupReason: null);
        return (new LedController(ec, access), io, access);
    }

    private static LedBehaviorService BuildBehaviorService(
        LedController leds,
        IReadOnlyDictionary<Led, LedMapping> mappings,
        AppSettings? settings = null)
    {
        FakeLedBlinkController blink = new();
        LedBehaviorService svc = new(leds, settings ?? TestSettingsBuilder.Default(), blink);
        svc.Initialize(mappings);
        return svc;
    }

    // --- ExecuteReapplyTick: hardware enabled, needs reapply ---------------

    [Fact]
    public void ExecuteReapplyTick_HardwareEnabled_NeedsReapply_CallsReapplyManagedStates()
    {
        (LedController leds, FakePortIO io, HardwareAccessController access) = BuildLedController(hardwareEnabled: true);
        LedBehaviorService behavior = BuildBehaviorService(
            leds,
            TestSettingsBuilder.SingleMapping(Led.Power, LedMode.On));

        HardwareRuntimeCoordinator coordinator = new(access, behavior);

        // Prime the cache so the first write fills it
        behavior.ApplyAll();
        io.Clear();

        coordinator.ExecuteReapplyTick();

        io.DataPortWriteCount.Should().BePositive("ReapplyManagedStates uses forceWrite to bypass cache");
    }

    [Fact]
    public void ExecuteReapplyTick_HardwareDisabled_DoesNotWrite()
    {
        (LedController leds, FakePortIO io, HardwareAccessController access) = BuildLedController(hardwareEnabled: false);
        LedBehaviorService behavior = BuildBehaviorService(
            leds,
            TestSettingsBuilder.SingleMapping(Led.Power, LedMode.On));

        HardwareRuntimeCoordinator coordinator = new(access, behavior);

        coordinator.ExecuteReapplyTick();

        io.DataPortWriteCount.Should().Be(0, "hardware is disabled — no EC writes should occur");
    }

    [Fact]
    public void ExecuteReapplyTick_NoManagedLeds_DoesNotWrite()
    {
        (LedController leds, FakePortIO io, HardwareAccessController access) = BuildLedController(hardwareEnabled: true);
        LedBehaviorService behavior = BuildBehaviorService(
            leds,
            TestSettingsBuilder.SingleMapping(Led.Power, LedMode.DiskRead));

        HardwareRuntimeCoordinator coordinator = new(access, behavior);

        coordinator.ExecuteReapplyTick();

        io.DataPortWriteCount.Should().Be(0, "DiskRead mode does not need periodic backstop");
    }

    // --- ExecuteReapplyTick: idempotent on repeated calls -----------------

    [Fact]
    public void ExecuteReapplyTick_CalledTwice_WritesEachTime()
    {
        (LedController leds, FakePortIO io, HardwareAccessController access) = BuildLedController(hardwareEnabled: true);
        LedBehaviorService behavior = BuildBehaviorService(
            leds,
            TestSettingsBuilder.SingleMapping(Led.Power, LedMode.On));

        HardwareRuntimeCoordinator coordinator = new(access, behavior);

        behavior.ApplyAll();
        io.Clear();

        coordinator.ExecuteReapplyTick();
        int firstCount = io.DataPortWriteCount;
        coordinator.ExecuteReapplyTick();
        int secondCount = io.DataPortWriteCount;

        (secondCount - firstCount).Should().Be(firstCount,
            "each tick force-writes the same number of bytes as the first tick");
    }
}
