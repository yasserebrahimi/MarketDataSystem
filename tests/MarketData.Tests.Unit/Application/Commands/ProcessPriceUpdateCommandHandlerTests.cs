using FluentAssertions;
using MarketData.Application.Commands;
using MarketData.Application.Interfaces;
using Moq;
using Xunit;

namespace MarketData.Tests.Unit.Application.Commands;

public class ProcessPriceUpdateCommandHandlerTests
{
    private readonly Mock<IMarketDataProcessor> _mockProcessor;
    private readonly ProcessPriceUpdateCommandHandler _handler;

    public ProcessPriceUpdateCommandHandlerTests()
    {
        _mockProcessor = new Mock<IMarketDataProcessor>();
        _handler = new ProcessPriceUpdateCommandHandler(_mockProcessor.Object);
    }

    [Fact]
    public async Task Handle_ShouldProcessPriceUpdate_WhenValidCommand()
    {
        // Arrange
        var command = new ProcessPriceUpdateCommand
        {
            Symbol = "AAPL",
            Price = 150.50m,
            Timestamp = DateTime.UtcNow
        };

        _mockProcessor.Setup(x => x.ProcessPriceAsync(command.Symbol, command.Price, command.Timestamp))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Symbol.Should().Be(command.Symbol);
        result.Price.Should().Be(command.Price);
        _mockProcessor.Verify(x => x.ProcessPriceAsync(command.Symbol, command.Price, command.Timestamp), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenProcessorThrows()
    {
        // Arrange
        var command = new ProcessPriceUpdateCommand
        {
            Symbol = "AAPL",
            Price = 150.50m,
            Timestamp = DateTime.UtcNow
        };

        _mockProcessor.Setup(x => x.ProcessPriceAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new Exception("Processing error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _handler.Handle(command, CancellationToken.None));
    }
}
