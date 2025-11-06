using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MarketData.Application.DTOs.Auth;
using Xunit;

namespace MarketData.Tests.Integration.Api;

public class AuthControllerIntegrationTests : IClassFixture<WebApplicationFactoryFixture>
{
    private readonly HttpClient _client;

    public AuthControllerIntegrationTests(WebApplicationFactoryFixture factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ShouldReturn201_WhenValidRequest()
    {
        // Arrange
        var request = new RegisterRequestDto
        {
            Username = $"testuser_{Guid.NewGuid()}",
            Email = $"test_{Guid.NewGuid()}@example.com",
            Password = "Test@123",
            FirstName = "Test",
            LastName = "User"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        authResponse.Should().NotBeNull();
        authResponse!.AccessToken.Should().NotBeNullOrEmpty();
        authResponse.RefreshToken.Should().NotBeNullOrEmpty();
        authResponse.User.Username.Should().Be(request.Username);
    }

    [Fact]
    public async Task Login_ShouldReturn200_WithValidCredentials()
    {
        // Arrange - First register a user
        var username = $"testuser_{Guid.NewGuid()}";
        var password = "Test@123";
        var registerRequest = new RegisterRequestDto
        {
            Username = username,
            Email = $"test_{Guid.NewGuid()}@example.com",
            Password = password
        };
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new LoginRequestDto
        {
            Username = username,
            Password = password
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        authResponse.Should().NotBeNull();
        authResponse!.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_ShouldReturn401_WithInvalidCredentials()
    {
        // Arrange
        var loginRequest = new LoginRequestDto
        {
            Username = "nonexistent",
            Password = "wrongpassword"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentUser_ShouldReturn401_WithoutToken()
    {
        // Act
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentUser_ShouldReturn200_WithValidToken()
    {
        // Arrange - Register and login
        var username = $"testuser_{Guid.NewGuid()}";
        var registerRequest = new RegisterRequestDto
        {
            Username = username,
            Email = $"test_{Guid.NewGuid()}@example.com",
            Password = "Test@123"
        };
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var authResponse = await registerResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        // Add token to request
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.AccessToken);

        // Act
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var userDto = await response.Content.ReadFromJsonAsync<UserDto>();
        userDto.Should().NotBeNull();
        userDto!.Username.Should().Be(username);
    }
}
