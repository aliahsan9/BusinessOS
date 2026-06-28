namespace BusinessOS.Domain.Enums;

public enum PurchaseOrderStatus
{
    Draft,
    Pending,
    Approved,
    Received,
    Cancelled
}

public static class PurchaseOrderStatusNames
{
    public const string Draft = nameof(PurchaseOrderStatus.Draft);
    public const string Pending = nameof(PurchaseOrderStatus.Pending);
    public const string Approved = nameof(PurchaseOrderStatus.Approved);
    public const string Received = nameof(PurchaseOrderStatus.Received);
    public const string Cancelled = nameof(PurchaseOrderStatus.Cancelled);

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Draft,
        Pending,
        Approved,
        Received,
        Cancelled
    };

    public static bool IsValid(string? status) =>
        status is not null && All.Contains(status);
}
