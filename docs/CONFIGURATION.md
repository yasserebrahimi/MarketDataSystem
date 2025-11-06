# Configuration Guide

Complete configuration reference for MarketData system.

---

## Configuration Hierarchy

Settings are loaded in this order (later overrides earlier):
1. `appsettings.json` (base configuration)
2. `appsettings.{Environment}.json` (environment-specific)
3. Environment variables
4. Command-line arguments

---

## Configuration Files

### appsettings.json (Base)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=marketdata;Username=postgres;Password=postgres",
    "Redis": "localhost:6379,abortConnect=false"
  },
  "JwtSettings": {
    "SecretKey": "CHANGE_IN_PRODUCTION",
    "Issuer": "MarketDataAPI",
    "Audience": "MarketDataClients",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  },
  "IpRateLimiting": {
    "EnableEndpointRateLimiting": true,
    "GeneralRules": [
      { "Endpoint": "*", "Period": "1s", "Limit": 10 },
      { "Endpoint": "*", "Period": "1m", "Limit": 200 }
    ]
  },
  "MarketDataProcessing": {
    "Partitions": 0,
    "ChannelCapacity": 100000,
    "MovingAverageWindow": 64,
    "UsePersistentRepositories": false
  }
}
```

### appsettings.Development.json
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug"
    }
  },
  "JwtSettings": {
    "AccessTokenExpirationMinutes": 120
  },
  "IpRateLimiting": {
    "EnableEndpointRateLimiting": false
  }
}
```

---

## Environment Variables

Use environment variables to override configuration:

```bash
# Database
export ConnectionStrings__DefaultConnection="Host=prod-db;Database=marketdata;..."
export ConnectionStrings__Redis="redis-prod:6379"

# JWT
export JwtSettings__SecretKey="PRODUCTION_SECRET_KEY"
export JwtSettings__Issuer="MarketDataAPI"

# Application
export ASPNETCORE_ENVIRONMENT="Production"
export ASPNETCORE_URLS="http://+:80;https://+:443"
```

---

## Configuration Sections

### 1. ConnectionStrings
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "PostgreSQL connection string",
    "Redis": "Redis connection string"
  }
}
```

### 2. JwtSettings
```json
{
  "JwtSettings": {
    "SecretKey": "Min 32 characters",
    "Issuer": "Token issuer",
    "Audience": "Token audience",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  }
}
```

### 3. Rate Limiting
```json
{
  "IpRateLimiting": {
    "EnableEndpointRateLimiting": true,
    "GeneralRules": [
      {
        "Endpoint": "*",
        "Period": "1s|1m|1h",
        "Limit": 10
      }
    ]
  }
}
```

### 4. CORS
```json
{
  "Cors": {
    "AllowedOrigins": ["https://example.com"],
    "AllowedMethods": ["GET", "POST"],
    "AllowedHeaders": ["Content-Type", "Authorization"],
    "AllowCredentials": true
  }
}
```

### 5. Logging (Serilog)
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  }
}
```

---

## Kubernetes ConfigMap

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: marketdata-config
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  JwtSettings__Issuer: "MarketDataAPI"
  JwtSettings__Audience: "MarketDataClients"
```

---

## Kubernetes Secrets

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: marketdata-secrets
type: Opaque
stringData:
  ConnectionStrings__DefaultConnection: "Host=postgres;..."
  ConnectionStrings__Redis: "redis:6379"
  JwtSettings__SecretKey: "SECURE_SECRET_KEY"
```

---

**Last Updated:** 2025-11-06
