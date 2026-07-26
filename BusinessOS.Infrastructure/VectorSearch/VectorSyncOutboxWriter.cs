using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.VectorSearch.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Infrastructure.VectorSearch;

public sealed class VectorSyncOutboxWriter : IVectorSyncOutboxWriter
{
    private readonly IApplicationDbContext _context;

    public VectorSyncOutboxWriter(IApplicationDbContext context)
    {
        _context = context;
    }

    public void Enqueue(
        Guid tenantId,
        string entityType,
        Guid entityId,
        VectorSyncOperation operation,
        string? payloadJson = null)
    {
        _context.VectorSyncOutboxMessages.Add(new VectorSyncOutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = entityType,
            EntityId = entityId,
            Operation = operation,
            PayloadJson = payloadJson,
            Status = VectorSyncStatus.Pending,
            AttemptCount = 0,
            NextAttemptAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
    }
}
