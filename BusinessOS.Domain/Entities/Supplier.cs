using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class Supplier : AuditableEntity
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string Address { get; set; } = default!;
}
