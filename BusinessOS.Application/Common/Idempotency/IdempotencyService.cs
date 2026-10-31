using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Common.Idempotency;

public sealed class IdempotencyService : IIdempotencyService
{
    public static readonly TimeSpan InProgressWindow = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan CompletedRetention = TimeSpan.FromDays(90);

    private readonly IApplicationDbContext _context;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<IdempotencyService> _logger;

    public IdempotencyService(
        IApplicationDbContext context,
        ITenantProvider tenantProvider,
        ILogger<IdempotencyService> logger)
    {
        _context = context;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<IdempotencyOutcome> BeginLocalAsync(
        string operation,
        string key,
        string requestHash,
        CancellationToken cancellationToken = default)
    {
        EnsureKey(key);
        var existing = await FindAsync(operation, key, cancellationToken);
        if (existing is not null)
            return Interpret(existing, requestHash, reclaimStuck: true);

        var record = CreateRecord(operation, key, requestHash);
        _context.IdempotencyRecords.Add(record);
        return IdempotencyOutcome.Started(record);
    }

    public async Task<IdempotencyOutcome> ClaimCommittedAsync(
        string operation,
        string key,
        string requestHash,
        CancellationToken cancellationToken = default)
    {
        EnsureKey(key);
        var existing = await FindAsync(operation, key, cancellationToken);
        if (existing is not null)
            return Interpret(existing, requestHash, reclaimStuck: true);

        var record = CreateRecord(operation, key, requestHash);
        _context.IdempotencyRecords.Add(record);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbExceptionClassifier.IsUniqueViolation(ex))
        {
            Detach(record);
            var winner = await FindAsync(operation, key, cancellationToken);
            if (winner is null)
                throw;

            _logger.LogInformation(
                "Concurrent idempotency claim resolved for operation {Operation} in tenant {TenantId}",
                operation,
                _tenantProvider.TenantId);
            return Interpret(winner, requestHash, reclaimStuck: false);
        }

        return IdempotencyOutcome.Started(record);
    }

    public void CompleteLocal(IdempotencyRecord record, int statusCode, string responseBody, Guid? resourceId)
    {
        record.State = IdempotencyState.Completed;
        record.ResponseStatusCode = statusCode;
        record.ResponseBody = Trim(responseBody);
        record.ResourceId = resourceId;
        record.CompletedAt = DateTime.UtcNow;
        record.UpdatedAt = record.CompletedAt;
    }

    public async Task AttachResourceAsync(Guid recordId, Guid resourceId, CancellationToken cancellationToken = default)
    {
        var record = await _context.IdempotencyRecords
            .FirstAsync(x => x.Id == recordId, cancellationToken);
        record.ResourceId = resourceId;
        record.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteCommittedAsync(
        Guid recordId,
        int statusCode,
        string responseBody,
        Guid? resourceId,
        CancellationToken cancellationToken = default)
    {
        var record = await _context.IdempotencyRecords
            .FirstAsync(x => x.Id == recordId, cancellationToken);
        CompleteLocal(record, statusCode, responseBody, resourceId);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public void Detach(IdempotencyRecord record) =>
        _context.Detach(record);

    private async Task<IdempotencyRecord?> FindAsync(
        string operation,
        string key,
        CancellationToken cancellationToken) =>
        await _context.IdempotencyRecords
            .FirstOrDefaultAsync(x => x.Operation == operation && x.Key == key, cancellationToken);

    private IdempotencyOutcome Interpret(IdempotencyRecord existing, string requestHash, bool reclaimStuck)
    {
        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Idempotency key reused with a different request for operation {Operation} in tenant {TenantId}",
                existing.Operation,
                existing.TenantId);
            return IdempotencyOutcome.Conflict();
        }

        if (existing.State == IdempotencyState.Completed && existing.ResourceId.HasValue)
            return IdempotencyOutcome.Replay(existing);

        if (existing.State == IdempotencyState.Failed)
        {
            existing.State = IdempotencyState.Processing;
            existing.UpdatedAt = DateTime.UtcNow;
            return IdempotencyOutcome.Started(existing);
        }

        var age = DateTime.UtcNow - existing.CreatedAt;
        if (existing.ResourceId.HasValue)
            return IdempotencyOutcome.InProgress(existing);

        if (reclaimStuck && age >= InProgressWindow)
        {
            existing.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation(
                "Reclaimed stuck idempotency record {RecordId} for operation {Operation}",
                existing.Id,
                existing.Operation);
            return IdempotencyOutcome.Started(existing);
        }

        return IdempotencyOutcome.InProgress(existing);
    }

    private IdempotencyRecord CreateRecord(string operation, string key, string requestHash)
    {
        var now = DateTime.UtcNow;
        return new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantProvider.TenantId,
            Operation = operation,
            Key = key,
            RequestHash = requestHash,
            State = IdempotencyState.Processing,
            CreatedAt = now,
            ExpiresAt = now.Add(CompletedRetention)
        };
    }

    private static void EnsureKey(string key)
    {
        if (!IdempotencyKeyRules.IsValid(key))
            throw new BadRequestException("Idempotency-Key must be 1-128 visible ASCII characters without spaces.");
    }

    private static string Trim(string responseBody) =>
        responseBody.Length <= 4000 ? responseBody : responseBody[..4000];
}

public static class DbExceptionClassifier
{
    public static bool IsUniqueViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.GetType().Name == "SqlException")
            {
                var number = current.GetType().GetProperty("Number")?.GetValue(current) as int?;
                if (number is 2601 or 2627)
                    return true;
            }

            var message = current.Message;
            if (message.Contains("unique", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
