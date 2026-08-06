# API Interaction Flowcharts Guide: BusinessOS Backend

## Purpose
This document provides end-to-end visual sequence diagrams and flowcharts for primary API operations across BusinessOS, including Invoice Creation & PDF Generation, AI Assistant Multi-Tool Execution, and Stripe Payment Webhook Processing.

---

## 1. Invoice Creation & Vector Ingestion Flow

```mermaid
sequenceDiagram
    autonumber
    actor Client
    participant API as InvoiceEndpoints
    participant Med as MediatR Pipeline
    participant Hand as CreateInvoiceCommandHandler
    participant DB as BusinessOSDbContext (PostgreSQL)
    participant Interceptor as VectorSyncOutboxInterceptor
    participant Worker as VectorSyncBackgroundService
    participant Qdrant as QdrantVectorStore

    Client->>API: POST /api/invoices { customerId, items: [...] }
    API->>Med: Send(CreateInvoiceCommand)
    Med->>Med: Validate via FluentValidation
    Med->>Hand: Handle(CreateInvoiceCommand)
    Hand->>DB: Add Invoice Entity & SaveChangesAsync()
    DB->>Interceptor: Intercept SaveChanges
    Interceptor->>DB: Write VectorSyncOutboxMessage
    DB-->>Hand: Commit Transaction
    Hand-->>API: Result<InvoiceDto>.Success()
    API-->>Client: 201 Created { id: "inv_123" }

    async Background Vector Ingestion
        Worker->>DB: Poll Pending Outbox Messages
        Worker->>Worker: Project Invoice to Text String
        Worker->>Qdrant: Upsert Vector Point (TenantId, Text, Embeddings)
        Worker->>DB: Mark Outbox Status = Processed
    end
```

---

## 2. AI Copilot Chat & Tool Calling Flow

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant API as AiEndpoints
    participant ChatSvc as AiChatService
    participant Planner as AgentPlanner
    participant Tool as AgentBusinessTools
    participant LLM as OpenAiChatClient
    participant Reply as AiNaturalReplyBuilder

    User->>API: POST /api/ai/chat { message: "Generate inventory report and low stock POs" }
    API->>ChatSvc: ProcessMessageAsync(request)
    ChatSvc->>Planner: RequiresWorkflow(intent, message)
    Planner-->>ChatSvc: Returns Plan: [GetLowStock, GenerateInventoryReport]

    loop Execute Planned Tools
        ChatSvc->>Tool: Execute GetLowStock()
        Tool-->>ChatSvc: Return Low Stock Entity Array
        ChatSvc->>Tool: Execute GenerateInventoryReport()
        Tool-->>ChatSvc: Return PDF File Metadata
    end

    ChatSvc->>LLM: Send System Prompt + Message + Tool Results
    LLM-->>ChatSvc: Synthesized Executive Summary
    ChatSvc->>Reply: Format Markdown & Generate Neural TTS Audio
    Reply-->>API: Return AiChatResponse
    API-->>User: 200 OK { text, audioUrl, sources }
```

---

## 3. Stripe Payment Webhook Processing Flow

```mermaid
sequenceDiagram
    autonumber
    actor Stripe as Stripe Gateway
    participant Endpoint as BillingEndpoints
    participant WebhookSvc as BillingWebhookService
    participant DB as BusinessOSDbContext
    participant TenantSvc as TenantService

    Stripe->>Endpoint: POST /api/billing/webhook (Signature Header)
    Endpoint->>WebhookSvc: ProcessStripeWebhookAsync(payload, signature)
    WebhookSvc->>WebhookSvc: Verify Stripe Event Signature
    alt Event = invoice.payment_succeeded
        WebhookSvc->>DB: Query TenantSubscription by CustomerId
        WebhookSvc->>TenantSvc: Extend Subscription Expiry Date (+1 Month)
        WebhookSvc->>DB: Add BillingInvoice & BillingTransaction (Status = Paid)
        DB-->>WebhookSvc: Save Changes
        WebhookSvc-->>Endpoint: Success
        Endpoint-->>Stripe: HTTP 200 OK
    else Invalid Signature / Failure
        WebhookSvc-->>Endpoint: Throw Exception
        Endpoint-->>Stripe: HTTP 400 Bad Request
    end
```

---

## Related Documents
* [Controllers.md](file:///d:/Business_OS/BusinessOS/docs/Controllers.md)
* [Request-Lifecycle.md](file:///d:/Business_OS/BusinessOS/docs/Request-Lifecycle.md)
* [AI-Agent.md](file:///d:/Business_OS/BusinessOS/docs/AI-Agent.md)
