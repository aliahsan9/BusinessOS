using BusinessOS.Application.Features.Analytics.Models;

namespace BusinessOS.Application.Features.Analytics.Services;

public interface IAnalyticsDateRangeResolver
{
    AnalyticsDateRange Resolve(DateTime? startDate, DateTime? endDate, string? period);
}
