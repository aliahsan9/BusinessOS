using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class NalvaBusinessEvent : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string PayloadJson { get; set; } = "{}";
    public string? IdempotencyKey { get; set; }
    public string Status { get; set; } = "Pending";
    public int Attempt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string CorrelationId { get; set; } = default!;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string? LastError { get; set; }
    public int WorkflowsStarted { get; set; }
}

public class CompanyIntegrationConnection : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string ProviderKey { get; set; } = default!;
    public string Status { get; set; } = "Disconnected";
    public string ScopesJson { get; set; } = "[]";
    public string? AccessTokenProtected { get; set; }
    public string? RefreshTokenProtected { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
    public string? ExternalAccountId { get; set; }
    public string? ExternalAccountLabel { get; set; }
    public string? MetadataJson { get; set; }
    public string? WebhookSecretProtected { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public string? LastError { get; set; }
    public string OwnerUserId { get; set; } = default!;
}

public class IntegrationOAuthState : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string ProviderKey { get; set; } = default!;
    public string State { get; set; } = default!;
    public string CodeVerifier { get; set; } = default!;
    public string RedirectUri { get; set; } = default!;
    public string? MetadataJson { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string UserId { get; set; } = default!;
    public DateTime? ConsumedAt { get; set; }
}

public class IntegrationActivityLog : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid? ConnectionId { get; set; }
    public string ProviderKey { get; set; } = default!;
    public string Operation { get; set; } = default!;
    public bool Success { get; set; }
    public int DurationMs { get; set; }
    public string? ErrorRedacted { get; set; }
    public string? CorrelationId { get; set; }
}
