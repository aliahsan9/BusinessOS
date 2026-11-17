using BusinessOS.Domain.Common;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class Project : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string? Tags { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Planning;
    public string? AssignedUserId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? Code { get; set; }
    public string? ManagerUserId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? Deadline { get; set; }
    public decimal Budget { get; set; }
    public int Priority { get; set; } = 2;
    public string Visibility { get; set; } = "Internal";
    public Guid? DealId { get; set; }
    public string Workflow { get; set; } = "General";

    public Customer? Customer { get; set; }
    public ICollection<WorkTask> Tasks { get; set; } = new List<WorkTask>();
}
