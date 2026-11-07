using BusinessOS.Application.Features.Dashboard.Models;

namespace BusinessOS.Application.Features.Dashboard.Services;

/// <summary>
/// Bucket size for a dashboard period. Short filters need a finer grain so a
/// single sale still rises and falls against the quiet hours or days around it.
/// </summary>
public static class TrendSeries
{
    public enum Grain
    {
        Hour,
        Day,
        Month,
        /// <summary>Days that have activity only. Used for all-time sales.</summary>
        SparseDay,
        /// <summary>Months that have activity only. Used for all-time finance.</summary>
        SparseMonth
    }

    public static Grain ForSales(DashboardDateRange range)
    {
        if (IsAll(range)) return Grain.SparseDay;
        return ForSpan(range);
    }

    public static Grain ForFinance(DashboardDateRange range)
    {
        if (IsAll(range)) return Grain.SparseMonth;
        return ForSpan(range);
    }

    public static DateTime Truncate(DateTime value, Grain grain)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return grain switch
        {
            Grain.Hour => new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc),
            Grain.Day or Grain.SparseDay => new DateTime(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc),
            _ => new DateTime(utc.Year, utc.Month, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    public static string Format(DateTime bucket, Grain grain) => grain switch
    {
        Grain.Hour => bucket.ToString("yyyy-MM-dd'T'HH:00:00'Z'"),
        Grain.Day or Grain.SparseDay => bucket.ToString("yyyy-MM-dd"),
        _ => bucket.ToString("yyyy-MM")
    };

    public static IEnumerable<DateTime> Buckets(DashboardDateRange range, Grain grain)
    {
        var start = DateTime.SpecifyKind(range.StartDate, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(range.EndDate, DateTimeKind.Utc);
        if (end < start) yield break;

        var cursor = Truncate(start, grain);
        var last = Truncate(end, grain);
        var limit = grain switch
        {
            Grain.Hour => 36,
            Grain.Day => 62,
            _ => 18
        };

        var count = 0;
        while (cursor <= last && count < limit)
        {
            yield return cursor;
            cursor = grain switch
            {
                Grain.Hour => cursor.AddHours(1),
                Grain.Day => cursor.AddDays(1),
                _ => cursor.AddMonths(1)
            };
            count++;
        }
    }

    private static Grain ForSpan(DashboardDateRange range)
    {
        var span = range.EndDate - range.StartDate;
        if (span <= TimeSpan.FromHours(36)) return Grain.Hour;
        if (span <= TimeSpan.FromDays(62)) return Grain.Day;
        return Grain.Month;
    }

    private static bool IsAll(DashboardDateRange range) =>
        string.Equals(range.Period, DateRangePeriods.All, StringComparison.OrdinalIgnoreCase);
}
