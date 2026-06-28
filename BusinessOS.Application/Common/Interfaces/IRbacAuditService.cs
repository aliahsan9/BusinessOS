namespace BusinessOS.Application.Common.Interfaces;

public interface IRbacAuditService
{
    Task LogAsync(
        string action,
        string entityType,
        string entityId,
        string? oldValue,
        string? newValue,
        CancellationToken cancellationToken = default);
}
