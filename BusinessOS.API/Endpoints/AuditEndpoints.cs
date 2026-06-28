using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Audit.DTOs;
using BusinessOS.Application.Features.Audit.Services;
using BusinessOS.API.Authorization;

namespace BusinessOS.API.Endpoints;

public static class AuditEndpoints
{
    public static void MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit-logs")
            .WithTags("Audit")
            .RequireAuthorization();

        group.MapGet("", GetAuditLogs)
            .RequirePermission(PermissionCodes.AuditView)
            .WithName("GetAuditLogs")
            .Produces<PagedResult<AuditLogResponse>>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> GetAuditLogs(
        string? action,
        string? entityType,
        string? userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page,
        int pageSize,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await auditService.GetAuditLogsAsync(
            action, entityType, userId, dateFrom, dateTo, page, pageSize, cancellationToken);

        return Results.Ok(result);
    }
}
