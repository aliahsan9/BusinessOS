using System.Text.Json;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Audit.Services;
using BusinessOS.Domain.Entities;

namespace BusinessOS.Infrastructure.Services;

public sealed class EntityAuditService : IEntityAuditService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public EntityAuditService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task LogChangeAsync(
        string entityType,
        Guid entityId,
        string action,
        object? oldValues,
        object? newValues,
        CancellationToken cancellationToken = default)
    {
        var changedBy = _currentUserService.UserId ?? "system";

        _context.EntityAuditLogs.Add(new EntityAuditLog
        {
            Id = Guid.NewGuid(),
            ChangedBy = changedBy,
            EntityType = entityType.Trim(),
            EntityId = entityId.ToString(),
            Action = action.Trim(),
            OldValues = Truncate(Serialize(oldValues)),
            NewValues = Truncate(Serialize(newValues)),
            ChangedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    public static string Serialize(object? value) =>
        value is null ? string.Empty : JsonSerializer.Serialize(value);

    private static string? Truncate(string? value) =>
        value is null ? null : value.Length <= 8000 ? value : value[..8000];
}
