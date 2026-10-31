namespace BusinessOS.Domain.Enums;

public static class PaymentStatusNames
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Refunded = "Refunded";
    public const string RequiresReconciliation = "RequiresReconciliation";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Pending,
        Processing,
        Completed,
        Failed,
        Refunded,
        RequiresReconciliation
    };

    public static bool IsFinal(string? status) =>
        status is not null && (
            status.Equals(Completed, StringComparison.OrdinalIgnoreCase) ||
            status.Equals(Failed, StringComparison.OrdinalIgnoreCase) ||
            status.Equals(Refunded, StringComparison.OrdinalIgnoreCase));
}
