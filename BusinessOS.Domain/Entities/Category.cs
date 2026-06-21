using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class Category : AuditableEntity
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = default!;
    public string? Description { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
