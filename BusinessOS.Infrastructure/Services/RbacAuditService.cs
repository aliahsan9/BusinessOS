using System.Text.Json;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;

namespace BusinessOS.Infrastructure.Services;

public sealed class RbacAuditService : IRbacAuditService
{
    private readonly BusinessOSDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public RbacAuditService(BusinessOSDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task LogAsync(
        string action,
        string entityType,
        string entityId,
        string? oldValue,
        string? newValue,
        CancellationToken cancellationToken = default)
    {
        var actorUserId = _currentUserService.UserId ?? "system";

        _context.RbacAuditLogs.Add(new RbacAuditLog
        {
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValue = Truncate(oldValue),
            NewValue = Truncate(newValue),
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    public static string Serialize(object? value) =>
        value is null ? string.Empty : JsonSerializer.Serialize(value);

    private static string? Truncate(string? value) =>
        value is null ? null : value.Length <= 4000 ? value : value[..4000];
}
