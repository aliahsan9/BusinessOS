using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class AIConversation : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = default!;
    public Guid? SessionId { get; set; }
    public AiConversationSession? Session { get; set; }

    public string Prompt { get; set; } = default!;
    public string Response { get; set; } = default!;
    public string? Intent { get; set; }
    public string? ToolsUsedJson { get; set; }
    public string? CitationsJson { get; set; }
    public int? TokenUsage { get; set; }
    public int? ExecutionTimeMs { get; set; }
}
