using BusinessOS.Application.Features.VectorSearch.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BusinessOS.Infrastructure.VectorSearch;

public sealed class QdrantHealthCheck : IHealthCheck
{
    private readonly IVectorStore _vectorStore;

    public QdrantHealthCheck(IVectorStore vectorStore)
    {
        _vectorStore = vectorStore;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var healthy = await _vectorStore.IsHealthyAsync(cancellationToken);
            return healthy
                ? HealthCheckResult.Healthy("Qdrant is reachable.")
                : HealthCheckResult.Degraded("Qdrant is unreachable. Semantic search is unavailable.");
        }
        catch (Exception)
        {
            return HealthCheckResult.Degraded("Qdrant is unreachable. Semantic search is unavailable.");
        }
    }
}
