using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class Customer : AuditableEntity
{
    public Guid TenantId { get; set; }

    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string PhoneNumber { get; set; } = default!;
    public string Address { get; set; } = default!;
    public string City { get; set; } = default!;
    public string Country { get; set; } = default!;
    public string PostalCode { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public string? AssignedUserId { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
