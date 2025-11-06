using MarketData.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MarketData.Infrastructure.Data.Seed;

/// <summary>
/// Seeds initial data into the database
/// </summary>
public class DatabaseSeeder
{
    private readonly MarketDataDbContext _context;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(MarketDataDbContext context, ILogger<DatabaseSeeder> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SeedAsync()
    {
        try
        {
            // Ensure database is created
            await _context.Database.EnsureCreatedAsync();

            // Seed admin user if no users exist
            if (!await _context.Users.AnyAsync())
            {
                var adminPasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123");
                var adminUser = new User("admin", "admin@marketdata.com", adminPasswordHash, "Admin");
                adminUser.UpdateProfile("System", "Administrator");

                _context.Users.Add(adminUser);

                var demoPasswordHash = BCrypt.Net.BCrypt.HashPassword("Demo@123");
                var demoUser = new User("demo", "demo@marketdata.com", demoPasswordHash, "User");
                demoUser.UpdateProfile("Demo", "User");

                _context.Users.Add(demoUser);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Seeded initial users: admin and demo");
            }

            _logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding database");
            throw;
        }
    }
}
