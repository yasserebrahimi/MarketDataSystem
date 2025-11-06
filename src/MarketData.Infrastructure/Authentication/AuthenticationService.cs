using MarketData.Application.DTOs.Auth;
using MarketData.Domain.Entities;
using MarketData.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using BCrypt.Net;

namespace MarketData.Infrastructure.Authentication;

/// <summary>
/// Service for handling user authentication operations
/// </summary>
public class AuthenticationService
{
    private readonly UserRepository _userRepository;
    private readonly JwtTokenService _jwtTokenService;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        UserRepository userRepository,
        JwtTokenService jwtTokenService,
        ILogger<AuthenticationService> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Authenticates a user and returns tokens
    /// </summary>
    public async Task<AuthResponseDto?> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userRepository.GetByUsernameAsync(request.Username, cancellationToken);

            if (user == null || !user.IsActive)
            {
                _logger.LogWarning("Login failed for username {Username}: User not found or inactive", request.Username);
                return null;
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                _logger.LogWarning("Login failed for username {Username}: Invalid password", request.Username);
                return null;
            }

            var accessToken = _jwtTokenService.GenerateAccessToken(user);
            var refreshToken = _jwtTokenService.GenerateRefreshToken();
            var refreshTokenExpiry = _jwtTokenService.GetRefreshTokenExpiryTime();

            user.UpdateRefreshToken(refreshToken, refreshTokenExpiry);
            user.UpdateLastLogin();
            await _userRepository.UpdateAsync(user, cancellationToken);

            _logger.LogInformation("User {Username} logged in successfully", request.Username);

            return new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = _jwtTokenService.GetAccessTokenExpiryTime(),
                User = MapToUserDto(user)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for username {Username}", request.Username);
            throw;
        }
    }

    /// <summary>
    /// Registers a new user
    /// </summary>
    public async Task<AuthResponseDto?> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await _userRepository.ExistsByUsernameAsync(request.Username, cancellationToken))
            {
                _logger.LogWarning("Registration failed: Username {Username} already exists", request.Username);
                return null;
            }

            if (await _userRepository.ExistsByEmailAsync(request.Email, cancellationToken))
            {
                _logger.LogWarning("Registration failed: Email {Email} already exists", request.Email);
                return null;
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            var user = new User(request.Username, request.Email, passwordHash, "User");
            user.UpdateProfile(request.FirstName, request.LastName);

            await _userRepository.AddAsync(user, cancellationToken);

            _logger.LogInformation("User {Username} registered successfully", request.Username);

            // Auto-login after registration
            return await LoginAsync(new LoginRequestDto
            {
                Username = request.Username,
                Password = request.Password
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for username {Username}", request.Username);
            throw;
        }
    }

    /// <summary>
    /// Refreshes an access token using a refresh token
    /// </summary>
    public async Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userRepository.GetByRefreshTokenAsync(refreshToken, cancellationToken);

            if (user == null || !user.IsActive)
            {
                _logger.LogWarning("Refresh token failed: Invalid or inactive user");
                return null;
            }

            if (user.RefreshTokenExpiryTime == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                _logger.LogWarning("Refresh token failed for user {Username}: Token expired", user.Username);
                return null;
            }

            var accessToken = _jwtTokenService.GenerateAccessToken(user);
            var newRefreshToken = _jwtTokenService.GenerateRefreshToken();
            var refreshTokenExpiry = _jwtTokenService.GetRefreshTokenExpiryTime();

            user.UpdateRefreshToken(newRefreshToken, refreshTokenExpiry);
            await _userRepository.UpdateAsync(user, cancellationToken);

            _logger.LogInformation("Token refreshed successfully for user {Username}", user.Username);

            return new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = _jwtTokenService.GetAccessTokenExpiryTime(),
                User = MapToUserDto(user)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            throw;
        }
    }

    /// <summary>
    /// Logs out a user by clearing their refresh token
    /// </summary>
    public async Task<bool> LogoutAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("Logout failed: User {UserId} not found", userId);
                return false;
            }

            user.ClearRefreshToken();
            await _userRepository.UpdateAsync(user, cancellationToken);

            _logger.LogInformation("User {Username} logged out successfully", user.Username);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Changes a user's password
    /// </summary>
    public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("Change password failed: User {UserId} not found", userId);
                return false;
            }

            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            {
                _logger.LogWarning("Change password failed for user {Username}: Invalid current password", user.Username);
                return false;
            }

            var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.UpdatePassword(newPasswordHash);
            user.ClearRefreshToken(); // Force re-login

            await _userRepository.UpdateAsync(user, cancellationToken);

            _logger.LogInformation("Password changed successfully for user {Username}", user.Username);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during password change for user {UserId}", userId);
            throw;
        }
    }

    private static UserDto MapToUserDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role
        };
    }
}
