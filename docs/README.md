# BusinessOS Backend Documentation Portal

Welcome to the comprehensive technical documentation for the **BusinessOS Backend API**—an enterprise-grade, multi-tenant Business Operating System built on **.NET 10**, **Clean Architecture**, **CQRS (MediatR)**, **Entity Framework Core (PostgreSQL)**, **Qdrant Vector Database**, and **AI Agent/RAG Frameworks**.

---

## 🏛 Architecture Overview

BusinessOS follows strict **Clean Architecture** and **Domain-Driven Design (DDD)** principles, separating concerns across distinct solution layers:

```mermaid
graph TD
    Client[Web / Mobile Client] -->|HTTPS / WSS| API[BusinessOS.API]
    
    subgraph API Layer ["API Layer (BusinessOS.API)"]
        Endpoints[Minimal API Endpoints]
        Middleware[Middleware Pipeline]
        Hubs[SignalR Hubs]
    end

    subgraph Application Layer ["Application Layer (BusinessOS.Application)"]
        MediatR[MediatR CQRS Pipeline]
        Commands[Commands & Queries]
        Behaviors[Pipeline Behaviors (Validation, Logging)]
        AppInterfaces[Service Interfaces]
    end

    subgraph Domain Layer ["Domain Layer (BusinessOS.Domain)"]
        Entities[Domain Entities]
        Enums[Domain Enums]
        Events[Domain Events]
        ValueObjects[Value Objects & Rules]
    end

    subgraph Infrastructure Layer ["Infrastructure Layer (BusinessOS.Infrastructure)"]
        Services[Service Implementations]
        AiAgent[AI Agent Framework]
        VectorStore[Qdrant Vector Store]
        IdentitySvc[Identity & JWT]
        Payments[Stripe, JazzCash, EasyPaisa]
        Tts[Neural Voice / Edge TTS]
    end

    subgraph Persistence Layer ["Persistence Layer (BusinessOS.Persistence)"]
        DbContext[ApplicationDbContext]
        EFConfig[EF Core Entity Configurations]
        Migrations[PostgreSQL Migrations]
    end

    API --> Application Layer
    API --> Middleware
    Endpoints --> MediatR
    MediatR --> AppInterfaces
    Infrastructure Layer --> AppInterfaces
    Persistence Layer --> DbContext
    DbContext --> Entities
    Infrastructure Layer --> ExternalAI[OpenAI / LLM Providers]
    Infrastructure Layer --> Qdrant[Qdrant Vector DB]
    Persistence Layer --> PostgreSQL[(PostgreSQL Database)]
```

---

## 📚 Complete Documentation Index

| Document | Topic | Description |
| :--- | :--- | :--- |
| 🏗 [Architecture.md](file:///d:/Business_OS/BusinessOS/docs/Architecture.md) | **System Architecture** | Clean Architecture layers, design patterns, and systemic principles. |
| 📁 [Folder-Structure.md](file:///d:/Business_OS/BusinessOS/docs/Folder-Structure.md) | **Folder Directory** | Complete catalog of solution directories, projects, and conventions. |
| 🔄 [Request-Lifecycle.md](file:///d:/Business_OS/BusinessOS/docs/Request-Lifecycle.md) | **Request Execution** | End-to-end trace from HTTP request through middleware, MediatR, and EF Core. |
| 🔑 [Authentication.md](file:///d:/Business_OS/BusinessOS/docs/Authentication.md) | **Identity & JWT** | User authentication, token issuance, refresh tokens, and password management. |
| 🛡 [Authorization.md](file:///d:/Business_OS/BusinessOS/docs/Authorization.md) | **RBAC & Policies** | Role-based and permission-based authorization policies and handlers. |
| 🔌 [Dependency-Injection.md](file:///d:/Business_OS/BusinessOS/docs/Dependency-Injection.md) | **IoC & Registrations** | Service lifetime management, container setup, and extension methods. |
| ⚙️ [Middleware.md](file:///d:/Business_OS/BusinessOS/docs/Middleware.md) | **HTTP Pipeline** | Exception handling, tenant resolution, correlation tracking, and logging. |
| ⚡️ [CQRS.md](file:///d:/Business_OS/BusinessOS/docs/CQRS.md) | **MediatR & CQRS** | Command/Query segregation, validation behaviors, and handler design. |
| 🤖 [AI-Agent.md](file:///d:/Business_OS/BusinessOS/docs/AI-Agent.md) | **AI Agent Framework** | Autonomous AI execution, tool invocation, planning, and persona management. |
| 🔍 [RAG.md](file:///d:/Business_OS/BusinessOS/docs/RAG.md) | **Retrieval Augmented Gen** | Document ingestion, chunking, embedding generation, context search, and generation. |
| 📝 [Prompt-Pipeline.md](file:///d:/Business_OS/BusinessOS/docs/Prompt-Pipeline.md) | **Prompt Engineering** | System prompts, dynamic context enrichment, and prompt building pipelines. |
| 📊 [Vector-Search.md](file:///d:/Business_OS/BusinessOS/docs/Vector-Search.md) | **Qdrant Vector Store** | Vector embeddings, outbox synchronization, similarity search, and projections. |
| 🗄 [Database.md](file:///d:/Business_OS/BusinessOS/docs/Database.md) | **PostgreSQL & EF Core** | Multi-tenant DbContext, query filters, auditing, soft delete, and migrations. |
| 🔗 [Entity-Relationships.md](file:///d:/Business_OS/BusinessOS/docs/Entity-Relationships.md) | **Entity Catalog & ERD** | Domain entity definitions, keys, navigational properties, and Mermaid ERDs. |
| 🛠 [Services.md](file:///d:/Business_OS/BusinessOS/docs/Services.md) | **Service Index** | Detailed documentation of all application & infrastructure services. |
| 📡 [Controllers.md](file:///d:/Business_OS/BusinessOS/docs/Controllers.md) | **API Endpoints** | Minimal API route catalog, request/response models, and endpoint security. |
| 🔀 [API-Flow.md](file:///d:/Business_OS/BusinessOS/docs/API-Flow.md) | **API Flowcharts** | End-to-end sequence diagrams for key API operations. |
| ⚙️ [Configuration.md](file:///d:/Business_OS/BusinessOS/docs/Configuration.md) | **Settings & Options** | `appsettings.json`, environment variables, and typed options options binding. |
| 📜 [Logging.md](file:///d:/Business_OS/BusinessOS/docs/Logging.md) | **Serilog & Audit** | Structured logging, correlation context, entity change tracking, and RBAC logs. |
| ⚠️ [Error-Handling.md](file:///d:/Business_OS/BusinessOS/docs/Error-Handling.md) | **Global Error Pipeline** | Exception handling, `ProblemDetails` responses, and domain error mapping. |
| 🐳 [Deployment.md](file:///d:/Business_OS/BusinessOS/docs/Deployment.md) | **Docker & Hosting** | Containerization, environment setup, database migrations, and production guides. |
| 💼 [Workflows.md](file:///d:/Business_OS/BusinessOS/docs/Workflows.md) | **Business Processes** | Invoicing, Billing, Onboarding, AI Chat, and Customer Lifecycle flows. |

---

## 🛠 Technology Stack

* **Framework**: .NET 10 Web API
* **Architecture**: Clean Architecture / CQRS with MediatR
* **Persistence**: Entity Framework Core 10, PostgreSQL (`Npgsql`)
* **Vector Storage**: Qdrant Vector Database
* **AI & LLM**: OpenAI API (`gpt-4o`, `text-embedding-3-small`), Custom Agent Framework
* **Voice**: Edge Neural TTS (`EdgeNeuralTtsService`)
* **Realtime**: ASP.NET Core SignalR
* **Authentication**: ASP.NET Core Identity, JWT Bearer Tokens
* **Multi-Tenancy**: Header / Subdomain Tenant Resolution with Automatic EF Core Global Filters
* **Payments**: Stripe, JazzCash, EasyPaisa
* **Document Generation**: QuestPDF & HTML/CSS rendering
* **Logging**: Serilog with File, Console, and Audit enrichment
* **Testing**: xUnit, Moq, FluentAssertions, Integration Test Suite

---

## 🚀 Key System Features

1. **Multi-Tenant Isolation**: Complete database-level isolation via global EF Core query filters on `TenantId`.
2. **AI Copilot & Autonomous Agents**: Context-aware agent runner capable of tool calling (querying inventory, financial summaries, generating invoices) and multi-turn planning.
3. **Outbox-Pattern Vector Sync**: Asynchronous, background vector ingestion using `VectorSyncOutboxInterceptor` and `VectorSyncBackgroundService`.
4. **Multi-Provider Payment Processing**: Native integrations for global (Stripe) and regional Pakistani payment gateways (JazzCash, EasyPaisa).
5. **Auditing & RBAC**: Automated entity mutation tracking (`EntityAuditLog`), RBAC modification tracking (`RbacAuditLog`), and Tenant administration tracking (`TenantAuditLog`).
