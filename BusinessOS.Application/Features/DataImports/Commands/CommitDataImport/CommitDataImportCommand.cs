using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.DataImports.Commands.PreviewDataImport;
using BusinessOS.Application.Features.DataImports.Models;
using BusinessOS.Application.Features.DataImports.Services;
using MediatR;

namespace BusinessOS.Application.Features.DataImports.Commands.CommitDataImport;

public sealed record CommitDataImportCommand(CommitDataImportRequest Request)
    : IRequest<DataImportCommitResponse>;

public sealed class CommitDataImportCommandHandler : IRequestHandler<CommitDataImportCommand, DataImportCommitResponse>
{
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserService _user;
    private readonly DataImportCommitService _commitService;

    public CommitDataImportCommandHandler(
        ICacheService cache,
        ITenantProvider tenantProvider,
        ICurrentUserService user,
        DataImportCommitService commitService)
    {
        _cache = cache;
        _tenantProvider = tenantProvider;
        _user = user;
        _commitService = commitService;
    }

    public async Task<DataImportCommitResponse> Handle(
        CommitDataImportCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (request.PreviewId == Guid.Empty)
            throw new BadRequestException("PreviewId is required.");

        var cacheKey = PreviewDataImportCommandHandler.BuildCacheKey(
            _tenantProvider.TenantId,
            request.PreviewId);

        var session = await _cache.GetAsync<DataImportPreviewSession>(cacheKey, cancellationToken);
        if (session is null)
            throw new BadRequestException("Import preview expired or was not found. Upload the file again.");

        DataImportAccess.EnsureCanAccess(session.EntityType, _user, write: true);

        var mapping = MergeMapping(session.ColumnMapping, request.ColumnMapping);

        var effectiveSession = new DataImportPreviewSession
        {
            PreviewId = session.PreviewId,
            TenantId = session.TenantId,
            EntityType = session.EntityType,
            FileName = session.FileName,
            Format = session.Format,
            ContentHash = session.ContentHash,
            CsvColumns = session.CsvColumns,
            ColumnMapping = mapping,
            RawRows = session.RawRows,
            RowFailures = session.RowFailures,
            ValidRows = session.ValidRows,
            InvalidRows = session.InvalidRows,
            CreateCount = session.CreateCount,
            UpdateCount = session.UpdateCount,
            DuplicateBatchDetected = session.DuplicateBatchDetected
        };

        var response = await _commitService.CommitAsync(
            effectiveSession,
            mapping,
            _user.UserId,
            request.ImportValidRowsOnly,
            cancellationToken);

        await _cache.RemoveAsync(cacheKey, cancellationToken);
        return response;
    }

    private static IReadOnlyDictionary<string, string?> MergeMapping(
        IReadOnlyDictionary<string, string?> sessionMapping,
        IReadOnlyDictionary<string, string?>? overrideMapping)
    {
        if (overrideMapping is null || overrideMapping.Count == 0)
            return sessionMapping;

        var merged = new Dictionary<string, string?>(sessionMapping, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in overrideMapping)
            merged[pair.Key] = pair.Value;

        return merged;
    }
}
