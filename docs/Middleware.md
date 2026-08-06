# Middleware Guide: BusinessOS Backend

## Purpose
This document provides a detailed technical overview of the HTTP Middleware Pipeline in BusinessOS, explaining request correlation, structured log enrichment, exception handling, authentication, multi-tenant resolution, and response transformation.

---

## Responsibilities
* Establish request correlation IDs (`X-Correlation-ID`) across asynchronous operations (`CorrelationIdMiddleware`).
* Enrich Serilog logging context with `TenantId`, `UserId`, `TraceId`, and HTTP request metadata (`SerilogEnrichmentMiddleware`).
* Intercept all uncaught domain and system exceptions, converting them into standard RFC 7807 `ProblemDetails` responses (`ExceptionHandlingMiddleware`).
* Extract tenant context from `X-Tenant-Id` header, subdomain, or authenticated claims (`TenantMiddleware`).

---

## How It Works
Middlewares run in a strict, sequential pipeline. Each middleware performs pre-processing, invokes `_next(context)`, and handles post-processing or exception catching.

```mermaid
graph LR
    Request[HTTP Request] --> M1[CorrelationIdMiddleware]
    M1 --> M2[SerilogEnrichmentMiddleware]
    M2 --> M3[ExceptionHandlingMiddleware]
    M3 --> Auth[UseAuthentication]
    Auth --> M4[TenantMiddleware]
    M4 --> Authz[UseAuthorization]
    Authz --> Endpoints[Minimal API Endpoints]
```

---

## Execution Flow

```mermaid
sequenceDiagram
    autonumber
    actor Client
    participant Corr as CorrelationIdMiddleware
    participant Seri as SerilogEnrichmentMiddleware
    participant Exc as ExceptionHandlingMiddleware
    participant Ten as TenantMiddleware
    participant End as API Endpoint

    Client->>Corr: HTTP GET /api/dashboard
    Corr->>Corr: Read/Generate X-Correlation-ID header
    Corr->>Seri: Invoke _next()
    Seri->>Seri: Push CorrelationId to Serilog LogContext
    Seri->>Exc: Invoke _next()
    Exc->>Ten: Invoke _next() inside try/catch block
    Ten->>Ten: Resolve TenantId from Header/Host -> TenantContextService
    Ten->>End: Invoke Endpoint Handler
    alt Exception Thrown in Endpoint
        End-->>Exc: Exception Bubbles Up
        Exc->>Exc: Map to ProblemDetails (e.g. 404/400/500)
        Exc-->>Client: Return JSON ProblemDetails Response
    else Successful Execution
        End-->>Ten: 200 OK Response
        Ten-->>Exc: Return Response
        Exc-->>Seri: Return Response
        Seri-->>Corr: Return Response
        Corr-->>Client: 200 OK + X-Correlation-ID Header
    end
```

---

## Middlewares Breakdown

### 1. `CorrelationIdMiddleware`
* **File**: `BusinessOS.API/Middleware/CorrelationIdMiddleware.cs`
* **Purpose**: Guarantees every request has an `X-Correlation-ID` header. If absent in incoming request headers, generates a new `Guid.NewGuid().ToString()`.
* **Header**: `X-Correlation-ID`

### 2. `SerilogEnrichmentMiddleware`
* **File**: `BusinessOS.API/Middleware/SerilogEnrichmentMiddleware.cs`
* **Purpose**: Pushes contextual properties (`CorrelationId`, `TenantId`, `UserId`, `ClientIp`) into Serilog's `LogContext.PushProperty()`.

### 3. `ExceptionHandlingMiddleware`
* **File**: `BusinessOS.API/Middleware/ExceptionHandlingMiddleware.cs`
* **Purpose**: Centralized exception handler mapping exception types to HTTP status codes:
  * `ValidationException` -> 400 Bad Request
  * `NotFoundException` -> 404 Not Found
  * `UnauthorizedAccessException` -> 401 Unauthorized
  * `ForbiddenException` -> 403 Forbidden
  * `DomainException` -> 422 Unprocessable Entity
  * System `Exception` -> 500 Internal Server Error

### 4. `TenantMiddleware`
* **File**: `BusinessOS.API/Middleware/TenantMiddleware.cs`
* **Purpose**: Extracts tenant identity from:
  1. Header: `X-Tenant-Id`
  2. Query string: `tenantId`
  3. Host subdomain: `[tenant].businessos.com`
  4. Authenticated JWT claim: `TenantId`
* Injects tenant details into `TenantContextService`.

---

## Dependencies
* **Microsoft.AspNetCore.Http**: `HttpContext`, `RequestDelegate`.
* **Serilog.Context**: `LogContext`.
* **BusinessOS.Application.Common.Exceptions**: Domain exception definitions.

---

## Used By
* ASP.NET Core HTTP execution pipeline in `BusinessOS.API/Program.cs`.

---

## Calls To
* `TenantContextService.SetTenant()`
* `JsonSerializer.SerializeAsync()`

---

## Important Classes
* `CorrelationIdMiddleware`
* `SerilogEnrichmentMiddleware`
* `ExceptionHandlingMiddleware`
* `TenantMiddleware`

---

## Important Interfaces
* `ITenantContext`
* `ILogger<T>`

---

## Important Methods
* `InvokeAsync(HttpContext context)`: Standard middleware invocation method.

---

## Configuration
Middleware order in `Program.cs`:
```csharp
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SerilogEnrichmentMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseAuthorization();
```

---

## Common Pitfalls
* **Swallowing Exceptions**: Writing `try {} catch {}` blocks in handlers without rethrowing prevents `ExceptionHandlingMiddleware` from capturing the failure and generating `ProblemDetails`.

---

## Future Improvements
* Add Request/Response Body logging middleware for non-binary requests in staging environments.

---

## Related Documents
* [Request-Lifecycle.md](file:///d:/Business_OS/BusinessOS/docs/Request-Lifecycle.md)
* [Error-Handling.md](file:///d:/Business_OS/BusinessOS/docs/Error-Handling.md)
