using System.Text.Json;
using System.Text.RegularExpressions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Copilot;

public sealed class AiVectorRagService : IAiVectorRagService
{
    private const int ChunkSize = 800;
    private const int ChunkOverlap = 100;

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly OpenAiEmbeddingClient _embeddings;
    private readonly ILogger<AiVectorRagService> _logger;

    public AiVectorRagService(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        OpenAiEmbeddingClient embeddings,
        ILogger<AiVectorRagService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _embeddings = embeddings;
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

        var chunks = ChunkText(content);
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunkContent = chunks[i];
            float[]? embedding = null;
            try
            {
                embedding = await _embeddings.GenerateEmbeddingAsync(chunkContent, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Embedding generation failed; storing keyword-only chunk");
            }

            _context.AiDocumentChunks.Add(new AiDocumentChunk
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DocumentId = document.Id,
                ChunkIndex = i,
                Content = chunkContent,
                EmbeddingJson = embedding is null ? null : JsonSerializer.Serialize(embedding),
                Keywords = ExtractKeywords(chunkContent),
                DocumentType = documentType,
                CreatedByUserId = userId,
                Tags = tags
            });
        }

        document.IsIndexed = true;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AiCitationDto>> SearchAsync(
        string query,
        string? documentType,
        int top,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId ?? throw new InvalidOperationException("Tenant is required.");
        var normalizedQuery = query.Trim().ToLowerInvariant();
        var queryKeywords = ExtractKeywords(normalizedQuery).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var chunksQuery = _context.AiDocumentChunks
            .AsNoTracking()
            .Include(c => c.Document)
            .Where(c => c.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(documentType))
            chunksQuery = chunksQuery.Where(c => c.DocumentType == documentType);

        var chunks = await chunksQuery.Take(500).ToListAsync(cancellationToken);
        if (chunks.Count == 0)
            return [];

        float[]? queryEmbedding = null;
        try
        {
            queryEmbedding = await _embeddings.GenerateEmbeddingAsync(query, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Query embedding unavailable; using keyword search only");
        }

        var scored = chunks
            .Select(chunk =>
            {
                var keywordScore = KeywordScore(chunk, queryKeywords, normalizedQuery);
                var vectorScore = queryEmbedding is not null && !string.IsNullOrWhiteSpace(chunk.EmbeddingJson)
                    ? CosineSimilarity(queryEmbedding, DeserializeEmbedding(chunk.EmbeddingJson))
                    : 0;
                var hybridScore = (keywordScore * 0.4) + (vectorScore * 0.6);
                return new { chunk, hybridScore };
            })
            .Where(x => x.hybridScore > 0.05)
            .OrderByDescending(x => x.hybridScore)
            .Take(top)
            .Select(x => new AiCitationDto
            {
                Title = x.chunk.Document.Title,
                DocumentType = x.chunk.DocumentType,
                SourceId = x.chunk.DocumentId.ToString(),
                Excerpt = Truncate(x.chunk.Content, 200),
                Score = Math.Round(x.hybridScore, 3)
            })
            .ToList();

        return scored;
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

    private static float[] DeserializeEmbedding(string json) =>
        JsonSerializer.Deserialize<float[]>(json) ?? [];

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length)
            return 0;

        double dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        if (magA == 0 || magB == 0)
            return 0;

        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }

    private static List<string> ChunkText(string content)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(content))
            return chunks;

        var text = content.Trim();
        for (var start = 0; start < text.Length; start += ChunkSize - ChunkOverlap)
        {
            var length = Math.Min(ChunkSize, text.Length - start);
            chunks.Add(text.Substring(start, length));
            if (start + length >= text.Length)
                break;
        }

        return chunks;
    }

    private static string ExtractKeywords(string text) =>
        Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9\s]", " ");

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";
}
