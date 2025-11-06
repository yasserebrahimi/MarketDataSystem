using MarketData.Domain.Entities;
using MarketData.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MarketData.Infrastructure.Repositories;

/// <summary>
/// Repository for managing users with database persistence
/// </summary>
public class UserRepository
{
    private readonly MarketDataDbContext _context;
    private readonly ILogger<UserRepository> _logger;

    public UserRepository(
        MarketDataDbContext context,
        ILogger<UserRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user by ID {UserId}", id);
            throw;
        }
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user by username {Username}", username);
            throw;
        }
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user by email {Email}", email);
            throw;
        }
    }

    public async Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user by refresh token");
            throw;
        }
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user {Username}", user.Username);
            throw;
        }
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {Username}", user.Username);
            throw;
        }
    }

    public async Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Users
                .AnyAsync(u => u.Username == username, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if username exists {Username}", username);
            throw;
        }
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Users
                .AnyAsync(u => u.Email == email.ToLowerInvariant(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if email exists {Email}", email);
            throw;
        }
    }
}
