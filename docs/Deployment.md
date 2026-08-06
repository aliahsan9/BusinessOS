# Deployment & DevOps Guide: BusinessOS Backend

## Purpose
This document provides a complete production deployment guide for BusinessOS, covering Docker containerization (`docker-compose.yml`), environment setup, database migrations execution, health check endpoints, and cloud hosting recommendations.

---

## Responsibilities
* Provide containerized multi-container orchestration for local development and staging environments.
* Guide execution of Entity Framework Core database migrations (`dotnet ef database update`).
* Expose system health checks (`/health`) covering PostgreSQL connectivity and Qdrant vector database health (`QdrantHealthCheck`).

---

## Architecture Overview

```mermaid
graph TD
    Client[Web / Mobile Clients] --> Ingress[Nginx / Reverse Proxy / Cloud Load Balancer]
    
    subgraph Docker Stack ["Docker Compose Stack"]
        Ingress --> WebAPI[BusinessOS.API Container (.NET 10 App)]
        WebAPI --> Postgres[(PostgreSQL Container :5432)]
        WebAPI --> Qdrant[(Qdrant Vector DB Container :6334)]
    end
```

---

## Docker Compose Manifest (`docker-compose.yml`)

```yaml
version: '3.8'

services:
  businessos-db:
    image: postgres:16-alpine
    container_name: businessos-postgres
    environment:
      POSTGRES_DB: BusinessOSDb
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: SecretPassword123!
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

  businessos-qdrant:
    image: qdrant/qdrant:v1.9.0
    container_name: businessos-qdrant
    ports:
      - "6333:6333"
      - "6334:6334"
    volumes:
      - qdrant_data:/qdrant/storage

volumes:
  postgres_data:
  qdrant_data:
```

---

## Deployment Steps

### 1. Database Migrations Execution
```bash
# Apply pending migrations using EF Core CLI
dotnet ef database update --project BusinessOS.Infrastructure --startup-project BusinessOS.API
```

### 2. Launch Local Environment via Docker Compose
```bash
docker-compose up -d
```

### 3. Build & Run API Server
```bash
dotnet run --project BusinessOS.API --configuration Release
```

---

## System Health Checks (`/health`)
BusinessOS exposes ASP.NET Core Health Checks at `/health`. It validates:
* **PostgreSQL Connection**: Tests SQL query responsiveness.
* **Qdrant Vector Store**: Executes gRPC ping via `QdrantHealthCheck`.

---

## Environment Variables Matrix for Production

```bash
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection="Host=prod-postgres;Database=BusinessOSDb;Username=app;Password=COMPLEX_PASSWORD"
Jwt__Key="COMPLEX_PRODUCTION_JWT_SECRET_KEY_MIN_256_BITS"
Jwt__Issuer="https://api.businessos.com"
Jwt__Audience="https://app.businessos.com"
Ai__OpenAiApiKey="sk-proj-YOUR_PRODUCTION_OPENAI_KEY"
Qdrant__Host="prod-qdrant.internal"
```

---

## Related Documents
* [Configuration.md](file:///d:/Business_OS/BusinessOS/docs/Configuration.md)
* [Architecture.md](file:///d:/Business_OS/BusinessOS/docs/Architecture.md)
