using BusinessOS.Domain.Common;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class WorkTask : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public WorkTaskStatus Status { get; set; } = WorkTaskStatus.Todo;
    public string? AssignedUserId { get; set; }
    public int Priority { get; set; } = 1;
    public DateTime? DueDate { get; set; }

    public Project Project { get; set; } = default!;
}
