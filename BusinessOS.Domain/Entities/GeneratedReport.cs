using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Stores metadata and content for generated PDF reports.
/// </summary>
public class GeneratedReport : AuditableEntity
{
    public Guid TenantId { get; set; }

    public string ReportName { get; set; } = default!;
    public string ReportType { get; set; } = default!;
    public string FileType { get; set; } = "application/pdf";
    public string FileName { get; set; } = default!;

    public string GeneratedByUserId { get; set; } = default!;
    public string GeneratedByName { get; set; } = default!;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>JSON parameters used to regenerate the report.</summary>
    public string? ParametersJson { get; set; }

    /// <summary>Optional entity id (invoice, customer, project).</summary>
    public Guid? EntityId { get; set; }

    public byte[]? FileContent { get; set; }
}
