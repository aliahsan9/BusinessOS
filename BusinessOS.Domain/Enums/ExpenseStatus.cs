namespace BusinessOS.Domain.Enums;

public enum ExpenseStatus
{
    Pending,
    Approved,
    Rejected,
    Paid
}

public static class ExpenseStatusNames
{
    public const string Pending = nameof(ExpenseStatus.Pending);
    public const string Approved = nameof(ExpenseStatus.Approved);
    public const string Rejected = nameof(ExpenseStatus.Rejected);
    public const string Paid = nameof(ExpenseStatus.Paid);

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Pending, Approved, Rejected, Paid
    };

    public static bool IsValid(string? status) =>
        status is not null && All.Contains(status);
}
