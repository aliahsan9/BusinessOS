using BusinessOS.Application.Common.Idempotency;
using BusinessOS.Application.Features.Integrations;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Integrations;

public sealed class IntegrationWebhookIdempotencyStore : IIntegrationWebhookIdempotencyStore
{
    private readonly BusinessOSDbContext _db;

    public IntegrationWebhookIdempotencyStore(BusinessOSDbContext db) => _db = db;

    public async Task<IntegrationWebhookClaim> TryBeginAsync(string providerKey, string eventId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.IntegrationWebhookReceipts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.ProviderKey == providerKey && x.EventId == eventId, cancellationToken);

        if (existing is not null)
        {
            if (existing.State == IntegrationWebhookStates.Processed)
                return new IntegrationWebhookClaim.Duplicate();

            if (existing.State is IntegrationWebhookStates.Failed or IntegrationWebhookStates.DeadLetter)
            {
                existing.State = IntegrationWebhookStates.Processing;
                existing.ReceivedAt = DateTime.UtcNow;
                existing.ProcessedAt = null;
                existing.ErrorRedacted = null;
                existing.TenantId = tenantId ?? existing.TenantId;
                await _db.SaveChangesAsync(cancellationToken);
                return new IntegrationWebhookClaim.Started(existing.Id);
            }

            return new IntegrationWebhookClaim.Busy();
        }

        var record = new IntegrationWebhookReceipt
        {
            ProviderKey = providerKey,
            EventId = eventId,
            TenantId = tenantId,
            State = IntegrationWebhookStates.Processing,
            ReceivedAt = DateTime.UtcNow
        };
        _db.IntegrationWebhookReceipts.Add(record);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return new IntegrationWebhookClaim.Started(record.Id);
        }
        catch (DbUpdateException ex) when (BusinessOS.Application.Common.Idempotency.DbExceptionClassifier.IsUniqueViolation(ex))
        {
            _db.Entry(record).State = EntityState.Detached;
            return new IntegrationWebhookClaim.Busy();
        }
    }

    public async Task CompleteAsync(Guid receiptId, CancellationToken cancellationToken = default)
    {
        var record = await _db.IntegrationWebhookReceipts.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == receiptId, cancellationToken);
        if (record is null)
            return;
        record.State = IntegrationWebhookStates.Processed;
        record.ProcessedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeadLetterAsync(Guid receiptId, string errorRedacted, CancellationToken cancellationToken = default)
    {
        var record = await _db.IntegrationWebhookReceipts.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == receiptId, cancellationToken);
        if (record is null)
            return;
        record.State = IntegrationWebhookStates.DeadLetter;
        record.ErrorRedacted = NalvaGuard.Redact(NalvaGuard.Clip(errorRedacted, 500));
        record.ProcessedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
