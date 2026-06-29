using BusinessOS.Domain.Common;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class Project : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Planning;
    public string? AssignedUserId { get; set; }
    public Guid? CustomerId { get; set; }

    public Customer? Customer { get; set; }
    public ICollection<WorkTask> Tasks { get; set; } = new List<WorkTask>();
}
