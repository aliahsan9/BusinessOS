using BusinessOS.Application.Features.Dashboard.Models;
using FluentValidation;

namespace BusinessOS.Application.Features.Dashboard.Validators;

public sealed class DashboardDateRangeQueryValidator : AbstractValidator<DashboardDateRangeQuery>
{
    public DashboardDateRangeQueryValidator()
    {
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
    }
}

public sealed class DashboardTopLimitQueryValidator : AbstractValidator<DashboardTopLimitQuery>
{
    private static readonly int[] AllowedTopLimits = [10, 20];

    public DashboardTopLimitQueryValidator()
    {
        Include(new DashboardDateRangeQueryValidator());

        RuleFor(x => x.Top)
            .Must(x => AllowedTopLimits.Contains(x))
            .WithMessage("Top must be 10 or 20.");
    }
}

public record DashboardDateRangeQuery(DateTime? StartDate, DateTime? EndDate, string? Period);

public record DashboardTopLimitQuery(DateTime? StartDate, DateTime? EndDate, string? Period, int Top = 10)
    : DashboardDateRangeQuery(StartDate, EndDate, Period);

public sealed class ChartDataQueryValidator : AbstractValidator<ChartDataQuery>
{
    public ChartDataQueryValidator()
    {
        Include(new DashboardTopLimitQueryValidator());

        RuleFor(x => x.ChartType)
            .NotEmpty()
            .Must(x => DTOs.ChartTypes.All.Contains(x.ToLowerInvariant()))
            .WithMessage("Invalid chart type.");
    }
}

public record ChartDataQuery(
    string ChartType,
    DateTime? StartDate,
    DateTime? EndDate,
    string? Period,
    int Top = 10) : DashboardTopLimitQuery(StartDate, EndDate, Period, Top);
