namespace BusinessOS.Application.Features.Audit.Services;

public interface IEntityAuditService
{
    Task LogChangeAsync(
        string entityType,
        Guid entityId,
        string action,
        object? oldValues,
        object? newValues,
        CancellationToken cancellationToken = default);
}
