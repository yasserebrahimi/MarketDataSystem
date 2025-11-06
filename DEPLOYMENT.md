# MarketData System - Deployment Guide

## 📋 Table of Contents
- [Prerequisites](#prerequisites)
- [Local Development](#local-development)
- [Database Migration](#database-migration)
- [Docker Deployment](#docker-deployment)
- [Kubernetes Deployment](#kubernetes-deployment)
- [Azure Deployment with Terraform](#azure-deployment-with-terraform)
- [Configuration](#configuration)
- [Security](#security)
- [Monitoring](#monitoring)
- [Troubleshooting](#troubleshooting)

## Prerequisites

### Required Tools
- .NET 8.0 SDK
- Docker & Docker Compose
- kubectl
- Helm 3.x
- Terraform 1.5+
- Azure CLI (for Azure deployments)
- PostgreSQL 15+
- Redis 7+

## Local Development

### 1. Clone and Restore
```bash
git clone https://github.com/yasserebrahimi/MarketDataSystem.git
cd MarketDataSystem
dotnet restore
```

### 2. Configuration
Copy the example environment file:
```bash
cp .env.example .env
```

Edit `.env` with your local settings.

### 3. Start Infrastructure Services
```bash
docker-compose up -d postgres redis
```

### 4. Run Migrations
```bash
cd src/MarketData.API
dotnet ef database update --project ../MarketData.Infrastructure
```

### 5. Run the Application
```bash
dotnet run
```

The API will be available at: `https://localhost:5001`

### 6. Access Swagger UI
Navigate to: `https://localhost:5001/`

## Database Migration

### Create New Migration
```bash
cd src/MarketData.Infrastructure
dotnet ef migrations add MigrationName --startup-project ../MarketData.API
```

### Apply Migrations
```bash
dotnet ef database update --startup-project ../MarketData.API
```

### Rollback Migration
```bash
dotnet ef database update PreviousMigrationName --startup-project ../MarketData.API
```

## Docker Deployment

### Build Image
```bash
docker build -t marketdata:latest -f src/MarketData.API/Dockerfile .
```

### Run with Docker Compose
```bash
docker-compose up -d
```

Services will be available at:
- API: http://localhost:5000
- Swagger: http://localhost:5000
- PostgreSQL: localhost:5432
- Redis: localhost:6379
- Grafana: http://localhost:3000 (admin/admin)
- Prometheus: http://localhost:9090

## Kubernetes Deployment

### Using Raw Manifests

1. **Create namespace:**
```bash
kubectl create namespace marketdata-production
```

2. **Configure secrets:**
```bash
kubectl create secret generic marketdata-secrets \
  --from-literal=db-connection-string="Host=postgres;Port=5432;Database=marketdata;Username=postgres;Password=YOUR_PASSWORD" \
  --from-literal=redis-connection-string="redis:6379,password=YOUR_PASSWORD" \
  --from-literal=jwt-secret-key="YOUR_JWT_SECRET_KEY" \
  -n marketdata-production
```

3. **Deploy:**
```bash
kubectl apply -f k8s/base/ -n marketdata-production
```

4. **Check status:**
```bash
kubectl get all -n marketdata-production
kubectl logs -f deployment/marketdata-api -n marketdata-production
```

### Using Helm

1. **Add dependencies:**
```bash
helm dependency update charts/marketdata
```

2. **Install chart:**
```bash
helm install marketdata charts/marketdata \
  --namespace marketdata-production \
  --create-namespace \
  --values charts/marketdata/values-prod.yaml \
  --set secrets.jwtSecretKey="YOUR_JWT_SECRET" \
  --set postgresql.auth.password="YOUR_DB_PASSWORD"
```

3. **Upgrade:**
```bash
helm upgrade marketdata charts/marketdata \
  --namespace marketdata-production \
  --values charts/marketdata/values-prod.yaml
```

4. **Uninstall:**
```bash
helm uninstall marketdata -n marketdata-production
```

## Azure Deployment with Terraform

### 1. Prerequisites
```bash
# Login to Azure
az login

# Set subscription
az account set --subscription "YOUR_SUBSCRIPTION_ID"
```

### 2. Initialize Terraform
```bash
cd terraform
terraform init
```

### 3. Create terraform.tfvars
```bash
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars with your values
```

### 4. Plan Deployment
```bash
terraform plan -out=tfplan
```

### 5. Apply Infrastructure
```bash
terraform apply tfplan
```

### 6. Get AKS Credentials
```bash
az aks get-credentials \
  --resource-group marketdata-prod-rg \
  --name marketdata-prod-aks
```

### 7. Deploy Application to AKS
```bash
# Using Helm
helm install marketdata charts/marketdata \
  --namespace marketdata-production \
  --create-namespace \
  --set image.tag="v1.0.0" \
  --set postgresql.enabled=false \
  --set redis.enabled=false \
  --set secrets.dbConnectionString="$(terraform output -raw postgres_connection_string)" \
  --set secrets.redisConnectionString="$(terraform output -raw redis_connection_string)"
```

## Configuration

### Environment Variables

| Variable | Description | Required | Default |
|----------|-------------|----------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment (Development/Staging/Production) | Yes | Development |
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string | Yes | - |
| `ConnectionStrings__Redis` | Redis connection string | Yes | - |
| `JwtSettings__SecretKey` | JWT signing key (min 32 chars) | Yes | - |
| `JwtSettings__Issuer` | JWT issuer | No | MarketDataAPI |
| `JwtSettings__Audience` | JWT audience | No | MarketDataClients |
| `JwtSettings__AccessTokenExpirationMinutes` | Token lifetime in minutes | No | 60 |
| `IpRateLimiting__EnableEndpointRateLimiting` | Enable rate limiting | No | true |
| `MarketDataProcessing__UsePersistentRepositories` | Use database storage | No | false |

### appsettings Hierarchy
- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development overrides
- `appsettings.Staging.json` - Staging overrides
- `appsettings.Production.json` - Production overrides
- Environment variables - Highest priority

## Security

### 1. JWT Secret Key
Generate a secure key:
```bash
openssl rand -base64 32
```

### 2. Database Passwords
Use strong passwords and store in Azure Key Vault or Kubernetes Secrets.

### 3. SSL/TLS
- Enable HTTPS in production
- Use cert-manager for automatic certificate management in Kubernetes

### 4. Network Security
- Use Azure Network Security Groups
- Configure Kubernetes NetworkPolicies
- Enable WAF on Application Gateway

### 5. API Security
- Rate limiting enabled by default
- JWT authentication required for all endpoints except /auth/*
- Role-based authorization policies

## Monitoring

### Healthchecks
- `/health` - Overall health status
- `/health/ready` - Readiness probe

### Application Insights (Azure)
Configure in appsettings:
```json
{
  "ApplicationInsights": {
    "ConnectionString": "YOUR_CONNECTION_STRING"
  }
}
```

### Prometheus Metrics
Scrape endpoint: `http://[host]/metrics`

### Grafana Dashboards
Import dashboards from: `docs/grafana/`

## CI/CD

### GitHub Actions Workflows
- `.github/workflows/build.yml` - Build validation
- `.github/workflows/test.yml` - Run tests
- `.github/workflows/docker-build.yml` - Build and push Docker images
- `.github/workflows/code-quality.yml` - Code quality checks
- `.github/workflows/deploy.yml` - Deployment automation

### Required Secrets
Configure in GitHub repository settings:
- `DOCKER_USERNAME` - Docker Hub username
- `DOCKER_PASSWORD` - Docker Hub password
- `AZURE_CREDENTIALS_STAGING` - Azure credentials for staging
- `AZURE_CREDENTIALS_PROD` - Azure credentials for production
- `SNYK_TOKEN` - Snyk security scanning token

## Troubleshooting

### Common Issues

#### 1. Connection Refused to PostgreSQL
```bash
# Check if PostgreSQL is running
docker ps | grep postgres

# Check connection string format
Host=host;Port=5432;Database=db;Username=user;Password=pass
```

#### 2. Redis Connection Issues
```bash
# Test Redis connection
redis-cli -h localhost -p 6379 ping

# Check if authentication is required
redis-cli -h localhost -p 6379 -a YOUR_PASSWORD ping
```

#### 3. JWT Token Validation Errors
- Verify `JwtSettings__SecretKey` is set
- Check token expiration time
- Ensure Issuer and Audience match

#### 4. Migration Failures
```bash
# Drop and recreate database (DEVELOPMENT ONLY)
dotnet ef database drop -f --startup-project ../MarketData.API
dotnet ef database update --startup-project ../MarketData.API
```

#### 5. Kubernetes Pod CrashLoopBackOff
```bash
# Check logs
kubectl logs -f pod/marketdata-api-xxx -n marketdata-production

# Describe pod
kubectl describe pod marketdata-api-xxx -n marketdata-production

# Check events
kubectl get events -n marketdata-production --sort-by='.lastTimestamp'
```

### Debug Mode
Enable detailed logging:
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug"
    }
  }
}
```

## Performance Tuning

### Database
- Connection pooling is enabled by default
- Adjust `MaxPoolSize` in connection string for high load
- Create appropriate indexes (already defined in migrations)

### Redis
- Configure `maxmemory-policy` to `allkeys-lru`
- Monitor memory usage
- Use Redis Cluster for large datasets

### API
- Adjust HPA settings in Kubernetes for autoscaling
- Configure rate limiting based on load
- Enable response caching where appropriate

## Backup and Recovery

### Database Backup
```bash
# Backup
pg_dump -h localhost -U postgres -d marketdata > backup.sql

# Restore
psql -h localhost -U postgres -d marketdata < backup.sql
```

### Azure Automated Backups
Backups are automatic for:
- Azure PostgreSQL Flexible Server (7-day retention)
- Azure Redis Cache (daily)

## Support

For issues and questions:
- GitHub Issues: https://github.com/yasserebrahimi/MarketDataSystem/issues
- Email: yasser.ebrahimi@outlook.com

## License

MIT License - See LICENSE file for details
