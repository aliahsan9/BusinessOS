using System.Text;
using System.Text.Json;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.DataImports.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.DataImports.Queries.GetFailedImportRows;

public sealed record GetFailedImportRowsQuery(Guid BatchId) : IRequest<(byte[] Content, string FileName)>;

public sealed class GetFailedImportRowsQueryHandler
    : IRequestHandler<GetFailedImportRowsQuery, (byte[] Content, string FileName)>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;

    public GetFailedImportRowsQueryHandler(IApplicationDbContext context, ICurrentUserService user)
    {
        _context = context;
        _user = user;
    }

    public async Task<(byte[] Content, string FileName)> Handle(
        GetFailedImportRowsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_user.HasPermission(PermissionCodes.SettingsView))
            throw new ForbiddenException("You do not have permission to download failed import rows.");

        var batch = await _context.DataImportBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.BatchId, cancellationToken)
            ?? throw new NotFoundException("Import batch was not found.");

        if (string.IsNullOrWhiteSpace(batch.FailedRowsJson))
            throw new BadRequestException("This import batch has no failed rows to download.");

        var failures = JsonSerializer.Deserialize<List<ImportRowFailure>>(batch.FailedRowsJson)
            ?? [];

        if (failures.Count == 0)
            throw new BadRequestException("This import batch has no failed rows to download.");

        var valueKeys = failures
            .SelectMany(f => f.Values.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k)
            .ToList();

        var sb = new StringBuilder();
        sb.Append("RowNumber,NaturalKey,Errors");
        foreach (var key in valueKeys)
            sb.Append(',').Append(Escape(key));
        sb.AppendLine();

        foreach (var failure in failures)
        {
            sb.Append(failure.RowNumber).Append(',');
            sb.Append(Escape(failure.NaturalKey)).Append(',');
            sb.Append(Escape(string.Join("; ", failure.Errors)));
            foreach (var key in valueKeys)
            {
                failure.Values.TryGetValue(key, out var value);
                sb.Append(',').Append(Escape(value ?? string.Empty));
            }

            sb.AppendLine();
        }

        var fileName = $"{batch.EntityType}-failed-rows-{batch.Id:N}.csv";
        return (Encoding.UTF8.GetBytes(sb.ToString()), fileName);
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
