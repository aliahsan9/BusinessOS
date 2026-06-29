namespace BusinessOS.Application.Common.Interfaces;

public sealed record CreateTenantOptions(
    Guid TenantId,
    string BusinessName,
    string Email,
    string OwnerUserId,
    string Timezone = "UTC",
    string Currency = "USD",
    string Industry = "General");

public interface ITenantRegistrationService
{
    Task<Guid> CreateTenantAsync(
        CreateTenantOptions options,
        CancellationToken cancellationToken);
}
