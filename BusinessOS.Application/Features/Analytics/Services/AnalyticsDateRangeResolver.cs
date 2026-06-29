using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Features.Analytics.Models;

namespace BusinessOS.Application.Features.Analytics.Services;

public sealed class AnalyticsDateRangeResolver : IAnalyticsDateRangeResolver
{
    public AnalyticsDateRange Resolve(DateTime? startDate, DateTime? endDate, string? period)
    {
        var normalizedPeriod = string.IsNullOrWhiteSpace(period)
            ? null
            : period.Trim().ToLowerInvariant();

        if (normalizedPeriod is AnalyticsPeriods.Custom)
        {
            if (!startDate.HasValue || !endDate.HasValue)
                throw new BadRequestException(
                    "Both startDate and endDate are required for a custom date range.");

            return CreateRange(startDate.Value, endDate.Value, AnalyticsPeriods.Custom);
        }

        if (startDate.HasValue && endDate.HasValue)
            return CreateRange(startDate.Value, endDate.Value, AnalyticsPeriods.Custom);

        var utcNow = DateTime.UtcNow;

        return normalizedPeriod switch
        {
            AnalyticsPeriods.Today => CreateRange(
                utcNow.Date,
                utcNow.Date.AddDays(1).AddTicks(-1),
                AnalyticsPeriods.Today),

            AnalyticsPeriods.Last7Days => CreateRange(
                utcNow.Date.AddDays(-6),
                utcNow,
                AnalyticsPeriods.Last7Days),

            AnalyticsPeriods.Last30Days => CreateRange(
                utcNow.Date.AddDays(-29),
                utcNow,
                AnalyticsPeriods.Last30Days),

            AnalyticsPeriods.Last90Days => CreateRange(
                utcNow.Date.AddDays(-89),
                utcNow,
                AnalyticsPeriods.Last90Days),

            AnalyticsPeriods.Year => CreateRange(
                new DateTime(utcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                utcNow,
                AnalyticsPeriods.Year),

            null => CreateRange(
                utcNow.Date.AddDays(-29),
                utcNow,
                AnalyticsPeriods.Last30Days),

            _ => throw new BadRequestException(
                $"Invalid period '{period}'. Valid values: today, last7days, last30days, last90days, year, custom.")
        };
    }

    private static AnalyticsDateRange CreateRange(DateTime start, DateTime end, string period)
    {
        var normalizedStart = DateTime.SpecifyKind(start, DateTimeKind.Utc);
        var normalizedEnd = DateTime.SpecifyKind(end, DateTimeKind.Utc);

        if (normalizedStart > normalizedEnd)
            throw new BadRequestException(
                "startDate must be less than or equal to endDate.");

        return new AnalyticsDateRange(normalizedStart, normalizedEnd, period);
    }
}
