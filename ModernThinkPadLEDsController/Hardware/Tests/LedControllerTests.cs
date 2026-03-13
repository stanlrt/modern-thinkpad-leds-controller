using FluentAssertions;
using ModernThinkPadLEDsController.Lighting;
using ModernThinkPadLEDsController.TestInfrastructure;
using Xunit;

namespace ModernThinkPadLEDsController.Hardware.Tests;

/// <summary>
/// Unit tests for <see cref="LedController"/> cache semantics, force-write rules,
/// hardware-disabled suppression, and correct byte composition.
/// </summary>
public sealed class LedControllerTests
{
    // TP_LED_OFFSET = 0x0C, TP_KBD_OFFSET = 0x0D (constants from LedController).
    private const byte LED_OFFSET = 0x0C;
    private const byte KBD_OFFSET = 0x0D;

    private static (LedController Controller, FakePortIO Io) Build(bool hardwareEnabled = true)
    {
        FakePortIO io = new();
        EcController ec = new(io);
        HardwareAccessController access = new(hardwareEnabled, driverLoaded: false, startupReason: null);
        return (new LedController(ec, access), io);
    }

    // --- Byte composition --------------------------------------------------

    [Fact]
    public void SetLed_BuiltInId_WritesLedIdOrStateToOffset()
    {
        (LedController controller, FakePortIO io) = Build();

        controller.SetLed(Led.Power, LedState.On);

        // value = (byte)Led.Power | (byte)LedState.On = 0x00 | 0x80 = 0x80
        io.DataPortWrites.Should().Equal(new byte[] { LED_OFFSET, 0x80 });
    }

    [Theory]
    [InlineData(Led.Power, LedState.On, 0x80)]    // 0x00 | 0x80
    [InlineData(Led.Power, LedState.Off, 0x00)]   // 0x00 | 0x00
    [InlineData(Led.Mute, LedState.On, 0x84)]     // 0x04 | 0x80
    [InlineData(Led.Mute, LedState.Blink, 0xC4)]  // 0x04 | 0xC0
    public void SetLed_ByteComposition_MatchesExpected(Led led, LedState state, byte expectedValue)
    {
        (LedController controller, FakePortIO io) = Build();

        controller.SetLed(led, state);

        io.DataPortWrites.Should().Equal(new byte[] { LED_OFFSET, expectedValue });
    }

    [Fact]
    public void SetLed_CustomRegisterId_UsesCustomIdInsteadOfLedByte()
    {
        (LedController controller, FakePortIO io) = Build();
        const byte customId = 0x10;

        controller.SetLed(Led.Power, LedState.On, customId: customId);

        // value = customId | (byte)LedState.On = 0x10 | 0x80 = 0x90
        io.DataPortWrites.Should().Equal(new byte[] { LED_OFFSET, 0x90 });
    }

    // --- Cache suppression -------------------------------------------------

    [Fact]
    public void SetLed_SameStateTwice_SecondCallDoesNotWrite()
    {
        (LedController controller, FakePortIO io) = Build();

        controller.SetLed(Led.Power, LedState.On);
        io.Clear();
        bool result = controller.SetLed(Led.Power, LedState.On);

        result.Should().BeTrue();
        io.DataPortWriteCount.Should().Be(0, "cache hit should suppress the write");
    }

    [Fact]
    public void SetLed_DifferentState_WritesAgain()
    {
        (LedController controller, FakePortIO io) = Build();

        controller.SetLed(Led.Power, LedState.On);
        io.Clear();
        controller.SetLed(Led.Power, LedState.Off);

        io.DataPortWriteCount.Should().Be(2, "offset + value written for the new state");
    }

    [Fact]
    public void SetLed_TwoDistinctLeds_EachWrittenIndependently()
    {
        (LedController controller, FakePortIO io) = Build();

        controller.SetLed(Led.Power, LedState.On);
        controller.SetLed(Led.Mute, LedState.On);
        io.Clear();

        // Same state on each LED — both should be cached
        controller.SetLed(Led.Power, LedState.On);
        controller.SetLed(Led.Mute, LedState.On);

        io.DataPortWriteCount.Should().Be(0, "both LEDs are cached");
    }

    // --- Force write -------------------------------------------------------

    [Fact]
    public void SetLed_ForceWrite_BypassesCacheAndReturnsTrue()
    {
        (LedController controller, FakePortIO io) = Build();

        controller.SetLed(Led.Power, LedState.On);
        io.Clear();
        bool result = controller.SetLed(Led.Power, LedState.On, forceWrite: true);

        result.Should().BeTrue();
        io.DataPortWriteCount.Should().Be(2, "offset + value must be written even when cached");
    }

    // --- Hardware disabled -------------------------------------------------

    [Fact]
    public void SetLed_HardwareDisabled_ReturnsFalseAndDoesNotWrite()
    {
        (LedController controller, FakePortIO io) = Build(hardwareEnabled: false);

        bool result = controller.SetLed(Led.Power, LedState.On);

        result.Should().BeFalse();
        io.DataPortWriteCount.Should().Be(0);
    }

    // --- Keyboard backlight ------------------------------------------------

    [Fact]
    public void SetKeyboardBacklightRaw_FirstWrite_WritesLevelToKbdOffset()
    {
        (LedController controller, FakePortIO io) = Build();

        bool result = controller.SetKeyboardBacklightRaw(0x50);

        result.Should().BeTrue();
        io.DataPortWrites.Should().Equal(new byte[] { KBD_OFFSET, 0x50 });
    }

    [Fact]
    public void SetKeyboardBacklightRaw_SameLevel_CacheSuppressesWrite()
    {
        (LedController controller, FakePortIO io) = Build();

        controller.SetKeyboardBacklightRaw(0x50);
        io.Clear();
        bool result = controller.SetKeyboardBacklightRaw(0x50);

        result.Should().BeTrue();
        io.DataPortWriteCount.Should().Be(0);
    }

    [Fact]
    public void SetKeyboardBacklightRaw_DifferentLevel_WritesNewLevel()
    {
        (LedController controller, FakePortIO io) = Build();

        controller.SetKeyboardBacklightRaw(0x50);
        io.Clear();
        controller.SetKeyboardBacklightRaw(0x80);

        io.DataPortWrites.Should().Equal(new byte[] { KBD_OFFSET, 0x80 });
    }

    [Fact]
    public void SetKeyboardBacklightRaw_HardwareDisabled_ReturnsFalse()
    {
        (LedController controller, FakePortIO io) = Build(hardwareEnabled: false);

        bool result = controller.SetKeyboardBacklightRaw(0x50);

        result.Should().BeFalse();
        io.DataPortWriteCount.Should().Be(0);
    }
}
