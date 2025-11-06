using MarketData.Application.Commands;
using MarketData.Application.Interfaces;
using MarketData.Infrastructure.Services;
using MarketData.Infrastructure.Options;
using MarketData.Infrastructure.Processing;
using MarketData.Infrastructure.Repositories;
using FluentValidation;
using FluentValidation.AspNetCore;
using Serilog;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MarketDataProcessingOptions>(builder.Configuration.GetSection("MarketDataProcessing"));

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() 
    { 
        Title = "Market Data API", 
        Version = "v1",
        Description = "High-Performance Real-Time Market Data Processing System",
        Contact = new() 
        { 
            Name = "Yasser Ebrahimi Fard",
            Email = "yasser.ebrahimi@outlook.com"
        }
    });
});

// MediatR
builder.Services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(typeof(ProcessPriceUpdateCommand).Assembly));

// FluentValidation
builder.Services.AddValidatorsFromAssembly(typeof(ProcessPriceUpdateCommand).Assembly);
builder.Services.AddFluentValidationAutoValidation();

// Application Services
builder.Services.AddSingleton<HighPerformanceMarketDataProcessorService>();
builder.Services.AddSingleton<IMarketDataProcessor>(sp => sp.GetRequiredService<HighPerformanceMarketDataProcessorService>());
builder.Services.AddSingleton<IStatisticsRepository, InMemoryStatisticsRepository>();
builder.Services.AddSingleton<IAnomalyRepository, InMemoryAnomalyRepository>();

builder.Services.AddHostedService(sp => sp.GetRequiredService<HighPerformanceMarketDataProcessorService>());
builder.Services.AddHostedService<SimulatedMarketDataFeedHostedService>();

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<MarketDataHealthCheck>("market_data_processor");

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

app.UseHttpsRedirection();
app.UseCors();
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
