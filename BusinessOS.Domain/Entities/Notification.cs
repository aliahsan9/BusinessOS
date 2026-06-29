using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class Notification : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = default!;

    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;

    public string Type { get; set; } = default!;
    public bool IsRead { get; set; } = false;
    public string? Link { get; set; }
    public string? CreatedBy { get; set; }
}
