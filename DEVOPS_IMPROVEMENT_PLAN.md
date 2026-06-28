# BusinessOS — DevOps Improvement Plan

**Date:** June 28, 2026  
**DevOps Readiness Score:** 1.5 / 10  
**Verdict:** No production deployment capability exists  
**Auditor:** DevOps Engineer / Enterprise Solution Architect

---

## Current State

| Capability | Status | Detail |
|------------|:------:|--------|
| CI/CD Pipeline | ❌ | No GitHub Actions, Azure Pipelines, or any CI |
| Docker | ❌ | No Dockerfile or docker-compose |
| Environment Management | ⚠ | appsettings.json only; no staging/prod configs |
| Secrets Management | ❌ | JWT key hardcoded in appsettings.json |
| Health Checks | ⚠ | Custom `/api/system/health` requires auth |
| Logging | ⚠ | Serilog configured; no aggregation |
| Monitoring | ❌ | No Application Insights, Prometheus, or Grafana |
| Alerting | ❌ | No alert rules |
| Backups | ❌ | No backup strategy |
| Disaster Recovery | ❌ | No DR plan |
| Infrastructure as Code | ❌ | No Terraform/Bicep/Pulumi |
| Container Orchestration | ❌ | No Kubernetes/ECS/App Service config |

---

## Phase 1: CI/CD Pipeline (Week 1–2)

### 1.1 GitHub Actions — Backend CI

```yaml
# .github/workflows/backend-ci.yml
name: Backend CI

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Test with Coverage
        run: |
          dotnet test --no-build --configuration Release \
            --collect:"XPlat Code Coverage" \
            --settings coverlet.runsettings \
            --results-directory ./TestResults

      - name: Coverage Report
        uses: irongut/CodeCoverageSummary@v1.3.0
        with:
          filename: TestResults/**/coverage.cobertura.xml
          threshold: 80

      - name: Upload Coverage
        uses: codecov/codecov-action@v4
        with:
          files: TestResults/**/coverage.cobertura.xml
```

### 1.2 GitHub Actions — Frontend CI

```yaml
# .github/workflows/frontend-ci.yml
name: Frontend CI

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: BusinessOS.Web

    steps:
      - uses: actions/checkout@v4

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm'
          cache-dependency-path: BusinessOS.Web/package-lock.json

      - name: Install
        run: npm ci

      - name: Lint
        run: npm run lint --if-present

      - name: Unit Tests
        run: npm run test:ci

      - name: Build
        run: npm run build -- --configuration=production
```

### 1.3 GitHub Actions — E2E Tests

```yaml
# .github/workflows/e2e.yml
name: E2E Tests

on:
  push:
    branches: [main]
  schedule:
    - cron: '0 6 * * 1'  # Weekly Monday 6 AM

jobs:
  e2e:
    runs-on: ubuntu-latest
    services:
      sqlserver:
        image: mcr.microsoft.com/mssql/server:2022-latest
        env:
          ACCEPT_EULA: Y
          SA_PASSWORD: TestPassword123!
        ports:
          - 1433:1433

    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET + Node.js
      - name: Start API
        run: dotnet run --project BusinessOS.API
      - name: Run Playwright
        run: npx playwright test
        working-directory: BusinessOS.Web
```

### 1.4 Deployment Pipeline (Week 2)

```yaml
# .github/workflows/deploy.yml
name: Deploy to Production

on:
  push:
    tags: ['v*']

jobs:
  deploy:
    runs-on: ubuntu-latest
    environment: production
    steps:
      - uses: actions/checkout@v4
      - name: Build Docker images
      - name: Push to container registry
      - name: Deploy to Azure App Service / AWS ECS
      - name: Run database migrations
      - name: Smoke test health endpoint
```

---

## Phase 2: Containerization (Week 2–3)

### 2.1 Backend Dockerfile

```dockerfile
# BusinessOS.API/Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Packages.props", "Directory.Build.props", "./"]
COPY ["BusinessOS.API/", "BusinessOS.API/"]
COPY ["BusinessOS.Application/", "BusinessOS.Application/"]
COPY ["BusinessOS.Domain/", "BusinessOS.Domain/"]
COPY ["BusinessOS.Infrastructure/", "BusinessOS.Infrastructure/"]
RUN dotnet restore "BusinessOS.API/BusinessOS.API.csproj"
RUN dotnet build -c Release -o /app/build
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BusinessOS.API.dll"]
```

### 2.2 Frontend Dockerfile

```dockerfile
# BusinessOS.Web/Dockerfile
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build -- --configuration=production

FROM nginx:alpine
COPY --from=build /app/dist/business-os-web/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
```

### 2.3 Docker Compose (Development + Production)

```yaml
# docker-compose.yml
services:
  api:
    build: ./BusinessOS.API
    ports: ["8080:8080"]
    environment:
      - ConnectionStrings__DefaultConnection=Server=db;Database=BusinessOS;User=sa;Password=${DB_PASSWORD};TrustServerCertificate=True;Encrypt=True
      - Jwt__Key=${JWT_SECRET}
      - ASPNETCORE_ENVIRONMENT=Production
    depends_on:
      db:
        condition: service_healthy
      redis:
        condition: service_started

  web:
    build: ./BusinessOS.Web
    ports: ["80:80"]
    depends_on: [api]

  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: Y
      SA_PASSWORD: ${DB_PASSWORD}
    volumes: [sqldata:/var/opt/mssql]
    healthcheck:
      test: /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$$SA_PASSWORD" -Q "SELECT 1"
      interval: 10s
      retries: 5

  redis:
    image: redis:7-alpine
    ports: ["6379:6379"]

volumes:
  sqldata:
```

---

## Phase 3: Environment & Secrets Management (Week 3)

### 3.1 Environment Configuration

| Environment | Config Source | Database | Notes |
|-------------|--------------|----------|-------|
| Development | appsettings.Development.json + User Secrets | Local SQL Express | Current setup |
| Staging | Azure App Configuration / env vars | Azure SQL (staging) | Mirror production |
| Production | Azure Key Vault / env vars | Azure SQL (production) | No secrets in code |

### 3.2 Secrets to Externalize

| Secret | Current Location | Target |
|--------|-----------------|--------|
| JWT signing key | appsettings.json | Azure Key Vault / env var |
| Database connection string | appsettings.json | Azure Key Vault / env var |
| Stripe API key (future) | N/A | Azure Key Vault |
| Email service API key (future) | N/A | Azure Key Vault |

### 3.3 .NET User Secrets Setup

```bash
cd BusinessOS.API
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "<random-256-bit-key>"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<local-connection>"
```

Add to `.gitignore`:
```
appsettings.*.local.json
secrets.json
.env
.env.*
```

---

## Phase 4: Health Checks & Monitoring (Week 4)

### 4.1 ASP.NET Health Checks

```csharp
// Program.cs additions
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "database")
    .AddRedis(redisConnection, name: "redis");

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
}); // Unauthenticated — for load balancers

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

### 4.2 Monitoring Stack

| Component | Tool | Purpose |
|-----------|------|---------|
| APM | Application Insights / Datadog | Request tracing, dependencies |
| Logs | Serilog → Seq / Azure Log Analytics | Centralized logging |
| Metrics | Prometheus + Grafana | Custom business metrics |
| Uptime | UptimeRobot / Pingdom | External availability monitoring |
| Errors | Sentry / App Insights | Error tracking and alerting |

### 4.3 Alert Rules

| Alert | Condition | Severity | Channel |
|-------|-----------|:--------:|---------|
| API down | Health check fails 2× | Critical | PagerDuty / SMS |
| High error rate | > 5% 5xx in 5 min | Critical | Slack + email |
| Slow responses | p95 > 2s for 10 min | Warning | Slack |
| Database connection failures | > 3 in 1 min | Critical | PagerDuty |
| Disk space | > 85% on DB server | Warning | Email |
| Certificate expiry | < 14 days | Warning | Email |
| Failed deployment | CI/CD pipeline fails | High | Slack |

---

## Phase 5: Backups & Disaster Recovery (Week 5)

### 5.1 Backup Strategy

| Data | Method | Frequency | Retention |
|------|--------|-----------|-----------|
| SQL Server database | Automated backup (Azure SQL / native) | Daily full + hourly log | 30 days |
| Redis cache | RDB snapshots | Every 6 hours | 7 days |
| File storage (future) | Geo-redundant storage | Continuous | 90 days |
| Configuration | Git repository | Every commit | Indefinite |
| Secrets | Key Vault soft-delete | Continuous | 90 days |

### 5.2 Disaster Recovery Plan

| Scenario | RTO | RPO | Procedure |
|----------|:---:|:---:|-----------|
| Database corruption | 1 hour | 1 hour | Restore from latest backup |
| Region outage | 4 hours | 1 hour | Failover to secondary region |
| Application crash | 5 min | 0 | Auto-restart (container orchestrator) |
| Data breach | 24 hours | — | Rotate secrets, audit access, notify |
| Complete infrastructure loss | 8 hours | 1 hour | Redeploy from IaC + restore DB |

### 5.3 Recovery Testing

- Monthly: Restore database backup to staging and verify
- Quarterly: Full DR drill (failover to secondary region)
- Document runbooks for each scenario

---

## Phase 6: Infrastructure as Code (Week 6–8)

### Recommended: Azure (or AWS equivalent)

| Resource | Azure Service | IaC Tool |
|----------|--------------|----------|
| API hosting | Azure App Service / Container Apps | Bicep/Terraform |
| Frontend hosting | Azure Static Web Apps / CDN | Bicep/Terraform |
| Database | Azure SQL Database | Bicep/Terraform |
| Cache | Azure Cache for Redis | Bicep/Terraform |
| Secrets | Azure Key Vault | Bicep/Terraform |
| Storage | Azure Blob Storage | Bicep/Terraform |
| Monitoring | Application Insights | Bicep/Terraform |
| DNS | Azure DNS | Bicep/Terraform |
| SSL | Azure-managed certificates | Automatic |

---

## Phase 7: Production Checklist

| # | Item | Status |
|---|------|:------:|
| 1 | CI pipeline (build + test + coverage) | ❌ |
| 2 | CD pipeline (deploy on tag) | ❌ |
| 3 | Docker images for API + Web | ❌ |
| 4 | docker-compose for local dev | ❌ |
| 5 | Secrets in Key Vault / env vars | ❌ |
| 6 | Unauthenticated /health endpoint | ❌ |
| 7 | Application Insights / monitoring | ❌ |
| 8 | Centralized logging (Seq/Log Analytics) | ❌ |
| 9 | Alert rules configured | ❌ |
| 10 | Database backup automation | ❌ |
| 11 | DR plan documented | ❌ |
| 12 | Staging environment | ❌ |
| 13 | Production environment | ❌ |
| 14 | Custom domain + SSL | ❌ |
| 15 | CORS configured for production origin | ❌ |
| 16 | Rate limiting enabled | ❌ |
| 17 | Security headers middleware | ❌ |
| 18 | Dependabot / vulnerability scanning | ❌ |
| 19 | Runbooks for common incidents | ❌ |
| 20 | BusinessOS.Web in solution file | ❌ |

---

## Estimated Costs (Monthly, Production)

| Service | Tier | Est. Cost |
|---------|------|:---------:|
| Azure App Service (API) | B2 | $55 |
| Azure Static Web Apps | Standard | $9 |
| Azure SQL Database | S1 (20 DTU) | $30 |
| Azure Cache for Redis | C0 | $16 |
| Azure Key Vault | Standard | $5 |
| Application Insights | Pay-as-you-go | $10 |
| Azure Blob Storage | Hot | $5 |
| **Total (initial)** | | **~$130/mo** |

Scales with usage. At 1,000 tenants: estimate $500–800/mo. At 10,000 tenants: estimate $2,000–5,000/mo with read replicas and auto-scaling.

---

*DevOps plan complete. Cross-reference: [BUSINESSOS_AUDIT_REPORT.md](./BUSINESSOS_AUDIT_REPORT.md)*
