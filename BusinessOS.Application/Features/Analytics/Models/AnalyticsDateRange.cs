namespace BusinessOS.Application.Features.Analytics.Models;

public sealed record AnalyticsDateRange(
    DateTime StartDate,
    DateTime EndDate,
    string Period)
{
    public string CacheKeySuffix => $"{Period}:{StartDate:yyyyMMdd}:{EndDate:yyyyMMdd}";
}

public static class AnalyticsPeriods
{
    public const string Today = "today";
    public const string Last7Days = "last7days";
    public const string Last30Days = "last30days";
    public const string Last90Days = "last90days";
    public const string Year = "year";
    public const string Custom = "custom";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Today,
        Last7Days,
        Last30Days,
        Last90Days,
        Year,
        Custom
    };

    public static bool IsValid(string? period) =>
        period is null || All.Contains(period);
}
