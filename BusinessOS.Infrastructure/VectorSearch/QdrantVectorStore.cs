using BusinessOS.Application.Features.VectorSearch.Models;
using BusinessOS.Application.Features.VectorSearch.Options;
using BusinessOS.Application.Features.VectorSearch.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using static Qdrant.Client.Grpc.Conditions;

namespace BusinessOS.Infrastructure.VectorSearch;

public sealed class QdrantVectorStore : IVectorStore
{
    private readonly QdrantClient _client;
    private readonly QdrantOptions _options;
    private readonly ILogger<QdrantVectorStore> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly SemaphoreSlim _ensureLock = new(1, 1);
    private bool _collectionReady;

    public QdrantVectorStore(
        QdrantClient client,
        IOptions<QdrantOptions> options,
        ILogger<QdrantVectorStore> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
        _retryPolicy = Policy
            .Handle<Exception>(ex => ex is not OperationCanceledException)
            .WaitAndRetryAsync(
                3,
                attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)),
                (ex, delay, attempt, _) =>
                {
                    _logger.LogWarning(
                        ex,
                        "Qdrant operation failed (attempt {Attempt}). Retrying in {Delay}.",
                        attempt,
                        delay);
                });
    }

    public async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || _collectionReady)
            return;

        await _ensureLock.WaitAsync(cancellationToken);
        try
        {
            if (_collectionReady)
                return;

            await _retryPolicy.ExecuteAsync(async () =>
            {
                var exists = await _client.CollectionExistsAsync(_options.CollectionName, cancellationToken);
                if (!exists)
                {
                    var distance = ParseDistance(_options.Distance);
                    await _client.CreateCollectionAsync(
                        _options.CollectionName,
                        new VectorParams
                        {
                            Size = (ulong)_options.VectorSize,
                            Distance = distance
                        },
                        cancellationToken: cancellationToken);

                    _logger.LogInformation(
                        "Created Qdrant collection {Collection} (size={Size}, distance={Distance})",
                        _options.CollectionName,
                        _options.VectorSize,
                        distance);
                }
            });

            _collectionReady = true;
        }
        finally
        {
            _ensureLock.Release();
        }
    }

    public async Task UpsertAsync(
        IReadOnlyList<(VectorPointDocument Document, float[] Embedding)> points,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || points.Count == 0)
            return;

        await EnsureCollectionExistsAsync(cancellationToken);

        var structs = points.Select(p =>
        {
            var point = new PointStruct
            {
                Id = p.Document.PointId,
                Vectors = p.Embedding
            };

            point.Payload["tenantId"] = p.Document.TenantId.ToString();
            point.Payload["entityType"] = p.Document.EntityType;
            point.Payload["entityId"] = p.Document.EntityId.ToString();
            point.Payload["chunkIndex"] = p.Document.ChunkIndex;
            point.Payload["title"] = p.Document.Title;
            if (!string.IsNullOrWhiteSpace(p.Document.Excerpt))
                point.Payload["excerpt"] = p.Document.Excerpt;
            point.Payload["text"] = p.Document.Text;

            foreach (var (key, value) in p.Document.Payload)
            {
                if (value is null)
                    continue;
                point.Payload[key] = ConvertPayloadValue(value);
            }

            return point;
        }).ToList();

        await _retryPolicy.ExecuteAsync(async () =>
        {
            await _client.UpsertAsync(_options.CollectionName, structs, cancellationToken: cancellationToken);
        });
    }

    public async Task DeleteByEntityAsync(
        Guid tenantId,
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return;

        await EnsureCollectionExistsAsync(cancellationToken);

        var filter = new Filter
        {
            Must =
            {
                MatchKeyword("tenantId", tenantId.ToString()),
                MatchKeyword("entityType", entityType),
                MatchKeyword("entityId", entityId.ToString())
            }
        };

        await _retryPolicy.ExecuteAsync(async () =>
        {
            await _client.DeleteAsync(_options.CollectionName, filter, cancellationToken: cancellationToken);
        });
    }

    public async Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
        float[] queryEmbedding,
        VectorSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return [];

        await EnsureCollectionExistsAsync(cancellationToken);

        var must = new List<Condition>
        {
            MatchKeyword("tenantId", request.TenantId.ToString())
        };

        if (!string.IsNullOrWhiteSpace(request.EntityType))
            must.Add(MatchKeyword("entityType", request.EntityType));

        if (request.MetadataFilters is not null)
        {
            foreach (var (key, value) in request.MetadataFilters)
            {
                if (value is null)
                    continue;
                must.Add(MatchKeyword(key, value.ToString() ?? string.Empty));
            }
        }

        var filter = new Filter { Must = { must } };

        var results = await _retryPolicy.ExecuteAsync(async () =>
            await _client.SearchAsync(
                _options.CollectionName,
                queryEmbedding,
                filter: filter,
                limit: (ulong)Math.Max(1, request.Top),
                scoreThreshold: request.ScoreThreshold,
                payloadSelector: true,
                cancellationToken: cancellationToken));

        return results.Select(MapHit).ToList();
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return true;

        try
        {
            await _client.ListCollectionsAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Qdrant health check failed");
            return false;
        }
    }

    private static VectorSearchHit MapHit(ScoredPoint scored)
    {
        var payload = scored.Payload;
        var tenantId = Guid.TryParse(GetString(payload, "tenantId"), out var tid) ? tid : Guid.Empty;
        var entityId = Guid.TryParse(GetString(payload, "entityId"), out var eid) ? eid : Guid.Empty;
        var chunkIndex = GetInt(payload, "chunkIndex");

        var extras = payload.ToDictionary(
            kv => kv.Key,
            kv => (object?)ValueToObject(kv.Value));

        return new VectorSearchHit
        {
            PointId = scored.Id.Uuid is { Length: > 0 } uuid && Guid.TryParse(uuid, out var pid)
                ? pid
                : Guid.Empty,
            TenantId = tenantId,
            EntityType = GetString(payload, "entityType") ?? string.Empty,
            EntityId = entityId,
            ChunkIndex = chunkIndex,
            Title = GetString(payload, "title") ?? string.Empty,
            Excerpt = GetString(payload, "excerpt") ?? GetString(payload, "text"),
            Score = scored.Score,
            Payload = extras
        };
    }

    private static string? GetString(IDictionary<string, Value> payload, string key)
        => payload.TryGetValue(key, out var value) ? ValueToObject(value)?.ToString() : null;

    private static int GetInt(IDictionary<string, Value> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value))
            return 0;
        return ValueToObject(value) switch
        {
            long l => (int)l,
            int i => i,
            double d => (int)d,
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => 0
        };
    }

    private static object? ValueToObject(Value value)
    {
        return value.KindCase switch
        {
            Value.KindOneofCase.StringValue => value.StringValue,
            Value.KindOneofCase.IntegerValue => value.IntegerValue,
            Value.KindOneofCase.DoubleValue => value.DoubleValue,
            Value.KindOneofCase.BoolValue => value.BoolValue,
            Value.KindOneofCase.NullValue => null,
            _ => value.ToString()
        };
    }

    private static Value ConvertPayloadValue(object value) => value switch
    {
        string s => s,
        bool b => b,
        int i => i,
        long l => l,
        float f => f,
        double d => d,
        Guid g => g.ToString(),
        Enum e => e.ToString(),
        _ => value.ToString() ?? string.Empty
    };

    private static Distance ParseDistance(string distance) =>
        distance.Equals("Euclid", StringComparison.OrdinalIgnoreCase) ? Distance.Euclid
        : distance.Equals("Dot", StringComparison.OrdinalIgnoreCase) ? Distance.Dot
        : Distance.Cosine;
}
