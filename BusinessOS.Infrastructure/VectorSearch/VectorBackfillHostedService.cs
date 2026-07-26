using BusinessOS.Application.Features.VectorSearch.Models;
using BusinessOS.Application.Features.VectorSearch.Options;
using BusinessOS.Application.Features.VectorSearch.Services;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.VectorSearch;

public sealed class VectorBackfillHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly VectorSyncOptions _options;
    private readonly QdrantOptions _qdrantOptions;
    private readonly ILogger<VectorBackfillHostedService> _logger;

    public VectorBackfillHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<VectorSyncOptions> options,
        IOptions<QdrantOptions> qdrantOptions,
        ILogger<VectorBackfillHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _qdrantOptions = qdrantOptions.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_qdrantOptions.Enabled || !_options.BackfillOnStartup)
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
            var writer = new VectorSyncOutboxWriter(db);
            var enqueued = 0;

            var documents = await db.AiDocuments.IgnoreQueryFilters()
                .Where(x => !x.IsDeleted)
                .Select(x => new EntityRef(x.Id, x.TenantId))
                .ToListAsync(cancellationToken);
            enqueued += await EnqueueMissingAsync(db, writer, VectorEntityTypes.Document, documents, cancellationToken);

            var products = await db.Products.IgnoreQueryFilters()
                .Where(x => !x.IsDeleted)
                .Select(x => new EntityRef(x.Id, x.TenantId))
                .ToListAsync(cancellationToken);
            enqueued += await EnqueueMissingAsync(db, writer, VectorEntityTypes.Product, products, cancellationToken);

            var projects = await db.Projects.IgnoreQueryFilters()
                .Where(x => !x.IsDeleted)
                .Select(x => new EntityRef(x.Id, x.TenantId))
                .ToListAsync(cancellationToken);
            enqueued += await EnqueueMissingAsync(db, writer, VectorEntityTypes.Project, projects, cancellationToken);

            var customers = await db.Customers.IgnoreQueryFilters()
                .Where(x => !x.IsDeleted)
                .Select(x => new EntityRef(x.Id, x.TenantId))
                .ToListAsync(cancellationToken);
            enqueued += await EnqueueMissingAsync(db, writer, VectorEntityTypes.Customer, customers, cancellationToken);

            if (enqueued > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Enqueued {Count} vector backfill outbox messages.", enqueued);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vector backfill on startup failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task<int> EnqueueMissingAsync(
        BusinessOSDbContext db,
        VectorSyncOutboxWriter writer,
        string entityType,
        IReadOnlyList<EntityRef> entities,
        CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
            return 0;

        var ids = entities.Select(e => e.Id).ToList();
        var alreadySynced = await db.VectorSyncOutboxMessages
            .Where(x =>
                x.EntityType == entityType
                && ids.Contains(x.EntityId)
                && (x.Status == VectorSyncStatus.Succeeded
                    || x.Status == VectorSyncStatus.Pending
                    || x.Status == VectorSyncStatus.Processing
                    || x.Status == VectorSyncStatus.Failed))
            .Select(x => x.EntityId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var syncedSet = alreadySynced.ToHashSet();
        var count = 0;

        foreach (var entity in entities)
        {
            if (syncedSet.Contains(entity.Id))
                continue;

            writer.Enqueue(entity.TenantId, entityType, entity.Id, VectorSyncOperation.Upsert);
            count++;
        }

        return count;
    }

    private sealed record EntityRef(Guid Id, Guid TenantId);
}
