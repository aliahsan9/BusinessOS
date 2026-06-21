using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class AIConversation : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = default!;

    public string Prompt { get; set; } = default!;
    public string Response { get; set; } = default!;
}
