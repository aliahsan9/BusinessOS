using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class AiCopilotAuditLog : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = default!;
    public Guid? SessionId { get; set; }
    public string Intent { get; set; } = default!;
    public string? UserMessage { get; set; }
    public string? ToolsUsedJson { get; set; }
    public string? RetrievedDocumentsJson { get; set; }
    public int ExecutionTimeMs { get; set; }
    public int? TokenUsage { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
