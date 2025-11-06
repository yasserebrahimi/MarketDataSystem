using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MarketData.Application.Commands;
using Xunit;

namespace MarketData.Tests.Integration.Api;

public class PricesControllerIntegrationTests : IClassFixture<WebApplicationFactoryFixture>
{
    private readonly HttpClient _client;

    public PricesControllerIntegrationTests(WebApplicationFactoryFixture factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ProcessUpdate_ShouldReturn200_WhenValidCommand()
    {
        // Arrange
        var command = new ProcessPriceUpdateCommand
        {
            Symbol = "AAPL",
            Price = 150.50m,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/prices", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProcessUpdate_ShouldReturn400_WhenInvalidPrice()
    {
        // Arrange
        var command = new ProcessPriceUpdateCommand
        {
            Symbol = "AAPL",
            Price = -10m, // Invalid negative price
            Timestamp = DateTime.UtcNow
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/prices", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetStatistics_ShouldReturn404_ForNonExistentSymbol()
    {
        // Act
        var response = await _client.GetAsync("/api/prices/NONEXISTENT/statistics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
