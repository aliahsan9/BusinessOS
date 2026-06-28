using BusinessOS.Application.Features.Dashboard.DTOs;
using BusinessOS.Application.Features.Dashboard.Models;
using BusinessOS.Application.Features.Dashboard.Services;
using FluentValidation;
using MediatR;

namespace BusinessOS.Application.Features.Dashboard.Queries.GetChartData;

public sealed record GetChartDataQuery(
    string ChartType,
    DateTime? StartDate,
    DateTime? EndDate,
    string? Period,
    int Top = 10) : IRequest<ChartDataResponse>;

public sealed class GetChartDataQueryHandler : IRequestHandler<GetChartDataQuery, ChartDataResponse>
{
    private readonly IDashboardDateRangeResolver _dateRangeResolver;
    private readonly IReportingService _reportingService;
    private readonly IDashboardCacheService _cacheService;

    public GetChartDataQueryHandler(
        IDashboardDateRangeResolver dateRangeResolver,
        IReportingService reportingService,
        IDashboardCacheService cacheService)
    {
        _dateRangeResolver = dateRangeResolver;
        _reportingService = reportingService;
        _cacheService = cacheService;
    }

    public Task<ChartDataResponse> Handle(
        GetChartDataQuery request,
        CancellationToken cancellationToken)
    {
        var dateRange = _dateRangeResolver.Resolve(request.StartDate, request.EndDate, request.Period);
        var chartType = request.ChartType.ToLowerInvariant();

        return _cacheService.GetOrCreateAsync(
            $"chart:{chartType}:{request.Top}:{dateRange.CacheKeySuffix}",
            ct => _reportingService.GetChartDataAsync(chartType, dateRange, request.Top, ct),
            cancellationToken);
    }
}

public sealed class GetChartDataQueryValidator : AbstractValidator<GetChartDataQuery>
{
    public GetChartDataQueryValidator()
    {
        RuleFor(x => x.ChartType)
            .NotEmpty()
            .Must(x => ChartTypes.All.Contains(x.ToLowerInvariant()))
            .WithMessage("Invalid chart type.");

        RuleFor(x => x.Period)
            .Must(DateRangePeriods.IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.Period))
            .WithMessage("Invalid period. Valid values: today, week, month, year, all, custom.");

        RuleFor(x => x)
            .Must(x => x.StartDate.HasValue && x.EndDate.HasValue)
            .When(x => string.Equals(x.Period, DateRangePeriods.Custom, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Both startDate and endDate are required when period is custom.");

        RuleFor(x => x)
            .Must(x => !x.StartDate.HasValue || !x.EndDate.HasValue || x.StartDate <= x.EndDate)
            .WithMessage("startDate must be less than or equal to endDate.");

        RuleFor(x => x.Top)
            .Must(x => x is 10 or 20)
            .WithMessage("Top must be 10 or 20.");
    }
}
