using BusinessOS.Application.Features.Dashboard.Queries.GetCustomerAnalytics;
using BusinessOS.Application.Features.Dashboard.Queries.GetDashboardOverview;
using BusinessOS.Application.Features.Dashboard.Queries.GetInventoryAnalytics;
using BusinessOS.Application.Features.Dashboard.Queries.GetOrderAnalytics;
using BusinessOS.Application.Features.Dashboard.Queries.GetProductAnalytics;
using BusinessOS.Application.Features.Dashboard.Queries.GetSalesAnalytics;
using BusinessOS.Application.Features.Dashboard.Validators;
using FluentValidation;

namespace BusinessOS.Application.Features.Dashboard.Validators;

public sealed class GetDashboardOverviewQueryValidator : AbstractValidator<GetDashboardOverviewQuery>
{
    public GetDashboardOverviewQueryValidator() => Include(new DashboardDateRangeQueryValidator());
}

public sealed class GetSalesAnalyticsQueryValidator : AbstractValidator<GetSalesAnalyticsQuery>
{
    public GetSalesAnalyticsQueryValidator() => Include(new DashboardDateRangeQueryValidator());
}

public sealed class GetCustomerAnalyticsDashboardQueryValidator
    : AbstractValidator<GetCustomerAnalyticsDashboardQuery>
{
    public GetCustomerAnalyticsDashboardQueryValidator() => Include(new DashboardDateRangeQueryValidator());
}

public sealed class GetProductAnalyticsDashboardQueryValidator
    : AbstractValidator<GetProductAnalyticsDashboardQuery>
{
    public GetProductAnalyticsDashboardQueryValidator() => Include(new DashboardTopLimitQueryValidator());
}

public sealed class GetInventoryAnalyticsDashboardQueryValidator
    : AbstractValidator<GetInventoryAnalyticsDashboardQuery>
{
    public GetInventoryAnalyticsDashboardQueryValidator() => Include(new DashboardDateRangeQueryValidator());
}

public sealed class GetOrderAnalyticsQueryValidator : AbstractValidator<GetOrderAnalyticsQuery>
{
    public GetOrderAnalyticsQueryValidator() => Include(new DashboardDateRangeQueryValidator());
}
