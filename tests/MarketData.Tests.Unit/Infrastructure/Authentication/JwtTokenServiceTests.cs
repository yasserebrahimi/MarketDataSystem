using FluentAssertions;
using MarketData.Domain.Entities;
using MarketData.Infrastructure.Authentication;
using Microsoft.Extensions.Options;
using Xunit;

namespace MarketData.Tests.Unit.Infrastructure.Authentication;

public class JwtTokenServiceTests
{
    private readonly JwtTokenService _service;
    private readonly JwtSettings _settings;

    public JwtTokenServiceTests()
    {
        _settings = new JwtSettings
        {
            SecretKey = "THIS_IS_A_TEST_SECRET_KEY_AT_LEAST_32_CHARACTERS_LONG",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessTokenExpirationMinutes = 60,
            RefreshTokenExpirationDays = 7
        };

        var options = Options.Create(_settings);
        _service = new JwtTokenService(options);
    }

    [Fact]
    public void GenerateAccessToken_ShouldReturnValidToken()
    {
        // Arrange
        var user = new User("testuser", "test@example.com", "hash", "User");

        // Act
        var token = _service.GenerateAccessToken(user);

        // Assert
        token.Should().NotBeNullOrEmpty();
        token.Split('.').Should().HaveCount(3); // JWT has 3 parts
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnUniqueTokens()
    {
        // Act
        var token1 = _service.GenerateRefreshToken();
        var token2 = _service.GenerateRefreshToken();

        // Assert
        token1.Should().NotBeNullOrEmpty();
        token2.Should().NotBeNullOrEmpty();
        token1.Should().NotBe(token2);
    }

    [Fact]
    public void GetAccessTokenExpiryTime_ShouldReturnFutureTime()
    {
        // Act
        var expiry = _service.GetAccessTokenExpiryTime();

        // Assert
        expiry.Should().BeAfter(DateTime.UtcNow);
        expiry.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GetRefreshTokenExpiryTime_ShouldReturnFutureTime()
    {
        // Act
        var expiry = _service.GetRefreshTokenExpiryTime();

        // Assert
        expiry.Should().BeAfter(DateTime.UtcNow);
        expiry.Should().BeCloseTo(DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_ShouldReturnPrincipal_ForValidToken()
    {
        // Arrange
        var user = new User("testuser", "test@example.com", "hash", "User");
        var token = _service.GenerateAccessToken(user);

        // Act
        var principal = _service.GetPrincipalFromExpiredToken(token);

        // Assert
        principal.Should().NotBeNull();
        principal!.Identity!.Name.Should().Be(user.Username);
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_ShouldReturnNull_ForInvalidToken()
    {
        // Act
        var principal = _service.GetPrincipalFromExpiredToken("invalid.token.here");

        // Assert
        principal.Should().BeNull();
    }
}
