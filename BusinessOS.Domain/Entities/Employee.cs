using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class Employee : AuditableEntity
{
    public Guid TenantId { get; set; }

    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;

    public string Email { get; set; } = default!;
    public string Phone { get; set; } = default!;

    public string Designation { get; set; } = default!;

    public decimal Salary { get; set; }

    public DateTime JoiningDate { get; set; }

    public bool IsActive { get; set; } = true;
}
