using BusinessOS.Application.Features.VectorSearch;
using BusinessOS.Application.Features.VectorSearch.Models;
using BusinessOS.Application.Features.VectorSearch.Options;
using BusinessOS.Application.Features.VectorSearch.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.VectorSearch;

public sealed class VectorSearchService : IVectorSearchService
{
    private readonly IVectorStore _store;
    private readonly IEmbeddingGenerator _embeddings;
    private readonly QdrantOptions _options;
    private readonly ILogger<VectorSearchService> _logger;

    public VectorSearchService(
        IVectorStore store,
        IEmbeddingGenerator embeddings,
        IOptions<QdrantOptions> options,
        ILogger<VectorSearchService> logger)
    {
        _store = store;
        _embeddings = embeddings;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
        VectorSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return [];

        try
        {
            var embedding = await _embeddings.GenerateAsync(request.Query, cancellationToken);
            var semanticHits = await _store.SearchAsync(embedding, request, cancellationToken);

            return ApplyHybridRescore(request.Query, semanticHits)
                .OrderByDescending(x => x.Score)
                .Take(Math.Max(1, request.Top))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vector search failed for tenant {TenantId}", request.TenantId);
            throw;
        }
    }

    private static IEnumerable<VectorSearchHit> ApplyHybridRescore(
        string query,
        IReadOnlyList<VectorSearchHit> hits)
    {
        var normalizedQuery = query.Trim().ToLowerInvariant();
        var keywords = VectorTextChunker.NormalizeKeywords(normalizedQuery)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var hit in hits)
        {
            var text = $"{hit.Title} {hit.Excerpt}".ToLowerInvariant();
            var keywordScore = 0.0;

            if (!string.IsNullOrWhiteSpace(normalizedQuery)
                && text.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            {
                keywordScore += 0.5;
            }

            foreach (var kw in keywords)
            {
                if (text.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    keywordScore += 0.15;
            }

            keywordScore = Math.Min(keywordScore, 1.0);
            var hybrid = (keywordScore * 0.4) + (hit.Score * 0.6);

            yield return new VectorSearchHit
            {
                PointId = hit.PointId,
                TenantId = hit.TenantId,
                EntityType = hit.EntityType,
                EntityId = hit.EntityId,
                ChunkIndex = hit.ChunkIndex,
                Title = hit.Title,
                Excerpt = hit.Excerpt,
                Score = hybrid,
                Payload = hit.Payload
            };
        }
    }
}
