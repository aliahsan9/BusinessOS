using BusinessOS.Application.Features.VectorSearch.Services;
using BusinessOS.Domain.Common;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BusinessOS.Infrastructure.VectorSearch;

public sealed class VectorSyncOutboxInterceptor : SaveChangesInterceptor
{
    private readonly IVectorEntityProjectorRegistry _registry;

    public VectorSyncOutboxInterceptor(IVectorEntityProjectorRegistry registry)
    {
        _registry = registry;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        EnqueueFromChangeTracker(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        EnqueueFromChangeTracker(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void EnqueueFromChangeTracker(DbContext? context)
    {
        if (context is not BusinessOSDbContext db)
            return;

        var outboxWriter = new VectorSyncOutboxWriter(db);
        var tracked = _registry.TrackedClrTypes;

        foreach (var entry in db.ChangeTracker.Entries())
        {
            if (!tracked.Contains(entry.Entity.GetType()))
                continue;

            if (entry.Entity is not BaseEntity baseEntity)
                continue;

            var projector = _registry.Resolve(entry.Entity.GetType());
            if (projector is null)
                continue;

            var tenantIdProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "TenantId");
            var tenantId = tenantIdProp?.CurrentValue as Guid? ?? Guid.Empty;
            if (tenantId == Guid.Empty)
                continue;

            var isDeletedProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "IsDeleted");
            var isSoftDeleted = isDeletedProp?.CurrentValue is true
                                && entry.State is EntityState.Modified or EntityState.Deleted;

            VectorSyncOperation operation;
            if (entry.State == EntityState.Deleted || isSoftDeleted)
                operation = VectorSyncOperation.Delete;
            else if (entry.State is EntityState.Added or EntityState.Modified)
                operation = VectorSyncOperation.Upsert;
            else
                continue;

            var alreadyQueued = db.ChangeTracker.Entries<VectorSyncOutboxMessage>()
                .Any(e => e.State == EntityState.Added
                          && e.Entity.EntityType == projector.EntityType
                          && e.Entity.EntityId == baseEntity.Id
                          && e.Entity.Operation == operation);

            if (alreadyQueued)
                continue;

            outboxWriter.Enqueue(tenantId, projector.EntityType, baseEntity.Id, operation);
        }
    }
}
