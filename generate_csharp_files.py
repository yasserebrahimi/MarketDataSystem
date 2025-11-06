#!/usr/bin/env python3
"""
Generate complete C# solution with Clean Architecture
This creates a production-ready market data processing system
"""

import os
from pathlib import Path

base_path = Path("/home/claude/MarketDataSystem")

# Dictionary of all files to create
csharp_files = {}

# ==================== DOMAIN LAYER ====================

csharp_files["src/MarketData.Domain/MarketData.Domain.csproj"] = '''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
'''

csharp_files["src/MarketData.Domain/Entities/PriceUpdate.cs"] = '''namespace MarketData.Domain.Entities;

/// <summary>
/// Represents a single price update for a financial instrument
/// </summary>
public class PriceUpdate
{
    public Guid Id { get; private set; }
    public string Symbol { get; private set; }
    public decimal Price { get; private set; }
    public DateTime Timestamp { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Private constructor for EF Core
    private PriceUpdate() { }

    /// <summary>
    /// Creates a new price update
    /// </summary>
    public PriceUpdate(string symbol, decimal price, DateTime timestamp)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be empty", nameof(symbol));
        
        if (price <= 0)
            throw new ArgumentException("Price must be positive", nameof(price));

        Id = Guid.NewGuid();
        Symbol = symbol.ToUpperInvariant();
        Price = price;
        Timestamp = timestamp;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdatePrice(decimal newPrice)
    {
        if (newPrice <= 0)
            throw new ArgumentException("Price must be positive", nameof(newPrice));
        
        Price = newPrice;
        Timestamp = DateTime.UtcNow;
    }
}
'''

csharp_files["src/MarketData.Domain/Entities/SymbolStatistics.cs"] = '''namespace MarketData.Domain.Entities;

/// <summary>
/// Aggregated statistics for a symbol
/// </summary>
public class SymbolStatistics
{
    public string Symbol { get; private set; }
    public decimal CurrentPrice { get; private set; }
    public decimal MovingAverage { get; private set; }
    public long UpdateCount { get; private set; }
    public DateTime LastUpdateTime { get; private set; }
    public decimal MinPrice { get; private set; }
    public decimal MaxPrice { get; private set; }

    private SymbolStatistics() { }

    public SymbolStatistics(string symbol)
    {
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        MinPrice = decimal.MaxValue;
        MaxPrice = decimal.MinValue;
        UpdateCount = 0;
    }

    public void Update(decimal price, decimal movingAverage)
    {
        CurrentPrice = price;
        MovingAverage = movingAverage;
        UpdateCount++;
        LastUpdateTime = DateTime.UtcNow;
        
        if (price < MinPrice) MinPrice = price;
        if (price > MaxPrice) MaxPrice = price;
    }
}
'''

csharp_files["src/MarketData.Domain/Entities/PriceAnomaly.cs"] = '''namespace MarketData.Domain.Entities;

/// <summary>
/// Represents a detected price anomaly
/// </summary>
public class PriceAnomaly
{
    public Guid Id { get; private set; }
    public string Symbol { get; private set; }
    public decimal OldPrice { get; private set; }
    public decimal NewPrice { get; private set; }
    public decimal ChangePercent { get; private set; }
    public DateTime DetectedAt { get; private set; }
    public string Severity { get; private set; }

    private PriceAnomaly() { }

    public PriceAnomaly(
        string symbol,
        decimal oldPrice,
        decimal newPrice,
        decimal changePercent)
    {
        Id = Guid.NewGuid();
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        OldPrice = oldPrice;
        NewPrice = newPrice;
        ChangePercent = changePercent;
        DetectedAt = DateTime.UtcNow;
        Severity = DetermineSeverity(changePercent);
    }

    private string DetermineSeverity(decimal changePercent)
    {
        var absChange = Math.Abs(changePercent);
        if (absChange > 5) return "Critical";
        if (absChange > 3) return "High";
        return "Medium";
    }
}
'''

csharp_files["src/MarketData.Domain/ValueObjects/Symbol.cs"] = '''namespace MarketData.Domain.ValueObjects;

/// <summary>
/// Value object representing a trading symbol
/// </summary>
public record Symbol
{
    public string Value { get; init; }

    public Symbol(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Symbol cannot be empty", nameof(value));

        if (value.Length > 10)
            throw new ArgumentException("Symbol cannot exceed 10 characters", nameof(value));

        Value = value.ToUpperInvariant();
    }

    public static implicit operator string(Symbol symbol) => symbol.Value;
    public static implicit operator Symbol(string value) => new(value);

    public override string ToString() => Value;
}
'''

# ==================== APPLICATION LAYER ====================

csharp_files["src/MarketData.Application/MarketData.Application.csproj"] = '''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MediatR" Version="12.2.0" />
    <PackageReference Include="FluentValidation" Version="11.9.0" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.9.0" />
    <PackageReference Include="AutoMapper" Version="12.0.1" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../MarketData.Domain/MarketData.Domain.csproj" />
  </ItemGroup>
</Project>
'''

csharp_files["src/MarketData.Application/Commands/ProcessPriceUpdateCommand.cs"] = '''using MediatR;
using MarketData.Application.DTOs;

namespace MarketData.Application.Commands;

/// <summary>
/// Command to process a new price update
/// Follows CQRS pattern - commands represent state changes
/// </summary>
public record ProcessPriceUpdateCommand : IRequest<PriceUpdateResultDto>
{
    public string Symbol { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public DateTime Timestamp { get; init; }
}
'''

print("Creating complete C# project files...")
print(f"Total files to create: {len(csharp_files)}")

# Create all files
for file_path, content in csharp_files.items():
    full_path = base_path / file_path
    full_path.parent.mkdir(parents=True, exist_ok=True)
    with open(full_path, 'w', encoding='utf-8') as f:
        f.write(content)
    print(f"Created: {file_path}")

print("\n✅ All C# files created successfully!")

