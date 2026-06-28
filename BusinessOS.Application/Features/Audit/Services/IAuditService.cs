using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Audit.DTOs;

namespace BusinessOS.Application.Features.Audit.Services;

public interface IAuditService
{
    Task<PagedResult<AuditLogResponse>> GetAuditLogsAsync(
        string? action,
        string? entityType,
        string? userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
