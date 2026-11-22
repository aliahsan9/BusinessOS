namespace BusinessOS.Application.Features.DataImports.Models;

public sealed record ImportFieldMappingDto(string FieldKey, string Label, bool Required, string? SourceColumn);

public sealed record ImportPreviewRowDto(
    int RowNumber,
    string Action,
    string NaturalKey,
    IReadOnlyList<string> Errors,
    IReadOnlyDictionary<string, string> Values);

public sealed record ImportPlanCapacityDto(
    string Resource,
    long CurrentUsage,
    long Maximum,
    long Remaining,
    int RowsInFile,
    int CreateCount,
    int RowsExceedingLimit,
    bool WithinLimit,
    string? UpgradeHint);

public sealed record ImportPreviewResponse(
    Guid PreviewId,
    string EntityType,
    string FileName,
    string Format,
    string ContentHash,
    IReadOnlyList<string> CsvColumns,
    IReadOnlyList<ImportFieldMappingDto> Fields,
    IReadOnlyDictionary<string, string?> SuggestedMapping,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    int CreateCount,
    int UpdateCount,
    IReadOnlyList<ImportPreviewRowDto> PreviewRows,
    ImportPlanCapacityDto? PlanCapacity,
    bool DuplicateBatchDetected);

public sealed record CommitDataImportRequest(
    Guid PreviewId,
    IReadOnlyDictionary<string, string?>? ColumnMapping,
    bool ImportValidRowsOnly = false);

public sealed record DataImportCommitResponse(
    Guid BatchId,
    string EntityType,
    string FileName,
    string Status,
    int TotalRows,
    int CreatedCount,
    int UpdatedCount,
    int SkippedCount,
    int ErrorCount,
    int FailedRowCount,
    bool IsIdempotentReplay,
    IReadOnlyList<string> Errors);

public sealed record DataImportHistoryItemDto(
    Guid Id,
    string EntityType,
    string FileName,
    string Status,
    int TotalRows,
    int CreatedCount,
    int UpdatedCount,
    int SkippedCount,
    int ErrorCount,
    DateTime CreatedAt,
    string? ImportedByUserId,
    string? ContentHash,
    bool HasFailedRows);

public sealed record ImportPreflightRequest(
    string EntityType,
    int CreateCount,
    int TotalRows);

public sealed record ImportPreflightResponse(
    string EntityType,
    string? Resource,
    long CurrentUsage,
    long Maximum,
    long Remaining,
    int CreateCount,
    int TotalRows,
    int RowsExceedingLimit,
    bool WithinLimit,
    string SubscriptionStatus,
    string PlanName,
    string? Message,
    string? UpgradeHint);

public sealed class DataImportPreviewSession
{
    public Guid PreviewId { get; init; }
    public Guid TenantId { get; init; }
    public string EntityType { get; init; } = default!;
    public string FileName { get; init; } = default!;
    public string Format { get; init; } = "csv";
    public string ContentHash { get; init; } = string.Empty;
    public IReadOnlyList<string> CsvColumns { get; init; } = [];
    public IReadOnlyDictionary<string, string?> ColumnMapping { get; init; } =
        new Dictionary<string, string?>();
    public IReadOnlyList<IReadOnlyDictionary<string, string>> RawRows { get; init; } = [];
    public IReadOnlyList<ImportRowFailure> RowFailures { get; set; } = [];
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public int CreateCount { get; set; }
    public int UpdateCount { get; set; }
    public bool DuplicateBatchDetected { get; set; }
}

public sealed record ImportRowFailure(
    int RowNumber,
    string NaturalKey,
    IReadOnlyList<string> Errors,
    IReadOnlyDictionary<string, string> Values);
