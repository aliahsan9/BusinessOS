namespace BusinessOS.Application.Features.VectorSearch.Models;

public sealed class VectorSearchRequest
{
    public required string Query { get; init; }
    public Guid TenantId { get; init; }
    public string? EntityType { get; init; }
    public IReadOnlyDictionary<string, object?>? MetadataFilters { get; init; }
    public int Top { get; init; } = 5;
    public float? ScoreThreshold { get; init; }
}
