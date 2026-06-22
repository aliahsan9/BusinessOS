using BusinessOS.Application.Common.Interfaces;

namespace BusinessOS.Infrastructure.MultiTenancy;

public class TenantDbConnection : ITenantDbConnection
{
    public string GetConnectionString(Guid tenantId)
    {
        return tenantId switch
        {
            var id when id != Guid.Empty =>
                "Server=DESKTOP-GVA6N7B\\SQLEXPRESS;Database=BusinessOSDb;Trusted_Connection=True;TrustServerCertificate=True",

            _ => throw new Exception("Invalid tenant")
        };
    }
}
