using BusinessOS.Application.Features.VectorSearch.Models;
using BusinessOS.Application.Features.VectorSearch.Options;
using BusinessOS.Application.Features.VectorSearch.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.VectorSearch;

public sealed class VectorSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly VectorSyncOptions _options;
    private readonly QdrantOptions _qdrantOptions;
    private readonly ILogger<VectorSyncBackgroundService> _logger;

    public VectorSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<VectorSyncOptions> options,
        IOptions<QdrantOptions> qdrantOptions,
        ILogger<VectorSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _qdrantOptions = qdrantOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_qdrantOptions.Enabled)
        {
            _logger.LogInformation("Vector sync worker disabled (Qdrant:Enabled=false).");
            return;
        }

        _logger.LogInformation("Vector sync worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in vector sync worker.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds)), stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
        var store = scope.ServiceProvider.GetRequiredService<IVectorStore>();
        var embeddings = scope.ServiceProvider.GetRequiredService<IEmbeddingGenerator>();
        var registry = scope.ServiceProvider.GetRequiredService<IVectorEntityProjectorRegistry>();

        var now = DateTime.UtcNow;
        var staleBefore = now.AddMinutes(-5);

        var stale = await db.VectorSyncOutboxMessages
            .Where(x =>
                x.Status == VectorSyncStatus.Processing
                && x.NextAttemptAt != null
                && x.NextAttemptAt < staleBefore)
            .ToListAsync(cancellationToken);
        foreach (var message in stale)
        {
            message.Status = VectorSyncStatus.Failed;
            message.NextAttemptAt = now;
            message.LastError = "Reclaimed stale processing message.";
        }

        if (stale.Count > 0)
            await db.SaveChangesAsync(cancellationToken);

        var batch = await db.VectorSyncOutboxMessages
            .Where(x =>
                (x.Status == VectorSyncStatus.Pending || x.Status == VectorSyncStatus.Failed)
                && (x.NextAttemptAt == null || x.NextAttemptAt <= now))
            .OrderBy(x => x.CreatedAt)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        if (batch.Count == 0)
            return;

        foreach (var message in batch)
        {
            message.Status = VectorSyncStatus.Processing;
            message.NextAttemptAt = now.AddMinutes(10);
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var message in batch)
        {
            try
            {
                await ProcessMessageAsync(db, store, embeddings, registry, message, cancellationToken);
                message.Status = VectorSyncStatus.Succeeded;
                message.ProcessedAt = DateTime.UtcNow;
                message.LastError = null;
            }
            catch (Exception ex)
            {
                message.AttemptCount++;
                message.LastError = Truncate(ex.Message, 2000);
                message.NextAttemptAt = DateTime.UtcNow.AddSeconds(Math.Pow(2, Math.Min(message.AttemptCount, 8)));

                if (message.AttemptCount >= _options.MaxAttempts)
                {
                    message.Status = VectorSyncStatus.Dead;
                    _logger.LogError(
                        ex,
                        "Vector sync message {MessageId} dead-lettered after {Attempts} attempts ({EntityType}/{EntityId})",
                        message.Id,
                        message.AttemptCount,
                        message.EntityType,
                        message.EntityId);
                }
                else
                {
                    message.Status = VectorSyncStatus.Failed;
                    _logger.LogWarning(
                        ex,
                        "Vector sync message {MessageId} failed (attempt {Attempt})",
                        message.Id,
                        message.AttemptCount);
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task ProcessMessageAsync(
        BusinessOSDbContext db,
        IVectorStore store,
        IEmbeddingGenerator embeddings,
        IVectorEntityProjectorRegistry registry,
        VectorSyncOutboxMessage message,
        CancellationToken cancellationToken)
    {
        if (message.Operation == VectorSyncOperation.Delete)
        {
            await store.DeleteByEntityAsync(
                message.TenantId,
                message.EntityType,
                message.EntityId,
                cancellationToken);
            return;
        }

        var projector = registry.Resolve(message.EntityType)
            ?? throw new InvalidOperationException($"No projector for entity type '{message.EntityType}'.");

        var entity = await LoadEntityAsync(db, projector.ClrType, message.TenantId, message.EntityId, cancellationToken);
        if (entity is null)
        {
            await store.DeleteByEntityAsync(
                message.TenantId,
                message.EntityType,
                message.EntityId,
                cancellationToken);
            return;
        }

        var documents = projector.BuildDocuments(entity);
        await store.DeleteByEntityAsync(message.TenantId, message.EntityType, message.EntityId, cancellationToken);

        if (documents.Count == 0)
            return;

        var points = new List<(VectorPointDocument Document, float[] Embedding)>(documents.Count);
        foreach (var document in documents)
        {
            var embedding = await embeddings.GenerateAsync(document.Text, cancellationToken);
            points.Add((document, embedding));
        }

        await store.UpsertAsync(points, cancellationToken);
    }

    private static async Task<object?> LoadEntityAsync(
        BusinessOSDbContext db,
        Type clrType,
        Guid tenantId,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        if (clrType == typeof(AiDocument))
        {
            return await db.AiDocuments
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == entityId && x.TenantId == tenantId && !x.IsDeleted, cancellationToken);
        }

        if (clrType == typeof(Product))
        {
            return await db.Products
                .IgnoreQueryFilters()
                .Include(x => x.Category)
                .FirstOrDefaultAsync(x => x.Id == entityId && x.TenantId == tenantId && !x.IsDeleted, cancellationToken);
        }

        if (clrType == typeof(Project))
        {
            return await db.Projects
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == entityId && x.TenantId == tenantId && !x.IsDeleted, cancellationToken);
        }

        if (clrType == typeof(Customer))
        {
            return await db.Customers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == entityId && x.TenantId == tenantId && !x.IsDeleted, cancellationToken);
        }

        return await db.FindAsync(clrType, [entityId], cancellationToken);
    }

    private static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..max];
}
