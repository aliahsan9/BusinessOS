namespace BusinessOS.Application.Common.Interfaces;

public interface ITenantRegistrationService
{
    Task<Guid> CreateTenantAsync(
        Guid tenantId,
        string businessName,
        string email,
        string ownerUserId,
        CancellationToken cancellationToken);
}
