using System.Text.RegularExpressions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Application.Features.VectorSearch;
using BusinessOS.Application.Features.VectorSearch.Models;
using BusinessOS.Application.Features.VectorSearch.Options;
using BusinessOS.Application.Features.VectorSearch.Services;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.AI.Copilot;

public sealed class AiVectorRagService : IAiVectorRagService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IVectorSearchService _vectorSearch;
    private readonly QdrantOptions _qdrantOptions;
    private readonly ILogger<AiVectorRagService> _logger;

    public AiVectorRagService(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IVectorSearchService vectorSearch,
        IOptions<QdrantOptions> qdrantOptions,
        ILogger<AiVectorRagService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _vectorSearch = vectorSearch;
        _qdrantOptions = qdrantOptions.Value;
        _logger = logger;
    }

    public async Task IndexDocumentAsync(
        string title,
        string documentType,
        string content,
        string? tags,
        string? sourceEntityType,
        Guid? sourceEntityId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId ?? throw new InvalidOperationException("Tenant is required.");
        var userId = _currentUser.UserId ?? "system";

        var document = new AiDocument
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title,
            DocumentType = documentType,
            Content = content,
            Tags = tags,
            SourceEntityType = sourceEntityType,
            SourceEntityId = sourceEntityId,
            CreatedByUserId = userId,
            IsIndexed = false
        };

        _context.AiDocuments.Add(document);

        var chunks = VectorTextChunker.Chunk(content);
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunkContent = chunks[i];
            _context.AiDocumentChunks.Add(new AiDocumentChunk
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DocumentId = document.Id,
                ChunkIndex = i,
                Content = chunkContent,
                EmbeddingJson = null,
                Keywords = VectorTextChunker.NormalizeKeywords(chunkContent),
                DocumentType = documentType,
                CreatedByUserId = userId,
                Tags = tags
            });
        }

        document.IsIndexed = true;
        // Outbox interceptor enqueues Qdrant upsert for AiDocument.
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AiCitationDto>> SearchAsync(
        string query,
        string? documentType,
        int top,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId ?? throw new InvalidOperationException("Tenant is required.");

        if (_qdrantOptions.Enabled)
        {
            try
            {
                var filters = new Dictionary<string, object?>();
                if (!string.IsNullOrWhiteSpace(documentType))
                    filters["documentType"] = documentType;

                var hits = await _vectorSearch.SearchAsync(new VectorSearchRequest
                {
                    Query = query,
                    TenantId = tenantId,
                    EntityType = null,
                    MetadataFilters = filters.Count > 0 ? filters : null,
                    Top = Math.Max(top, 1),
                    ScoreThreshold = 0.25f
                }, cancellationToken);

                if (hits.Count > 0)
                {
                    return hits
                        .Take(top)
                        .Select(hit => new AiCitationDto
                        {
                            Title = hit.Title,
                            DocumentType = hit.Payload.TryGetValue("documentType", out var dt)
                                ? dt?.ToString() ?? hit.EntityType
                                : hit.EntityType,
                            SourceId = hit.EntityId.ToString(),
                            Excerpt = VectorTextChunker.Truncate(hit.Excerpt ?? string.Empty, 200),
                            Score = Math.Round(hit.Score, 3)
                        })
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Qdrant search failed; falling back to SQL keyword search");
            }
        }

        return await SearchSqlKeywordFallbackAsync(query, documentType, top, tenantId, cancellationToken);
    }

    private async Task<IReadOnlyList<AiCitationDto>> SearchSqlKeywordFallbackAsync(
        string query,
        string? documentType,
        int top,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var normalizedQuery = query.Trim().ToLowerInvariant();
        var queryKeywords = VectorTextChunker.NormalizeKeywords(normalizedQuery)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var chunksQuery = _context.AiDocumentChunks
            .AsNoTracking()
            .Include(c => c.Document)
            .Where(c => c.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(documentType))
            chunksQuery = chunksQuery.Where(c => c.DocumentType == documentType);

        var chunks = await chunksQuery.Take(500).ToListAsync(cancellationToken);
        if (chunks.Count == 0)
            return [];

        return chunks
            .Select(chunk => new
            {
                chunk,
                hybridScore = KeywordScore(chunk, queryKeywords, normalizedQuery)
            })
            .Where(x => x.hybridScore > 0.2)
            .OrderByDescending(x => x.hybridScore)
            .Take(top)
            .Select(x => new AiCitationDto
            {
                Title = x.chunk.Document.Title,
                DocumentType = x.chunk.DocumentType,
                SourceId = x.chunk.DocumentId.ToString(),
                Excerpt = VectorTextChunker.Truncate(x.chunk.Content, 200),
                Score = Math.Round(x.hybridScore, 3)
            })
            .ToList();
    }

    private static double KeywordScore(AiDocumentChunk chunk, string[] queryKeywords, string normalizedQuery)
    {
        var content = chunk.Content.ToLowerInvariant();
        var keywords = chunk.Keywords?.ToLowerInvariant() ?? string.Empty;
        var score = 0.0;

        if (content.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            score += 0.5;

        foreach (var kw in queryKeywords)
        {
            if (content.Contains(kw, StringComparison.OrdinalIgnoreCase) || keywords.Contains(kw, StringComparison.OrdinalIgnoreCase))
                score += 0.15;
        }

        return Math.Min(score, 1.0);
    }
}
