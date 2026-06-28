using BusinessOS.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace BusinessOS.Infrastructure.MultiTenancy;

public class TenantDbConnection : ITenantDbConnection
{
    private readonly IConfiguration _configuration;

    public TenantDbConnection(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GetConnectionString(Guid tenantId) =>
        _configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection is not configured.");
}
