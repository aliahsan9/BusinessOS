using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Records a completed bulk CSV/XLSX import for tenant history and auditing.
/// </summary>
public class DataImportBatch : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string EntityType { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string Status { get; set; } = "Completed";
    public int TotalRows { get; set; }
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public string? ImportedByUserId { get; set; }
    public string? ErrorSummaryJson { get; set; }
    /// <summary>SHA-256 hex of uploaded file bytes for idempotent replay detection.</summary>
    public string? ContentHash { get; set; }
    /// <summary>JSON array of failed rows with errors (for download / retry).</summary>
    public string? FailedRowsJson { get; set; }
}