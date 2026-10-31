using BusinessOS.Application.Common.Idempotency;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class WebhookEventStore : IWebhookEventStore
{
    private readonly BusinessOSDbContext _context;

    public WebhookEventStore(BusinessOSDbContext context) => _context = context;

    public async Task<WebhookClaim> TryBeginAsync(
        PaymentProviderType provider,
        string eventId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.ProcessedWebhookEvents
            .FirstOrDefaultAsync(x => x.Provider == provider && x.EventId == eventId, cancellationToken);

        if (existing is not null)
        {
            if (existing.State == WebhookEventState.Processed)
                return new WebhookClaim.Duplicate();

            if (existing.State == WebhookEventState.Failed)
            {
                existing.State = WebhookEventState.Processing;
                existing.ReceivedAt = DateTime.UtcNow;
                existing.ProcessedAt = null;
                await _context.SaveChangesAsync(cancellationToken);
                return new WebhookClaim.Started(existing.Id);
            }

            return new WebhookClaim.Busy();
        }

        var record = new ProcessedWebhookEvent
        {
            Id = Guid.NewGuid(),
            Provider = provider,
            EventId = eventId,
            State = WebhookEventState.Processing,
            ReceivedAt = DateTime.UtcNow
        };

        _context.ProcessedWebhookEvents.Add(record);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return new WebhookClaim.Started(record.Id);
        }
        catch (DbUpdateException ex) when (DbExceptionClassifier.IsUniqueViolation(ex))
        {
            _context.Detach(record);
            return new WebhookClaim.Busy();
        }
    }

    public async Task CompleteAsync(Guid eventRecordId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var record = await _context.ProcessedWebhookEvents
            .FirstOrDefaultAsync(x => x.Id == eventRecordId, cancellationToken);
        if (record is null)
            return;

        record.State = WebhookEventState.Processed;
        record.TenantId = tenantId ?? record.TenantId;
        record.ProcessedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AbandonAsync(Guid eventRecordId, CancellationToken cancellationToken = default)
    {
        var record = await _context.ProcessedWebhookEvents
            .FirstOrDefaultAsync(x => x.Id == eventRecordId, cancellationToken);
        if (record is null)
            return;

        record.State = WebhookEventState.Failed;
        record.ProcessedAt = null;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
