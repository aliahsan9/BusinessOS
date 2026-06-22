namespace BusinessOS.Application.Common.Interfaces;

public interface ITenantDbConnection
{
    string GetConnectionString(Guid tenantId);
}
