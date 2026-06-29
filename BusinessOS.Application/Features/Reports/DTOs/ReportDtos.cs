namespace BusinessOS.Application.Features.Reports.DTOs;

public sealed class ReportHistoryItemDto
{
    public Guid Id { get; init; }
    public string ReportName { get; init; } = default!;
    public string ReportType { get; init; } = default!;
    public string GeneratedBy { get; init; } = default!;
    public DateTime GeneratedAt { get; init; }
    public string FileType { get; init; } = default!;
    public string FileName { get; init; } = default!;
}

public sealed class ReportHistoryResponse
{
    public IReadOnlyList<ReportHistoryItemDto> Items { get; init; } = [];
}

public sealed class ReportGenerationResult
{
    public byte[] Content { get; init; } = [];
    public string FileName { get; init; } = default!;
    public string ContentType { get; init; } = "application/pdf";
    public Guid HistoryId { get; init; }
}

public sealed class ReportQueryParams
{
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? Period { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? ProjectId { get; init; }
}
