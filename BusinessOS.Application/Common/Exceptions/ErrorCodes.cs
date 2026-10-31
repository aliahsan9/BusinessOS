namespace BusinessOS.Application.Common.Exceptions;

public static class ErrorCodes
{
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";
    public const string IdempotencyInProgress = "IDEMPOTENCY_IN_PROGRESS";
    public const string InsufficientStock = "INSUFFICIENT_STOCK";
}
