namespace BusinessOS.Domain.Enums;

public enum QuotationStatus
{
    Draft,
    Sent,
    Accepted,
    Rejected,
    Expired
}

public static class QuotationStatusNames
{
    public const string Draft = nameof(QuotationStatus.Draft);
    public const string Sent = nameof(QuotationStatus.Sent);
    public const string Accepted = nameof(QuotationStatus.Accepted);
    public const string Rejected = nameof(QuotationStatus.Rejected);
    public const string Expired = nameof(QuotationStatus.Expired);

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Draft,
        Sent,
        Accepted,
        Rejected,
        Expired
    };

    public static bool IsValid(string? status) =>
        status is not null && All.Contains(status);
}
