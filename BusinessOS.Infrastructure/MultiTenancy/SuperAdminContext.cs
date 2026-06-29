using BusinessOS.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace BusinessOS.Infrastructure.MultiTenancy;

public sealed class SuperAdminContext : ISuperAdminContext
{
    public SuperAdminContext(IConfiguration configuration, ICurrentUserService currentUserService)
    {
        var superAdminEmails = configuration
            .GetSection("SuperAdmin:Emails")
            .Get<string[]>() ?? [];

        var email = currentUserService.Email ?? string.Empty;
        IsSuperAdmin = superAdminEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
        CanAccessAllTenants = IsSuperAdmin;
    }

    public bool IsSuperAdmin { get; }
    public bool CanAccessAllTenants { get; }
}
