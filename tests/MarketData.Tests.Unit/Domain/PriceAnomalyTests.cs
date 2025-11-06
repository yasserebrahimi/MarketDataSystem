using FluentAssertions;
using MarketData.Domain.Entities;
using Xunit;

namespace MarketData.Tests.Unit.Domain;

public class PriceAnomalyTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithValidData()
    {
        // Arrange
        var symbol = "AAPL";
        var oldPrice = 100m;
        var newPrice = 105m;
        var changePercent = 5m;

        // Act
        var anomaly = new PriceAnomaly(symbol, oldPrice, newPrice, changePercent);

        // Assert
        anomaly.Symbol.Should().Be(symbol);
        anomaly.OldPrice.Should().Be(oldPrice);
        anomaly.NewPrice.Should().Be(newPrice);
        anomaly.ChangePercent.Should().Be(changePercent);
        anomaly.Id.Should().NotBeEmpty();
        anomaly.DetectedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Constructor_ShouldThrowException_WhenSymbolIsNull()
    {
        // Act
        Action act = () => new PriceAnomaly(null!, 100m, 105m, 5m);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(2.5, "Medium")]
    [InlineData(3.5, "High")]
    [InlineData(5.5, "Critical")]
    [InlineData(-2.5, "Medium")]
    [InlineData(-3.5, "High")]
    [InlineData(-6, "Critical")]
    public void Constructor_ShouldSetCorrectSeverity(decimal changePercent, string expectedSeverity)
    {
        // Act
        var anomaly = new PriceAnomaly("AAPL", 100m, 100m + changePercent, changePercent);

        // Assert
        anomaly.Severity.Should().Be(expectedSeverity);
    }
}
