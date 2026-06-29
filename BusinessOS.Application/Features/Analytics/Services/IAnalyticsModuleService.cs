using BusinessOS.Application.Features.Analytics.DTOs;
using BusinessOS.Application.Features.Dashboard.DTOs;

namespace BusinessOS.Application.Features.Analytics.Services;

public interface IAnalyticsModuleService
{
    Task<AnalyticsOverviewResponse> GetOverviewAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        CancellationToken cancellationToken = default);

    Task<ChartDataResponse> GetRevenueChartAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        CancellationToken cancellationToken = default);

    Task<ChartDataResponse> GetExpenseChartAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        CancellationToken cancellationToken = default);

    Task<ChartDataResponse> GetProfitChartAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        CancellationToken cancellationToken = default);

    Task<ChartDataResponse> GetCustomerGrowthChartAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        CancellationToken cancellationToken = default);

    Task<AnalyticsProjectAnalyticsResponse> GetProjectAnalyticsAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        CancellationToken cancellationToken = default);

    Task<AnalyticsTaskAnalyticsResponse> GetTaskAnalyticsAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        CancellationToken cancellationToken = default);

    Task<AnalyticsTopCustomersResponse> GetTopCustomersAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        int top = 10,
        CancellationToken cancellationToken = default);

    Task<AnalyticsRecentActivityResponse> GetRecentActivityAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        int limit = 20,
        CancellationToken cancellationToken = default);
}
