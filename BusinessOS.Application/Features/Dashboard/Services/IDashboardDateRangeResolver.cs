using BusinessOS.Application.Features.Dashboard.Models;

namespace BusinessOS.Application.Features.Dashboard.Services;

public interface IDashboardDateRangeResolver
{
    DashboardDateRange Resolve(DateTime? startDate, DateTime? endDate, string? period);
}
