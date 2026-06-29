using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Audit.DTOs;
using BusinessOS.Application.Features.Audit.Services;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class AuditService : IAuditService
{
    private readonly IApplicationDbContext _context;

    public AuditService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<AuditLogResponse>> GetAuditLogsAsync(
        string? action,
        string? entityType,
        string? userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedPageSize) = PaginationParams.Normalize(page, pageSize);

        var query = _context.EntityAuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(action))
        {
            var normalizedAction = action.Trim();
            query = query.Where(x => x.Action == normalizedAction);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var normalizedEntityType = entityType.Trim();
            query = query.Where(x => x.EntityType == normalizedEntityType);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var normalizedUserId = userId.Trim();
            query = query.Where(x => x.ChangedBy == normalizedUserId);
        }

        if (dateFrom.HasValue)
        {
            var from = dateFrom.Value.ToUniversalTime();
            query = query.Where(x => x.ChangedAt >= from);
        }

        if (dateTo.HasValue)
        {
            var to = dateTo.Value.ToUniversalTime();
            query = query.Where(x => x.ChangedAt <= to);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.ChangedAt)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new AuditLogResponse
            {
                Id = x.Id,
                ActorUserId = x.ChangedBy,
                Action = x.Action,
                EntityType = x.EntityType,
                EntityId = x.EntityId,
                OldValue = x.OldValues,
                NewValue = x.NewValues,
                CreatedAt = x.ChangedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogResponse>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }
}
