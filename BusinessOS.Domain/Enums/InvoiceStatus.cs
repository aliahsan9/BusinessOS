namespace BusinessOS.Domain.Enums;

public enum InvoiceStatus
{
    Draft,
    Sent,
    Paid,
    PartiallyPaid,
    Overdue,
    Cancelled
}

public static class InvoiceStatusNames
{
    public const string Draft = nameof(InvoiceStatus.Draft);
    public const string Sent = nameof(InvoiceStatus.Sent);
    public const string Paid = nameof(InvoiceStatus.Paid);
    public const string PartiallyPaid = nameof(InvoiceStatus.PartiallyPaid);
    public const string Overdue = nameof(InvoiceStatus.Overdue);
    public const string Cancelled = nameof(InvoiceStatus.Cancelled);

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Draft,
        Sent,
        Paid,
        PartiallyPaid,
        Overdue,
        Cancelled
    };

    public static bool IsValid(string? status) =>
        status is not null && All.Contains(status);
}
