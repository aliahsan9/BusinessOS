using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Application.Features.DataImports;
using BusinessOS.Application.Features.DataImports.Models;
using BusinessOS.Application.Features.DataImports.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.DataImports.Commands.PreviewDataImport;

public sealed record PreviewDataImportCommand(
    string EntityType,
    string FileName,
    byte[] FileContent) : IRequest<ImportPreviewResponse>;

public sealed class PreviewDataImportCommandHandler : IRequestHandler<PreviewDataImportCommand, ImportPreviewResponse>
{
    private static readonly TimeSpan PreviewTtl = TimeSpan.FromHours(2);

    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserService _user;
    private readonly ISubscriptionEntitlementService _entitlements;

    public PreviewDataImportCommandHandler(
        IApplicationDbContext context,
        ICacheService cache,
        ITenantProvider tenantProvider,
        ICurrentUserService user,
        ISubscriptionEntitlementService entitlements)
    {
        _context = context;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _user = user;
        _entitlements = entitlements;
    }

    public async Task<ImportPreviewResponse> Handle(
        PreviewDataImportCommand request,
        CancellationToken cancellationToken)
    {
        var entityType = DataImportEntityTypes.NormalizeOrThrow(request.EntityType);
        DataImportAccess.EnsureCanAccess(entityType, _user, write: true);

        var parsed = ImportFileParser.Parse(request.FileName, request.FileContent);

        if (parsed.Headers.Count == 0)
            throw new BadRequestException("File has no header row.");

        if (parsed.Rows.Count == 0)
            throw new BadRequestException("File contains no data rows.");

        var mapping = ImportColumnMapper.MapColumns(parsed.Headers, entityType);
        var previewId = Guid.NewGuid();

        var duplicateBatch = await _context.DataImportBatches
            .AsNoTracking()
            .AnyAsync(
                x => x.ContentHash == parsed.ContentHash
                     && x.EntityType == entityType
                     && (x.Status == "Completed" || x.Status == "IdempotentReplay"),
                cancellationToken);

        var session = new DataImportPreviewSession
        {
            PreviewId = previewId,
            TenantId = _tenantProvider.TenantId,
            EntityType = entityType,
            FileName = request.FileName,
            Format = parsed.Format,
            ContentHash = parsed.ContentHash,
            CsvColumns = parsed.Headers.ToList(),
            ColumnMapping = mapping,
            RawRows = parsed.Rows.ToList(),
            DuplicateBatchDetected = duplicateBatch
        };

        var response = await ImportPreviewBuilder.BuildAsync(session, _context, cancellationToken);

        session.ValidRows = response.ValidRows;
        session.InvalidRows = response.InvalidRows;
        session.CreateCount = response.CreateCount;
        session.UpdateCount = response.UpdateCount;
        session.RowFailures = response.PreviewRows
            .Where(r => r.Errors.Count > 0)
            .Select(r => new ImportRowFailure(r.RowNumber, r.NaturalKey, r.Errors, r.Values))
            .ToList();

        // Recompute failures for ALL rows (preview truncates display to 100)
        session.RowFailures = ImportPreviewBuilder.CollectFailures(session);

        var capacity = await ImportPlanCapacityEvaluator.EvaluateAsync(
            entityType,
            response.CreateCount,
            _entitlements,
            _tenantProvider.TenantId,
            cancellationToken);

        var cacheKey = BuildCacheKey(_tenantProvider.TenantId, previewId);
        await _cache.SetAsync(cacheKey, session, PreviewTtl, cancellationToken: cancellationToken);

        return response with
        {
            Format = parsed.Format,
            ContentHash = parsed.ContentHash,
            PlanCapacity = capacity,
            DuplicateBatchDetected = duplicateBatch
        };
    }

    internal static string BuildCacheKey(Guid tenantId, Guid previewId) =>
        $"data-import:{tenantId:N}:{previewId:N}";
}
