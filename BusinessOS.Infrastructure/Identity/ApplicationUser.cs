using BusinessOS.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace BusinessOS.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public Guid TenantId { get; set; }

    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;

    public bool IsActive { get; set; } = true;
    public string? AvatarUrl { get; set; }
    public DateTime? LastActiveAt { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = default!;
}
