using BusinessOS.Domain.Entities;

namespace BusinessOS.Application.Common.Idempotency;

public enum IdempotencyOutcomeKind
{
    Started,
    Replay,
    InProgress,
    Conflict
}

public sealed class IdempotencyOutcome
{
    public IdempotencyOutcomeKind Kind { get; init; }

    public IdempotencyRecord? Record { get; init; }

    public Guid? ResourceId { get; init; }

    public string? ResponseBody { get; init; }

    public int? StatusCode { get; init; }

    public static IdempotencyOutcome Started(IdempotencyRecord record) => new()
    {
        Kind = IdempotencyOutcomeKind.Started,
        Record = record
    };

    public static IdempotencyOutcome Replay(IdempotencyRecord record) => new()
    {
        Kind = IdempotencyOutcomeKind.Replay,
        Record = record,
        ResourceId = record.ResourceId,
        ResponseBody = record.ResponseBody,
        StatusCode = record.ResponseStatusCode
    };

    public static IdempotencyOutcome InProgress(IdempotencyRecord record) => new()
    {
        Kind = IdempotencyOutcomeKind.InProgress,
        Record = record,
        ResourceId = record.ResourceId
    };

    public static IdempotencyOutcome Conflict() => new()
    {
        Kind = IdempotencyOutcomeKind.Conflict
    };
}

public interface IIdempotencyService
{
    Task<IdempotencyOutcome> BeginLocalAsync(
        string operation,
        string key,
        string requestHash,
        CancellationToken cancellationToken = default);

    Task<IdempotencyOutcome> ClaimCommittedAsync(
        string operation,
        string key,
        string requestHash,
        CancellationToken cancellationToken = default);

    void CompleteLocal(IdempotencyRecord record, int statusCode, string responseBody, Guid? resourceId);

    Task AttachResourceAsync(Guid recordId, Guid resourceId, CancellationToken cancellationToken = default);

    Task CompleteCommittedAsync(
        Guid recordId,
        int statusCode,
        string responseBody,
        Guid? resourceId,
        CancellationToken cancellationToken = default);

    void Detach(IdempotencyRecord record);
}
