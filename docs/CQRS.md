# CQRS & MediatR Guide: BusinessOS Backend

## Purpose
This document explains the Command Query Responsibility Segregation (CQRS) architecture in BusinessOS using the **MediatR** in-process messaging pattern, detailing feature organization, command/query dispatching, validation pipeline behaviors, and handler implementation.

---

## Responsibilities
* Decouple HTTP endpoint routing from business logic execution.
* Separate read-only query operations from state-modifying command operations.
* Enforce request validation transparently before business handlers execute.
* Standardize input DTOs, return types (`Result<T>`), and exceptions.

---

## How It Works
BusinessOS organizes use cases inside `BusinessOS.Application/Features/[FeatureName]`. Each feature encapsulates its commands, queries, handlers, validators, and DTO mappings.

```mermaid
graph TD
    Endpoint[Minimal API Endpoint] -->|mediator.Send()| MediatR[MediatR Mediator Engine]

    subgraph Pipeline ["MediatR Pipeline Behaviors"]
        Logging[LoggingBehavior]
        Validation[ValidationBehavior]
    end

    MediatR --> Logging
    Logging --> Validation

    subgraph Handlers ["Feature Handlers"]
        CommandHandler[Command Handlers (Mutations)]
        QueryHandler[Query Handlers (Reads)]
    end

    Validation -->|If Valid| Handlers
    Validation -->|If Invalid| ValidationExc[Throw ValidationException]

    CommandHandler --> DB[(EF Core ApplicationDbContext)]
    QueryHandler --> DB
```

---

## Execution Flow

```mermaid
sequenceDiagram
    autonumber
    actor Client
    participant End as Endpoint (ProductEndpoints)
    participant Pipe as ValidationBehavior
    participant Hand as CreateProductCommandHandler
    participant DB as ApplicationDbContext

    Client->>End: POST /api/products { Name: "Laptop", Price: 1200 }
    End->>Pipe: _mediator.Send(new CreateProductCommand(dto))
    Pipe->>Pipe: Validate against CreateProductCommandValidator
    alt Validation Failure
        Pipe-->>End: Throw ValidationException
    else Validation Clean
        Pipe->>Hand: Handle(CreateProductCommand, cancellationToken)
        Hand->>DB: Add Product entity & SaveChangesAsync()
        DB-->>Hand: Saved Entity (Id = 101)
        Hand-->>End: Result<ProductDto>.Success(dto)
        End-->>Client: 201 Created { id: 101, name: "Laptop" }
    end
```

---

## Command vs. Query Pattern Comparison

| Aspect | Command | Query |
| :--- | :--- | :--- |
| **Intent** | Mutates application state (Create, Update, Delete). | Fetches data without side-effects (Read). |
| **Interface** | `IRequest<Result<TResponse>>` | `IRequest<Result<TResponse>>` |
| **Side Effects** | Modifies database records, emits events, sends emails. | None (Read-only query execution). |
| **EF Core Tracking** | Default change tracking (`AsTracking()`). | Disables tracking (`AsNoTracking()`) for max performance. |
| **Validation** | Strict domain input validation (`CreateInvoiceCommandValidator`). | Parameter bounds validation (e.g. `PageSize <= 100`). |

---

## MediatR Pipeline Behaviors

### 1. `LoggingBehavior<TRequest, TResponse>`
Measures execution time using `Stopwatch`. Logs warnings if execution exceeds 500ms ("Slow Request Warning").

### 2. `ValidationBehavior<TRequest, TResponse>`
Scans IoC container for registered FluentValidation `IValidator<TRequest>` instances. Executes validators asynchronously. If any errors occur, throws `ValidationException` containing structured validation failures.

---

## Feature Folder Structure Example
```
BusinessOS.Application/Features/Invoices/
├── Commands/
│   ├── CreateInvoice/
│   │   ├── CreateInvoiceCommand.cs
│   │   ├── CreateInvoiceCommandHandler.cs
│   │   └── CreateInvoiceCommandValidator.cs
│   └── UpdateInvoiceStatus/
├── Queries/
│   ├── GetInvoiceById/
│   │   ├── GetInvoiceByIdQuery.cs
│   │   └── GetInvoiceByIdQueryHandler.cs
│   └── GetInvoicesPaged/
└── Services/
    └── IInvoiceNumberGenerator.cs
```

---

## Dependencies
* **MediatR**: `IMediator`, `IRequest<T>`, `IRequestHandler<TRequest, TResponse>`.
* **FluentValidation**: `AbstractValidator<T>`.

---

## Used By
* Minimal API Endpoints in `BusinessOS.API/Endpoints`.

---

## Calls To
* `ApplicationDbContext` for persistence operations.
* Domain services and external providers.

---

## Important Classes
* `LoggingBehavior<TRequest, TResponse>`
* `ValidationBehavior<TRequest, TResponse>`

---

## Important Interfaces
* `IMediator`: Dispatcher contract.
* `IRequest<TResponse>`: Request message contract.
* `IRequestHandler<TRequest, TResponse>`: Handler implementation contract.

---

## Important Methods
* `IMediator.Send(IRequest<TResponse> request, CancellationToken ct)`

---

## Configuration
Registered in `BusinessOS.Application/DependencyInjection.cs`:
```csharp
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```

---

## Common Pitfalls
* **Fat Handlers**: Putting external API network calls or PDF rendering code directly inside command handlers instead of delegating to domain/infrastructure services.
* **Tracking Queries**: Forgetting `.AsNoTracking()` in query handlers leads to memory overhead in large paginated result sets.

---

## Future Improvements
* Add MediatR Notification handlers for async domain event handling (`IDomainEvent`).

---

## Related Documents
* [Architecture.md](file:///d:/Business_OS/BusinessOS/docs/Architecture.md)
* [Folder-Structure.md](file:///d:/Business_OS/BusinessOS/docs/Folder-Structure.md)
