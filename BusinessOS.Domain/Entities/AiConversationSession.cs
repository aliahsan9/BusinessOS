using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class AiConversationSession : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = default!;
    public string Title { get; set; } = "New conversation";
    public string? CurrentPage { get; set; }
    public Guid? SelectedCustomerId { get; set; }
    public Guid? SelectedProjectId { get; set; }
    public Guid? SelectedOrderId { get; set; }
    public Guid? SelectedInvoiceId { get; set; }
    public string? MemoryJson { get; set; }
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public ICollection<AIConversation> Messages { get; set; } = [];
}
