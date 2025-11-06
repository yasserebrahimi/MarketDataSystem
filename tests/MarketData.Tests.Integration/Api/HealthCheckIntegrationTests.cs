using System.Net;
using FluentAssertions;
using Xunit;

namespace MarketData.Tests.Integration.Api;

public class HealthCheckIntegrationTests : IClassFixture<WebApplicationFactoryFixture>
{
    private readonly HttpClient _client;

    public HealthCheckIntegrationTests(WebApplicationFactoryFixture factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task ReadyCheck_ShouldReturnHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
