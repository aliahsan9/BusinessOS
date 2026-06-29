namespace BusinessOS.Application.Features.Tenant.Services;

public interface ITenantAuditService
{
    Task LogAsync(
        Guid tenantId,
        string action,
        string? entityType = null,
        string? oldValue = null,
        string? newValue = null,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);
}
