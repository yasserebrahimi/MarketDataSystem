using FluentAssertions;
using MarketData.Domain.Entities;
using Xunit;

namespace MarketData.Tests.Unit.Domain;

/// <summary>
/// Unit tests for PriceUpdate entity
/// Tests business logic and invariants
/// </summary>
public class PriceUpdateTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesInstance()
    {
        // Arrange
        var symbol = "AAPL";
        var price = 150.50m;
        var timestamp = DateTime.UtcNow;

        // Act
        var update = new PriceUpdate(symbol, price, timestamp);

        // Assert
        update.Symbol.Should().Be("AAPL");
        update.Price.Should().Be(price);
        update.Timestamp.Should().Be(timestamp);
        update.Id.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidSymbol_ThrowsException(string invalidSymbol)
    {
        // Act
        var act = () => new PriceUpdate(invalidSymbol, 100m, DateTime.UtcNow);

        // Assert
        act.Should().Throw<ArgumentException>()
           .WithParameterName("symbol");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_WithInvalidPrice_ThrowsException(decimal invalidPrice)
    {
        // Act
        var act = () => new PriceUpdate("AAPL", invalidPrice, DateTime.UtcNow);

        // Assert
        act.Should().Throw<ArgumentException>()
           .WithParameterName("price");
    }

    [Fact]
    public void UpdatePrice_WithValidPrice_UpdatesSuccessfully()
    {
        // Arrange
        var update = new PriceUpdate("AAPL", 100m, DateTime.UtcNow);
        var newPrice = 150m;

        // Act
        update.UpdatePrice(newPrice);

        // Assert
        update.Price.Should().Be(newPrice);
    }
}
