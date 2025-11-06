using MarketData.Application.Commands;
using MarketData.Application.Interfaces;
using MarketData.Infrastructure.Services;
using MarketData.Infrastructure.Options;
using MarketData.Infrastructure.Processing;
using MarketData.Infrastructure.Repositories;
using MarketData.Infrastructure.Data;
using MarketData.Infrastructure.Authentication;
using MarketData.Infrastructure.Caching;
using FluentValidation;
using FluentValidation.AspNetCore;
using Serilog;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using AspNetCoreRateLimit;

var builder = WebApplication.CreateBuilder(args);

// Configure Options
builder.Services.Configure<MarketDataProcessingOptions>(builder.Configuration.GetSection("MarketDataProcessing"));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add Database Context
var usePersistentRepositories = builder.Configuration.GetValue<bool>("MarketDataProcessing:UsePersistentRepositories");
if (usePersistentRepositories)
{
    builder.Services.AddDbContext<MarketDataDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
}

// Add Redis Distributed Cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "MarketData:";
});

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Market Data API",
        Version = "v1",
        Description = "High-Performance Real-Time Market Data Processing System with Authentication",
        Contact = new()
        {
            Name = "Yasser Ebrahimi Fard",
            Email = "yasser.ebrahimi@outlook.com"
        }
    });

    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings?.Issuer,
        ValidAudience = jwtSettings?.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings?.SecretKey ?? string.Empty)),
        ClockSkew = TimeSpan.Zero
    };
});

// Configure Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireUserRole", policy => policy.RequireRole("User", "Admin"));
    options.AddPolicy("RequireReadAccess", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("RequireWriteAccess", policy => policy.RequireRole("User", "Admin"));
});

// Configure Rate Limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.Configure<IpRateLimitPolicies>(builder.Configuration.GetSection("IpRateLimitPolicies"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(ProcessPriceUpdateCommand).Assembly));

// FluentValidation
builder.Services.AddValidatorsFromAssembly(typeof(ProcessPriceUpdateCommand).Assembly);
builder.Services.AddFluentValidationAutoValidation();

// Authentication & Authorization Services
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<AuthenticationService>();

// Repository Services
if (usePersistentRepositories)
{
    builder.Services.AddScoped<IStatisticsRepository, EfStatisticsRepository>();
    builder.Services.AddScoped<IAnomalyRepository, EfAnomalyRepository>();
    builder.Services.AddScoped<PriceUpdateRepository>();
}
else
{
    builder.Services.AddSingleton<IStatisticsRepository, InMemoryStatisticsRepository>();
    builder.Services.AddSingleton<IAnomalyRepository, InMemoryAnomalyRepository>();
}
builder.Services.AddScoped<UserRepository>();

// Caching Services
builder.Services.AddScoped<RedisCacheService>();

// Application Services
builder.Services.AddSingleton<HighPerformanceMarketDataProcessorService>();
builder.Services.AddSingleton<IMarketDataProcessor>(sp => sp.GetRequiredService<HighPerformanceMarketDataProcessorService>());

builder.Services.AddHostedService(sp => sp.GetRequiredService<HighPerformanceMarketDataProcessorService>());
builder.Services.AddHostedService<SimulatedMarketDataFeedHostedService>();

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<MarketDataHealthCheck>("market_data_processor")
    .AddNpgSql(
        builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty,
        name: "postgres",
        tags: new[] { "db", "sql", "postgres" })
    .AddRedis(
        builder.Configuration.GetConnectionString("Redis") ?? string.Empty,
        name: "redis",
        tags: new[] { "cache", "redis" });

// CORS - Read from configuration
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "*" };
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (corsOrigins.Contains("*"))
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            policy.WithOrigins(corsOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});

var app = builder.Build();

// Run database migrations in development
if (app.Environment.IsDevelopment() && usePersistentRepositories)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetService<MarketDataDbContext>();
    if (dbContext != null)
    {
        try
        {
            await dbContext.Database.MigrateAsync();
            Log.Information("Database migration completed successfully");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Database migration failed or not needed");
        }
    }
}

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Market Data API v1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at root
    });
}

app.UseSerilogRequestLogging();

// Use Rate Limiting
app.UseIpRateLimiting();

app.UseHttpsRedirection();
app.UseCors();

// IMPORTANT: Authentication must come before Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health check endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// Start the processor
var processor = app.Services.GetRequiredService<IMarketDataProcessor>();
await processor.StartAsync();

try
{
    Log.Information("Starting Market Data API");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start");
    throw;
}
finally
{
    await processor.StopAsync();
    Log.CloseAndFlush();
}
