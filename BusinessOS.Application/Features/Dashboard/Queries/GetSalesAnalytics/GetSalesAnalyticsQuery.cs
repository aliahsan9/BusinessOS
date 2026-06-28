using BusinessOS.Application.Features.Dashboard.DTOs;
using BusinessOS.Application.Features.Dashboard.Services;
using BusinessOS.Application.Features.Dashboard.Validators;
using MediatR;

namespace BusinessOS.Application.Features.Dashboard.Queries.GetSalesAnalytics;

public sealed record GetSalesAnalyticsQuery(
    DateTime? StartDate,
    DateTime? EndDate,
    string? Period) : DashboardDateRangeQuery(StartDate, EndDate, Period),
    IRequest<SalesAnalyticsResponse>;

public sealed class GetSalesAnalyticsQueryHandler
    : IRequestHandler<GetSalesAnalyticsQuery, SalesAnalyticsResponse>
{
    private readonly IDashboardDateRangeResolver _dateRangeResolver;
    private readonly IAnalyticsService _analyticsService;
    private readonly IDashboardCacheService _cacheService;

    public GetSalesAnalyticsQueryHandler(
        IDashboardDateRangeResolver dateRangeResolver,
        IAnalyticsService analyticsService,
        IDashboardCacheService cacheService)
    {
        _dateRangeResolver = dateRangeResolver;
        _analyticsService = analyticsService;
        _cacheService = cacheService;
    }

    public Task<SalesAnalyticsResponse> Handle(
        GetSalesAnalyticsQuery request,
        CancellationToken cancellationToken)
    {
        var dateRange = _dateRangeResolver.Resolve(request.StartDate, request.EndDate, request.Period);

        return _cacheService.GetOrCreateAsync(
            $"sales:{dateRange.CacheKeySuffix}",
            ct => _analyticsService.GetSalesAnalyticsAsync(dateRange, ct),
            cancellationToken);
    }
}
