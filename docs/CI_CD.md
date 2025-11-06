# CI/CD Guide

Continuous Integration and Deployment workflows for MarketData.

---

## GitHub Actions Workflows

### 1. Build Workflow (`.github/workflows/build.yml`)

**Triggers:** Push to main, develop, claude/* branches; Pull requests

**Steps:**
- Checkout code
- Setup .NET 8.0
- Restore dependencies
- Build in Release mode
- Check for warnings
- Upload build artifacts

**Usage:**
```bash
# Automatically runs on push/PR
# Manual trigger: gh workflow run build.yml
```

### 2. Test Workflow (`.github/workflows/test.yml`)

**Triggers:** Push/PR to main, develop, claude/*

**Services:**
- PostgreSQL 15
- Redis 7

**Steps:**
- Run unit tests
- Run integration tests
- Generate code coverage
- Upload to Codecov
- Publish test results

**Usage:**
```bash
# View test results in Actions tab
# Coverage: https://codecov.io/gh/yasserebrahimi/MarketDataSystem
```

### 3. Docker Build (`.github/workflows/docker-build.yml`)

**Triggers:** Push to main/develop, version tags

**Steps:**
- Build multi-platform Docker images (amd64, arm64)
- Tag with branch name, SHA, and semantic version
- Push to Docker Hub
- Security scan with Trivy
- Upload scan results to GitHub Security

**Tags Generated:**
- `latest` (main branch)
- `develop` (develop branch)
- `v1.0.0` (version tags)
- `main-abc1234` (SHA)

### 4. Code Quality (`.github/workflows/code-quality.yml`)

**Steps:**
- Check code formatting
- Run .NET analyzers
- Snyk security scan
- CodeQL analysis

### 5. Deploy (`.github/workflows/deploy.yml`)

**Triggers:** Manual dispatch, version tags

**Environments:**
- staging
- production

**Steps:**
- Azure login
- Deploy to AKS
- Create GitHub release (production)

---

## Required Secrets

Configure in GitHub repository settings → Secrets:

| Secret | Description |
|--------|-------------|
| `DOCKER_USERNAME` | Docker Hub username |
| `DOCKER_PASSWORD` | Docker Hub password/token |
| `AZURE_CREDENTIALS_STAGING` | Azure service principal (staging) |
| `AZURE_CREDENTIALS_PROD` | Azure service principal (production) |
| `SNYK_TOKEN` | Snyk security scan token |

---

## Branch Strategy

```
main
├── develop
│   ├── feature/new-feature
│   └── claude/task-branch
└── hotfix/critical-fix
```

**Branches:**
- `main`: Production-ready code
- `develop`: Integration branch
- `feature/*`: Feature development
- `claude/*`: AI-assisted development
- `hotfix/*`: Production fixes

---

## Deployment Process

### Staging Deployment
1. Push to develop branch
2. Tests run automatically
3. Docker image built and pushed
4. Manual approval required
5. Deploy to AKS staging

### Production Deployment
1. Create version tag: `git tag v1.0.0`
2. Push tag: `git push origin v1.0.0`
3. Full CI/CD pipeline runs
4. Manual approval required
5. Deploy to AKS production
6. GitHub release created

---

## Local CI/CD Testing

### Act (Run GitHub Actions locally)
```bash
# Install act
brew install act  # macOS
# or download from: https://github.com/nektos/act

# Run workflow
act -j build
act -j test
```

---

**Last Updated:** 2025-11-06
