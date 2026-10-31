namespace BusinessOS.Domain.Enums;

public enum IdempotencyState
{
    Processing = 0,
    Completed = 1,
    Failed = 2
}
