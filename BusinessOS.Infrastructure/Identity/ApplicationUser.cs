using BusinessOS.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace BusinessOS.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public Guid TenantId { get; set; }

    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;

    public bool IsActive { get; set; } = true;

    // Navigation
    public Tenant Tenant { get; set; } = default!;
}
