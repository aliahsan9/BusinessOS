namespace BusinessOS.Domain.Enums;

public enum OrderStatus
{
    Pending,
    Confirmed,
    Processing,
    Completed,
    Cancelled
}

public static class OrderStatusNames
{
    public const string Pending = nameof(OrderStatus.Pending);
    public const string Confirmed = nameof(OrderStatus.Confirmed);
    public const string Processing = nameof(OrderStatus.Processing);
    public const string Completed = nameof(OrderStatus.Completed);
    public const string Cancelled = nameof(OrderStatus.Cancelled);

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Pending,
        Confirmed,
        Processing,
        Completed,
        Cancelled
    };

    public static bool IsValid(string? status) =>
        status is not null && All.Contains(status);
}
