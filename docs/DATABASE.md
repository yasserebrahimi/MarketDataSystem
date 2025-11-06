# Database Guide

Complete guide for database management in MarketData system.

---

## 📋 Table of Contents

- [Overview](#overview)
- [Database Schema](#database-schema)
- [Entity Framework Core](#entity-framework-core)
- [Migrations](#migrations)
- [Connection Strings](#connection-strings)
- [Repositories](#repositories)
- [Performance Optimization](#performance-optimization)

---

## Overview

MarketData uses **PostgreSQL** as the primary database with **Entity Framework Core 8** for data access.

### Key Features:
- ✅ PostgreSQL 15+ support
- ✅ Entity Framework Core 8
- ✅ Code-First migrations
- ✅ Repository pattern
- ✅ Connection pooling
- ✅ Optimized indexes
- ✅ Automatic migrations in development

---

## Database Schema

### Tables

#### 1. Users
Stores user accounts and authentication data.

```sql
CREATE TABLE "Users" (
    "Id" uuid PRIMARY KEY,
    "Username" varchar(50) NOT NULL UNIQUE,
    "Email" varchar(255) NOT NULL UNIQUE,
    "PasswordHash" varchar(500) NOT NULL,
    "FirstName" varchar(100),
    "LastName" varchar(100),
    "Role" varchar(50) NOT NULL DEFAULT 'User',
    "IsActive" boolean NOT NULL DEFAULT true,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "LastLoginAt" timestamp with time zone,
    "RefreshToken" varchar(500),
    "RefreshTokenExpiryTime" timestamp with time zone
);

CREATE INDEX "IX_Users_IsActive" ON "Users"("IsActive");
CREATE INDEX "IX_Users_Role" ON "Users"("Role");
```

#### 2. SymbolStatistics
Stores aggregated statistics for each symbol.

```sql
CREATE TABLE "SymbolStatistics" (
    "Symbol" varchar(10) PRIMARY KEY,
    "CurrentPrice" numeric(18,8) NOT NULL,
    "MovingAverage" numeric(18,8) NOT NULL,
    "MinPrice" numeric(18,8) NOT NULL,
    "MaxPrice" numeric(18,8) NOT NULL,
    "UpdateCount" bigint NOT NULL,
    "LastUpdateTime" timestamp with time zone NOT NULL
);

CREATE INDEX "IX_SymbolStatistics_LastUpdateTime" ON "SymbolStatistics"("LastUpdateTime");
```

#### 3. PriceUpdates
Stores individual price updates (historical data).

```sql
CREATE TABLE "PriceUpdates" (
    "Id" uuid PRIMARY KEY,
    "Symbol" varchar(10) NOT NULL,
    "Price" numeric(18,8) NOT NULL,
    "Timestamp" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX "IX_PriceUpdates_Symbol" ON "PriceUpdates"("Symbol");
CREATE INDEX "IX_PriceUpdates_Timestamp" ON "PriceUpdates"("Timestamp");
CREATE INDEX "IX_PriceUpdates_Symbol_Timestamp" ON "PriceUpdates"("Symbol", "Timestamp");
```

#### 4. PriceAnomalies
Stores detected price anomalies.

```sql
CREATE TABLE "PriceAnomalies" (
    "Id" uuid PRIMARY KEY,
    "Symbol" varchar(10) NOT NULL,
    "OldPrice" numeric(18,8) NOT NULL,
    "NewPrice" numeric(18,8) NOT NULL,
    "ChangePercent" numeric(18,4) NOT NULL,
    "DetectedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "Severity" varchar(20) NOT NULL
);

CREATE INDEX "IX_PriceAnomalies_Symbol" ON "PriceAnomalies"("Symbol");
CREATE INDEX "IX_PriceAnomalies_DetectedAt" ON "PriceAnomalies"("DetectedAt");
CREATE INDEX "IX_PriceAnomalies_Severity" ON "PriceAnomalies"("Severity");
CREATE INDEX "IX_PriceAnomalies_Symbol_DetectedAt" ON "PriceAnomalies"("Symbol", "DetectedAt");
```

---

## Entity Framework Core

### DbContext

```csharp
public class MarketDataDbContext : DbContext
{
    public DbSet<PriceUpdate> PriceUpdates => Set<PriceUpdate>();
    public DbSet<SymbolStatistics> SymbolStatistics => Set<SymbolStatistics>();
    public DbSet<PriceAnomaly> PriceAnomalies => Set<PriceAnomaly>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all entity configurations
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
```

### Entity Configurations

Entity configurations use FluentAPI for schema definition:

```csharp
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Username).IsRequired().HasMaxLength(50);
        builder.HasIndex(u => u.Username).IsUnique();
        // ... more configuration
    }
}
```

---

## Migrations

### Creating Migrations

```bash
# Navigate to Infrastructure project
cd src/MarketData.Infrastructure

# Create a new migration
dotnet ef migrations add MigrationName --startup-project ../MarketData.API

# Example
dotnet ef migrations add AddUserTable --startup-project ../MarketData.API
```

### Applying Migrations

**Development (Automatic):**
Migrations run automatically on startup when `ASPNETCORE_ENVIRONMENT=Development`

**Manual:**
```bash
dotnet ef database update --startup-project ../MarketData.API
```

**Specific Migration:**
```bash
dotnet ef database update MigrationName --startup-project ../MarketData.API
```

### Rolling Back

```bash
# Rollback to specific migration
dotnet ef database update PreviousMigrationName --startup-project ../MarketData.API

# Rollback all migrations
dotnet ef database update 0 --startup-project ../MarketData.API
```

### Migration Files

Located in: `src/MarketData.Infrastructure/Data/Migrations/`

Current migrations:
- `20250106_InitialCreate.cs` - Initial schema creation

---

## Connection Strings

### Development

`appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=marketdata_dev;Username=postgres;Password=postgres"
  }
}
```

### Staging

`appsettings.Staging.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=postgres-staging;Port=5432;Database=marketdata_staging;Username=${DB_USER};Password=${DB_PASSWORD}"
  }
}
```

### Production

`appsettings.Production.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=${DB_HOST};Port=${DB_PORT};Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASSWORD};SSL Mode=Require"
  }
}
```

### Connection String Parameters

| Parameter | Description | Example |
|-----------|-------------|---------|
| Host | Database server | localhost, postgres |
| Port | Database port | 5432 |
| Database | Database name | marketdata |
| Username | DB username | postgres |
| Password | DB password | secure_password |
| SSL Mode | SSL requirement | Disable, Require |
| Pooling | Connection pooling | true (default) |
| MinPoolSize | Min connections | 0 (default) |
| MaxPoolSize | Max connections | 100 (default) |

---

## Repositories

### EF Core Repositories

Implementation of persistent storage with PostgreSQL:

#### EfStatisticsRepository

```csharp
public class EfStatisticsRepository : IStatisticsRepository
{
    private readonly MarketDataDbContext _context;

    public async Task<SymbolStatistics?> GetBySymbolAsync(string symbol)
    {
        return await _context.SymbolStatistics
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Symbol == symbol);
    }

    public async Task UpdateAsync(SymbolStatistics statistics)
    {
        var existing = await _context.SymbolStatistics.FindAsync(statistics.Symbol);
        if (existing == null)
            _context.SymbolStatistics.Add(statistics);
        else
            _context.Entry(existing).CurrentValues.SetValues(statistics);

        await _context.SaveChangesAsync();
    }
}
```

### Repository Configuration

Enable persistent repositories in `appsettings.json`:

```json
{
  "MarketDataProcessing": {
    "UsePersistentRepositories": true
  }
}
```

When `false`, uses in-memory repositories (default for development).

---

## Performance Optimization

### 1. Indexes

All tables have appropriate indexes for common queries:

- **Users**: Username (unique), Email (unique), IsActive, Role
- **SymbolStatistics**: LastUpdateTime
- **PriceUpdates**: Symbol, Timestamp, Composite (Symbol, Timestamp)
- **PriceAnomalies**: Symbol, DetectedAt, Severity, Composite (Symbol, DetectedAt)

### 2. Connection Pooling

Enabled by default in Npgsql:

```csharp
// Configured via connection string
"Host=localhost;Port=5432;Database=marketdata;MinPoolSize=0;MaxPoolSize=100"
```

### 3. AsNoTracking

Use for read-only queries:

```csharp
var stats = await _context.SymbolStatistics
    .AsNoTracking()  // Improves performance for read-only
    .Where(s => s.Symbol == symbol)
    .ToListAsync();
```

### 4. Bulk Operations

For inserting many records:

```csharp
_context.PriceUpdates.AddRange(priceUpdates);
await _context.SaveChangesAsync();
```

### 5. Query Optimization

```csharp
// Good: Single database call
var result = await _context.PriceUpdates
    .Where(p => p.Symbol == symbol)
    .OrderByDescending(p => p.Timestamp)
    .Take(100)
    .ToListAsync();

// Bad: Multiple database calls
foreach (var symbol in symbols)
{
    var price = await _context.PriceUpdates
        .FirstAsync(p => p.Symbol == symbol);
}
```

---

## Database Seeding

Initial data is seeded automatically in development:

```csharp
public class DatabaseSeeder
{
    public async Task SeedAsync()
    {
        if (!await _context.Users.AnyAsync())
        {
            var adminHash = BCrypt.Net.BCrypt.HashPassword("Admin@123");
            var admin = new User("admin", "admin@marketdata.com", adminHash, "Admin");
            _context.Users.Add(admin);

            var demoHash = BCrypt.Net.BCrypt.HashPassword("Demo@123");
            var demo = new User("demo", "demo@marketdata.com", demoHash, "User");
            _context.Users.Add(demo);

            await _context.SaveChangesAsync();
        }
    }
}
```

---

## Backup and Restore

### Backup

```bash
# Backup entire database
pg_dump -h localhost -U postgres -d marketdata > backup.sql

# Backup with compression
pg_dump -h localhost -U postgres -d marketdata | gzip > backup.sql.gz

# Backup specific tables
pg_dump -h localhost -U postgres -d marketdata -t Users -t PriceAnomalies > backup.sql
```

### Restore

```bash
# Restore from backup
psql -h localhost -U postgres -d marketdata < backup.sql

# Restore from compressed backup
gunzip -c backup.sql.gz | psql -h localhost -U postgres -d marketdata
```

### Azure Automated Backups

Azure PostgreSQL Flexible Server provides:
- Automatic daily backups
- 7-day retention (configurable)
- Point-in-time restore
- Geo-redundant backups (optional)

---

## Monitoring

### Query Performance

Enable query logging in `appsettings.json`:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Override": {
        "Microsoft.EntityFrameworkCore": "Information"
      }
    }
  }
}
```

### Health Checks

Database health check is included:

```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString,
        name: "postgres",
        tags: new[] { "db", "sql", "postgres" });
```

Check at: `GET /health`

---

## Troubleshooting

### Common Issues

#### 1. Connection Refused

**Problem**: Cannot connect to PostgreSQL

**Solutions**:
- Check PostgreSQL is running: `docker ps | grep postgres`
- Verify connection string
- Check firewall rules
- Ensure database exists

#### 2. Migration Errors

**Problem**: Migration fails to apply

**Solutions**:
- Check PostgreSQL version compatibility
- Verify user permissions
- Drop and recreate database (development only)
- Check migration file for errors

#### 3. Slow Queries

**Problem**: Queries taking too long

**Solutions**:
- Add indexes to frequently queried columns
- Use `.AsNoTracking()` for read-only queries
- Enable query logging to identify slow queries
- Consider pagination for large result sets

---

## Docker Setup

### docker-compose.yml

```yaml
postgres:
  image: postgres:15-alpine
  environment:
    POSTGRES_DB: marketdata
    POSTGRES_USER: postgres
    POSTGRES_PASSWORD: postgres
  ports:
    - "5432:5432"
  volumes:
    - postgres-data:/var/lib/postgresql/data
```

### Start PostgreSQL

```bash
docker-compose up -d postgres
```

---

## Azure PostgreSQL

### Terraform Configuration

```hcl
resource "azurerm_postgresql_flexible_server" "postgres" {
  name                   = "marketdata-postgres"
  resource_group_name    = azurerm_resource_group.main.name
  location               = azurerm_resource_group.main.location
  version                = "15"
  storage_mb             = 32768
  sku_name               = "GP_Standard_D4s_v3"
  backup_retention_days  = 7
}
```

### Connection String

```
Host=marketdata-postgres.postgres.database.azure.com;
Port=5432;
Database=marketdata;
Username=postgres@marketdata-postgres;
Password=<your-password>;
SSL Mode=Require
```

---

**Last Updated:** 2025-11-06
