namespace BusinessOS.Application.Common.Interfaces;

public interface ITenantProvider
{
    Guid GetTenantId();
    void SetTenantId(Guid tenantId);
}
