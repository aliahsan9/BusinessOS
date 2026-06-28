using BusinessOS.Application.Features.Dashboard.DTOs;
using BusinessOS.Application.Features.Dashboard.Services;
using BusinessOS.Application.Features.Dashboard.Validators;
using MediatR;

namespace BusinessOS.Application.Features.Dashboard.Queries.GetCustomerAnalytics;

public sealed record GetCustomerAnalyticsDashboardQuery(
    DateTime? StartDate,
    DateTime? EndDate,
    string? Period) : DashboardDateRangeQuery(StartDate, EndDate, Period),
    IRequest<CustomerAnalyticsDashboardResponse>;

public sealed class GetCustomerAnalyticsDashboardQueryHandler
    : IRequestHandler<GetCustomerAnalyticsDashboardQuery, CustomerAnalyticsDashboardResponse>
{
    private readonly IDashboardDateRangeResolver _dateRangeResolver;
    private readonly IAnalyticsService _analyticsService;
    private readonly IDashboardCacheService _cacheService;

    public GetCustomerAnalyticsDashboardQueryHandler(
        IDashboardDateRangeResolver dateRangeResolver,
        IAnalyticsService analyticsService,
        IDashboardCacheService cacheService)
    {
        _dateRangeResolver = dateRangeResolver;
        _analyticsService = analyticsService;
        _cacheService = cacheService;
    }

    public Task<CustomerAnalyticsDashboardResponse> Handle(
        GetCustomerAnalyticsDashboardQuery request,
        CancellationToken cancellationToken)
    {
        var dateRange = _dateRangeResolver.Resolve(request.StartDate, request.EndDate, request.Period);

        return _cacheService.GetOrCreateAsync(
            $"customers:{dateRange.CacheKeySuffix}",
            ct => _analyticsService.GetCustomerAnalyticsAsync(dateRange, ct),
            cancellationToken);
    }
}
