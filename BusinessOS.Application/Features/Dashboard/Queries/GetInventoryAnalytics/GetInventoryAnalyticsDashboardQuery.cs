using BusinessOS.Application.Features.Dashboard.DTOs;
using BusinessOS.Application.Features.Dashboard.Services;
using BusinessOS.Application.Features.Dashboard.Validators;
using MediatR;

namespace BusinessOS.Application.Features.Dashboard.Queries.GetInventoryAnalytics;

public sealed record GetInventoryAnalyticsDashboardQuery(
    DateTime? StartDate,
    DateTime? EndDate,
    string? Period) : DashboardDateRangeQuery(StartDate, EndDate, Period),
    IRequest<InventoryAnalyticsDashboardResponse>;

public sealed class GetInventoryAnalyticsDashboardQueryHandler
    : IRequestHandler<GetInventoryAnalyticsDashboardQuery, InventoryAnalyticsDashboardResponse>
{
    private readonly IDashboardDateRangeResolver _dateRangeResolver;
    private readonly IAnalyticsService _analyticsService;
    private readonly IDashboardCacheService _cacheService;

    public GetInventoryAnalyticsDashboardQueryHandler(
        IDashboardDateRangeResolver dateRangeResolver,
        IAnalyticsService analyticsService,
        IDashboardCacheService cacheService)
    {
        _dateRangeResolver = dateRangeResolver;
        _analyticsService = analyticsService;
        _cacheService = cacheService;
    }

    public Task<InventoryAnalyticsDashboardResponse> Handle(
        GetInventoryAnalyticsDashboardQuery request,
        CancellationToken cancellationToken)
    {
        var dateRange = _dateRangeResolver.Resolve(request.StartDate, request.EndDate, request.Period);

        return _cacheService.GetOrCreateAsync(
            $"inventory:{dateRange.CacheKeySuffix}",
            ct => _analyticsService.GetInventoryAnalyticsAsync(dateRange, ct),
            cancellationToken);
    }
}
