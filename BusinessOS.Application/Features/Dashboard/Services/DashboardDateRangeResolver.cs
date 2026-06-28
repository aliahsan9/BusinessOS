using BusinessOS.Application.Features.Dashboard.Models;

namespace BusinessOS.Application.Features.Dashboard.Services;

public sealed class DashboardDateRangeResolver : IDashboardDateRangeResolver
{
    public DashboardDateRange Resolve(DateTime? startDate, DateTime? endDate, string? period)
    {
        var normalizedPeriod = string.IsNullOrWhiteSpace(period)
            ? null
            : period.Trim().ToLowerInvariant();

        if (normalizedPeriod is DateRangePeriods.Custom)
        {
            if (!startDate.HasValue || !endDate.HasValue)
                throw new Application.Common.Exceptions.BadRequestException(
                    "Both startDate and endDate are required for a custom date range.");

            return CreateRange(startDate.Value, endDate.Value, DateRangePeriods.Custom);
        }

        if (startDate.HasValue && endDate.HasValue)
            return CreateRange(startDate.Value, endDate.Value, DateRangePeriods.Custom);

        var utcNow = DateTime.UtcNow;

        return normalizedPeriod switch
        {
            DateRangePeriods.Today => CreateRange(
                utcNow.Date,
                utcNow.Date.AddDays(1).AddTicks(-1),
                DateRangePeriods.Today),

            DateRangePeriods.Week => CreateRange(
                utcNow.Date.AddDays(-(int)utcNow.DayOfWeek),
                utcNow,
                DateRangePeriods.Week),

            DateRangePeriods.Month => CreateRange(
                new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                utcNow,
                DateRangePeriods.Month),

            DateRangePeriods.Year => CreateRange(
                new DateTime(utcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                utcNow,
                DateRangePeriods.Year),

            DateRangePeriods.All => CreateRange(
                DateTime.UnixEpoch,
                utcNow,
                DateRangePeriods.All),

            null => CreateRange(DateTime.UnixEpoch, utcNow, DateRangePeriods.All),

            _ => throw new Application.Common.Exceptions.BadRequestException(
                $"Invalid period '{period}'. Valid values: today, week, month, year, all, custom.")
        };
    }

    private static DashboardDateRange CreateRange(DateTime start, DateTime end, string period)
    {
        var normalizedStart = DateTime.SpecifyKind(start, DateTimeKind.Utc);
        var normalizedEnd = DateTime.SpecifyKind(end, DateTimeKind.Utc);

        if (normalizedStart > normalizedEnd)
            throw new Application.Common.Exceptions.BadRequestException(
                "startDate must be less than or equal to endDate.");

        return new DashboardDateRange(normalizedStart, normalizedEnd, period);
    }
}
