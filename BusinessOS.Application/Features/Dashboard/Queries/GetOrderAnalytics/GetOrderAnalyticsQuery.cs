using BusinessOS.Application.Features.Dashboard.DTOs;
using BusinessOS.Application.Features.Dashboard.Services;
using BusinessOS.Application.Features.Dashboard.Validators;
using MediatR;

namespace BusinessOS.Application.Features.Dashboard.Queries.GetOrderAnalytics;

public sealed record GetOrderAnalyticsQuery(
    DateTime? StartDate,
    DateTime? EndDate,
    string? Period) : DashboardDateRangeQuery(StartDate, EndDate, Period),
    IRequest<OrderAnalyticsResponse>;

public sealed class GetOrderAnalyticsQueryHandler
    : IRequestHandler<GetOrderAnalyticsQuery, OrderAnalyticsResponse>
{
    private readonly IDashboardDateRangeResolver _dateRangeResolver;
    private readonly IAnalyticsService _analyticsService;
    private readonly IDashboardCacheService _cacheService;

    public GetOrderAnalyticsQueryHandler(
        IDashboardDateRangeResolver dateRangeResolver,
        IAnalyticsService analyticsService,
        IDashboardCacheService cacheService)
    {
        _dateRangeResolver = dateRangeResolver;
        _analyticsService = analyticsService;
        _cacheService = cacheService;
    }

    public Task<OrderAnalyticsResponse> Handle(
        GetOrderAnalyticsQuery request,
        CancellationToken cancellationToken)
    {
        var dateRange = _dateRangeResolver.Resolve(request.StartDate, request.EndDate, request.Period);

        return _cacheService.GetOrCreateAsync(
            $"orders:{dateRange.CacheKeySuffix}",
            ct => _analyticsService.GetOrderAnalyticsAsync(dateRange, ct),
            cancellationToken);
    }
}
