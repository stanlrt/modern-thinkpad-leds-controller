using FluentAssertions;
using ModernThinkPadLEDsController.Hardware.PawnIo;
using Xunit;

namespace ModernThinkPadLEDsController.Hardware.PawnIo.Tests;

/// <summary>
/// Tests for <see cref="LpcAcpiEc"/> to catch regressions when updating from upstream LHM.
/// These tests document expected behavior and verify disposal logic.
/// </summary>
public sealed class LpcAcpiEcTests
{
    [Fact]
    public void Constructor_WithoutPawnIoDriver_ReturnsNonNullInstance()
    {
        // Act - This will fail to load if PawnIO driver isn't running, but shouldn't throw
        LpcAcpiEc ec = new LpcAcpiEc();

        // Assert
        ec.Should().NotBeNull();
    }

    [Fact]
    public void IsLoaded_PropertyAccess_DoesNotThrow()
    {
        // Arrange - On systems without PawnIO, the module won't load
        LpcAcpiEc ec = new LpcAcpiEc();

        // Act & Assert - Main goal: ensure property doesn't throw
        ec.Invoking(e => _ = e.IsLoaded).Should().NotThrow();
    }

    [Fact]
    public void Dispose_WhenCalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        LpcAcpiEc ec = new LpcAcpiEc();

        // Act & Assert
        ec.Dispose();
        ec.Invoking(e => e.Dispose()).Should().NotThrow();
    }

    [Fact]
    public void ReadPort_WhenDisposed_DoesNotThrow()
    {
        // Arrange
        LpcAcpiEc ec = new LpcAcpiEc();
        ec.Dispose();

        // Act & Assert - If driver isn't loaded, this returns 0 gracefully
        ec.Invoking(e => e.ReadPort(0x66)).Should().NotThrow();
    }

    [Fact]
    public void WritePort_WhenDisposed_DoesNotThrow()
    {
        // Arrange
        LpcAcpiEc ec = new LpcAcpiEc();
        ec.Dispose();

        // Act & Assert - If driver isn't loaded, this no-ops gracefully
        ec.Invoking(e => e.WritePort(0x66, 0x00)).Should().NotThrow();
    }
}
