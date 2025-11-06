using FluentAssertions;
using MarketData.Application.Commands;
using MarketData.Application.Validators;
using Xunit;

namespace MarketData.Tests.Unit.Application.Validators;

public class ProcessPriceUpdateCommandValidatorTests
{
    private readonly ProcessPriceUpdateCommandValidator _validator;

    public ProcessPriceUpdateCommandValidatorTests()
    {
        _validator = new ProcessPriceUpdateCommandValidator();
    }

    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
    {
        // Arrange
        var command = new ProcessPriceUpdateCommand
        {
            Symbol = "AAPL",
            Price = 150.50m,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_ShouldFail_WhenSymbolIsEmpty(string symbol)
    {
        // Arrange
        var command = new ProcessPriceUpdateCommand
        {
            Symbol = symbol,
            Price = 150.50m,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Symbol));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_ShouldFail_WhenPriceIsZeroOrNegative(decimal price)
    {
        // Arrange
        var command = new ProcessPriceUpdateCommand
        {
            Symbol = "AAPL",
            Price = price,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Price));
    }

    [Fact]
    public void Validate_ShouldFail_WhenPriceIsTooLarge()
    {
        // Arrange
        var command = new ProcessPriceUpdateCommand
        {
            Symbol = "AAPL",
            Price = 2_000_000m,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldFail_WhenTimestampIsInFuture()
    {
        // Arrange
        var command = new ProcessPriceUpdateCommand
        {
            Symbol = "AAPL",
            Price = 150.50m,
            Timestamp = DateTime.UtcNow.AddHours(1)
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Timestamp));
    }

    [Fact]
    public void Validate_ShouldFail_WhenSymbolContainsInvalidCharacters()
    {
        // Arrange
        var command = new ProcessPriceUpdateCommand
        {
            Symbol = "AAPL123",
            Price = 150.50m,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
    }
}
