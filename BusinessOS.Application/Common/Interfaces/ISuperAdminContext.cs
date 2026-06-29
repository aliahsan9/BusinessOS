namespace BusinessOS.Application.Common.Interfaces;

public interface ISuperAdminContext
{
    bool IsSuperAdmin { get; }
    bool CanAccessAllTenants { get; }
}
