namespace BusinessOS.Application.Features.VectorSearch.Models;

public sealed class VectorSearchHit
{
    public Guid PointId { get; init; }
    public Guid TenantId { get; init; }
    public string EntityType { get; init; } = default!;
    public Guid EntityId { get; init; }
    public int ChunkIndex { get; init; }
    public string Title { get; init; } = default!;
    public string? Excerpt { get; init; }
    public double Score { get; init; }
    public IReadOnlyDictionary<string, object?> Payload { get; init; }
        = new Dictionary<string, object?>();
}
