using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.DataImports.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.DataImports.Queries.GetDataImportHistory;

public sealed record GetDataImportHistoryQuery(int Limit = 25) : IRequest<IReadOnlyList<DataImportHistoryItemDto>>;

public sealed class GetDataImportHistoryQueryHandler
    : IRequestHandler<GetDataImportHistoryQuery, IReadOnlyList<DataImportHistoryItemDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;

    public GetDataImportHistoryQueryHandler(IApplicationDbContext context, ICurrentUserService user)
    {
        _context = context;
        _user = user;
    }

    public async Task<IReadOnlyList<DataImportHistoryItemDto>> Handle(
        GetDataImportHistoryQuery request,
        CancellationToken cancellationToken)
    {
        if (!_user.HasPermission(PermissionCodes.SettingsView))
            throw new ForbiddenException("You do not have permission to view import history.");

        var take = Math.Clamp(request.Limit <= 0 ? 25 : request.Limit, 1, 100);

        return await _context.DataImportBatches
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => new DataImportHistoryItemDto(
                x.Id,
                x.EntityType,
                x.FileName,
                x.Status,
                x.TotalRows,
                x.CreatedCount,
                x.UpdatedCount,
                x.SkippedCount,
                x.ErrorCount,
                x.CreatedAt,
                x.ImportedByUserId,
                x.ContentHash,
                x.FailedRowsJson != null && x.FailedRowsJson.Length > 2))
            .ToListAsync(cancellationToken);
    }
}
