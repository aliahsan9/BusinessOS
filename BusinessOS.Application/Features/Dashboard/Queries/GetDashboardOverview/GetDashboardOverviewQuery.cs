using BusinessOS.Application.Features.Dashboard.DTOs;
using BusinessOS.Application.Features.Dashboard.Services;
using BusinessOS.Application.Features.Dashboard.Validators;
using MediatR;

namespace BusinessOS.Application.Features.Dashboard.Queries.GetDashboardOverview;

public sealed record GetDashboardOverviewQuery(
    DateTime? StartDate,
    DateTime? EndDate,
    string? Period) : DashboardDateRangeQuery(StartDate, EndDate, Period),
    IRequest<DashboardOverviewResponse>;

public sealed class GetDashboardOverviewQueryHandler
    : IRequestHandler<GetDashboardOverviewQuery, DashboardOverviewResponse>
{
    private readonly IDashboardDateRangeResolver _dateRangeResolver;
    private readonly IDashboardService _dashboardService;
    private readonly IDashboardCacheService _cacheService;

    public GetDashboardOverviewQueryHandler(
        IDashboardDateRangeResolver dateRangeResolver,
        IDashboardService dashboardService,
        IDashboardCacheService cacheService)
    {
        _dateRangeResolver = dateRangeResolver;
        _dashboardService = dashboardService;
        _cacheService = cacheService;
    }

    public Task<DashboardOverviewResponse> Handle(
        GetDashboardOverviewQuery request,
        CancellationToken cancellationToken)
    {
        var dateRange = _dateRangeResolver.Resolve(request.StartDate, request.EndDate, request.Period);

        return _cacheService.GetOrCreateAsync(
            $"overview:{dateRange.CacheKeySuffix}",
            ct => _dashboardService.GetOverviewAsync(dateRange, ct),
            cancellationToken);
    }
}
