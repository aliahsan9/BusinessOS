using BusinessOS.Application.Features.Dashboard.DTOs;
using BusinessOS.Application.Features.Dashboard.Services;
using BusinessOS.Application.Features.Dashboard.Validators;
using MediatR;

namespace BusinessOS.Application.Features.Dashboard.Queries.GetProductAnalytics;

public sealed record GetProductAnalyticsDashboardQuery(
    DateTime? StartDate,
    DateTime? EndDate,
    string? Period,
    int Top = 10) : DashboardTopLimitQuery(StartDate, EndDate, Period, Top),
    IRequest<ProductAnalyticsResponse>;

public sealed class GetProductAnalyticsDashboardQueryHandler
    : IRequestHandler<GetProductAnalyticsDashboardQuery, ProductAnalyticsResponse>
{
    private readonly IDashboardDateRangeResolver _dateRangeResolver;
    private readonly IAnalyticsService _analyticsService;
    private readonly IDashboardCacheService _cacheService;

    public GetProductAnalyticsDashboardQueryHandler(
        IDashboardDateRangeResolver dateRangeResolver,
        IAnalyticsService analyticsService,
        IDashboardCacheService cacheService)
    {
        _dateRangeResolver = dateRangeResolver;
        _analyticsService = analyticsService;
        _cacheService = cacheService;
    }

    public Task<ProductAnalyticsResponse> Handle(
        GetProductAnalyticsDashboardQuery request,
        CancellationToken cancellationToken)
    {
        var dateRange = _dateRangeResolver.Resolve(request.StartDate, request.EndDate, request.Period);

        return _cacheService.GetOrCreateAsync(
            $"products:{request.Top}:{dateRange.CacheKeySuffix}",
            ct => _analyticsService.GetProductAnalyticsAsync(dateRange, request.Top, ct),
            cancellationToken);
    }
}
