using BusinessOS.Application.Features.VectorSearch.Models;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Features.VectorSearch.Services;

public interface IVectorStore
{
    Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(
        IReadOnlyList<(VectorPointDocument Document, float[] Embedding)> points,
        CancellationToken cancellationToken = default);

    Task DeleteByEntityAsync(
        Guid tenantId,
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
        float[] queryEmbedding,
        VectorSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}

public interface IEmbeddingGenerator
{
    Task<float[]> GenerateAsync(string text, CancellationToken cancellationToken = default);
    int VectorSize { get; }
}

public interface IVectorEntityProjector
{
    string EntityType { get; }
    Type ClrType { get; }
    bool CanHandle(Type clrType);
    IReadOnlyList<VectorPointDocument> BuildDocuments(object entity);
}

public interface IVectorEntityProjectorRegistry
{
    IReadOnlyList<IVectorEntityProjector> All { get; }
    IVectorEntityProjector? Resolve(Type clrType);
    IVectorEntityProjector? Resolve(string entityType);
    IReadOnlySet<Type> TrackedClrTypes { get; }
}

public interface IVectorSearchService
{
    Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
        VectorSearchRequest request,
        CancellationToken cancellationToken = default);
}

public interface IVectorSyncOutboxWriter
{
    void Enqueue(
        Guid tenantId,
        string entityType,
        Guid entityId,
        VectorSyncOperation operation,
        string? payloadJson = null);
}
