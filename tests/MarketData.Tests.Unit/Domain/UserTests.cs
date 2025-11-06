using FluentAssertions;
using MarketData.Domain.Entities;
using Xunit;

namespace MarketData.Tests.Unit.Domain;

public class UserTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithValidData()
    {
        // Arrange
        var username = "testuser";
        var email = "test@example.com";
        var passwordHash = "hashedpassword123";

        // Act
        var user = new User(username, email, passwordHash);

        // Assert
        user.Username.Should().Be(username);
        user.Email.Should().Be(email.ToLowerInvariant());
        user.PasswordHash.Should().Be(passwordHash);
        user.Role.Should().Be("User");
        user.IsActive.Should().BeTrue();
        user.Id.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("", "test@example.com", "hash")]
    [InlineData("user", "", "hash")]
    [InlineData("user", "test@example.com", "")]
    public void Constructor_ShouldThrowException_WhenRequiredFieldsEmpty(string username, string email, string hash)
    {
        // Act
        Action act = () => new User(username, email, hash);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdatePassword_ShouldChangePasswordHash()
    {
        // Arrange
        var user = new User("user", "test@example.com", "oldHash");
        var newHash = "newHash";

        // Act
        user.UpdatePassword(newHash);

        // Assert
        user.PasswordHash.Should().Be(newHash);
    }

    [Fact]
    public void UpdateRefreshToken_ShouldSetTokenAndExpiry()
    {
        // Arrange
        var user = new User("user", "test@example.com", "hash");
        var token = "refreshToken123";
        var expiry = DateTime.UtcNow.AddDays(7);

        // Act
        user.UpdateRefreshToken(token, expiry);

        // Assert
        user.RefreshToken.Should().Be(token);
        user.RefreshTokenExpiryTime.Should().Be(expiry);
    }

    [Fact]
    public void ClearRefreshToken_ShouldRemoveTokenAndExpiry()
    {
        // Arrange
        var user = new User("user", "test@example.com", "hash");
        user.UpdateRefreshToken("token", DateTime.UtcNow.AddDays(7));

        // Act
        user.ClearRefreshToken();

        // Assert
        user.RefreshToken.Should().BeNull();
        user.RefreshTokenExpiryTime.Should().BeNull();
    }

    [Fact]
    public void UpdateLastLogin_ShouldSetLastLoginTime()
    {
        // Arrange
        var user = new User("user", "test@example.com", "hash");

        // Act
        user.UpdateLastLogin();

        // Assert
        user.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveToFalse()
    {
        // Arrange
        var user = new User("user", "test@example.com", "hash");

        // Act
        user.Deactivate();

        // Assert
        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_ShouldSetIsActiveToTrue()
    {
        // Arrange
        var user = new User("user", "test@example.com", "hash");
        user.Deactivate();

        // Act
        user.Activate();

        // Assert
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void UpdateProfile_ShouldSetFirstNameAndLastName()
    {
        // Arrange
        var user = new User("user", "test@example.com", "hash");

        // Act
        user.UpdateProfile("John", "Doe");

        // Assert
        user.FirstName.Should().Be("John");
        user.LastName.Should().Be("Doe");
    }
}
