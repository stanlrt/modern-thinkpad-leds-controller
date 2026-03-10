using FluentAssertions;
using ModernThinkPadLEDsController.Hardware;
using Xunit;

namespace ModernThinkPadLEDsController.Hardware.Tests;

/// <summary>
/// Integration tests for <see cref="LhmDriver"/> to catch regressions when updating PawnIO code.
/// These verify the driver loading and disposal logic without requiring actual hardware.
/// </summary>
public sealed class LhmDriverIntegrationTests
{
    [Fact]
    public void TryOpen_ReturnsBoolean_WithoutThrowing()
    {
        // Act & Assert - Should not throw regardless of PawnIO availability
        Action act = () => LhmDriver.TryOpen(out LhmDriver? driver);
        act.Should().NotThrow();
    }

    [Fact]
    public void TryOpen_WhenFails_ReturnsNullDriver()
    {
        // This test may pass or fail depending on PawnIO availability
        // Main goal: verify contract that failed open returns null

        // Act
        bool opened = LhmDriver.TryOpen(out LhmDriver? driver);

        // Assert
        if (!opened)
        {
            driver.Should().BeNull();
        }
        else
        {
            driver.Should().NotBeNull();
            driver!.Dispose();
        }
    }

    [Fact]
    public void Dispose_WhenCalledMultipleTimes_DoesNotThrow()
    {
        // Arrange - Try to open driver
        bool opened = LhmDriver.TryOpen(out LhmDriver? driver);

        if (!opened)
        {
            // Skip test if PawnIO isn't available
            return;
        }

        // Act & Assert
        driver!.Dispose();
        driver.Invoking(d => d.Dispose()).Should().NotThrow();
    }

    [Fact]
    public void ReadByte_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        bool opened = LhmDriver.TryOpen(out LhmDriver? driver);

        if (!opened)
        {
            // Skip test if PawnIO isn't available
            return;
        }

        driver!.Dispose();

        // Act & Assert
        driver.Invoking(d => d.ReadByte(0x66))
            .Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void WriteByte_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        bool opened = LhmDriver.TryOpen(out LhmDriver? driver);

        if (!opened)
        {
            // Skip test if PawnIO isn't available
            return;
        }

        driver!.Dispose();

        // Act & Assert
        driver.Invoking(d => d.WriteByte(0x66, 0x00))
            .Should().Throw<ObjectDisposedException>();
    }
}
