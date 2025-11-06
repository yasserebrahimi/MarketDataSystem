using FluentAssertions;
using MarketData.Domain.Entities;
using Xunit;

namespace MarketData.Tests.Unit.Domain;

public class SymbolStatisticsTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithValidSymbol()
    {
        // Arrange
        var symbol = "AAPL";

        // Act
        var stats = new SymbolStatistics(symbol);

        // Assert
        stats.Symbol.Should().Be(symbol);
        stats.UpdateCount.Should().Be(0);
        stats.MinPrice.Should().Be(decimal.MaxValue);
        stats.MaxPrice.Should().Be(decimal.MinValue);
    }

    [Fact]
    public void Constructor_ShouldThrowException_WhenSymbolIsNull()
    {
        // Act
        Action act = () => new SymbolStatistics(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Update_ShouldUpdateAllFields()
    {
        // Arrange
        var stats = new SymbolStatistics("AAPL");
        var price = 150.50m;
        var movingAverage = 149.75m;

        // Act
        stats.Update(price, movingAverage);

        // Assert
        stats.CurrentPrice.Should().Be(price);
        stats.MovingAverage.Should().Be(movingAverage);
        stats.UpdateCount.Should().Be(1);
        stats.MinPrice.Should().Be(price);
        stats.MaxPrice.Should().Be(price);
    }

    [Fact]
    public void Update_ShouldTrackMinAndMaxPrices()
    {
        // Arrange
        var stats = new SymbolStatistics("AAPL");

        // Act
        stats.Update(150m, 150m);
        stats.Update(155m, 152m);
        stats.Update(145m, 150m);

        // Assert
        stats.MinPrice.Should().Be(145m);
        stats.MaxPrice.Should().Be(155m);
        stats.UpdateCount.Should().Be(3);
    }

    [Fact]
    public void Update_ShouldIncrementUpdateCount()
    {
        // Arrange
        var stats = new SymbolStatistics("AAPL");

        // Act
        stats.Update(150m, 150m);
        stats.Update(151m, 150.5m);
        stats.Update(152m, 151m);

        // Assert
        stats.UpdateCount.Should().Be(3);
    }
}
