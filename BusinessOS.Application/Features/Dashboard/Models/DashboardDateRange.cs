namespace BusinessOS.Application.Features.Dashboard.Models;

public sealed record DashboardDateRange(
    DateTime StartDate,
    DateTime EndDate,
    string Period)
{
    public string CacheKeySuffix => $"{Period}:{StartDate:yyyyMMdd}:{EndDate:yyyyMMdd}";
}

public static class DateRangePeriods
{
    public const string Today = "today";
    public const string Week = "week";
    public const string Month = "month";
    public const string Year = "year";
    public const string All = "all";
    public const string Custom = "custom";

    public static readonly IReadOnlySet<string> AllPeriods = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Today,
        Week,
        Month,
        Year,
        All,
        Custom
    };

    public static bool IsValid(string? period) =>
        period is null || AllPeriods.Contains(period);
}
