using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Tenant.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class TenantAuditService : ITenantAuditService
{
    private readonly IDbContextFactory<BusinessOSDbContext> _dbContextFactory;
    private readonly ICurrentUserService _currentUserService;

    public TenantAuditService(
        IDbContextFactory<BusinessOSDbContext> dbContextFactory,
        ICurrentUserService currentUserService)
    {
        _dbContextFactory = dbContextFactory;
        _currentUserService = currentUserService;
    }

    public async Task LogAsync(
        Guid tenantId,
        string action,
        string? entityType = null,
        string? oldValue = null,
        string? newValue = null,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.TenantAuditLogs.Add(new TenantAuditLog
        {
            TenantId = tenantId,
            ActorUserId = actorUserId ?? _currentUserService.UserId,
            Action = action,
            EntityType = entityType,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
