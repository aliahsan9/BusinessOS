using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Application.Features.Analytics.Services;

namespace BusinessOS.Infrastructure.AI.Copilot;

public sealed class AiAnalyticsQueryService : IAiAnalyticsQueryService
{
    private readonly IAnalyticsModuleService _analytics;

    public AiAnalyticsQueryService(IAnalyticsModuleService analytics)
    {
        _analytics = analytics;
    }

    public IReadOnlyList<string> SupportedQueryTypes { get; } =
    [
        "revenue-by-month",
        "revenue-by-year",
        "orders-by-month",
        "customer-growth",
        "expense-growth",
        "top-customers",
        "sales-summary",
        "profit-trend",
        "overview"
    ];

    public async Task<AiAnalyticsQueryResponse> ExecuteAsync(
        string queryType,
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        int? top,
        CancellationToken cancellationToken = default)
    {
        var normalized = queryType.Trim().ToLowerInvariant();
        var periodLabel = period ?? "custom";

        return normalized switch
        {
            "revenue-by-month" or "revenue-by-year" => new AiAnalyticsQueryResponse
            {
                QueryType = normalized,
                PeriodLabel = periodLabel,
                Data = await _analytics.GetRevenueChartAsync(startDate, endDate, period, cancellationToken)
            },
            "orders-by-month" => new AiAnalyticsQueryResponse
            {
                QueryType = normalized,
                PeriodLabel = periodLabel,
                Data = await _analytics.GetOverviewAsync(startDate, endDate, period, cancellationToken)
            },
            "customer-growth" => new AiAnalyticsQueryResponse
            {
                QueryType = normalized,
                PeriodLabel = periodLabel,
                Data = await _analytics.GetCustomerGrowthChartAsync(startDate, endDate, period, cancellationToken)
            },
            "expense-growth" => new AiAnalyticsQueryResponse
            {
                QueryType = normalized,
                PeriodLabel = periodLabel,
                Data = await _analytics.GetExpenseChartAsync(startDate, endDate, period, cancellationToken)
            },
            "top-customers" => new AiAnalyticsQueryResponse
            {
                QueryType = normalized,
                PeriodLabel = periodLabel,
                Data = await _analytics.GetTopCustomersAsync(startDate, endDate, period, top ?? 10, cancellationToken)
            },
            "profit-trend" => new AiAnalyticsQueryResponse
            {
                QueryType = normalized,
                PeriodLabel = periodLabel,
                Data = await _analytics.GetProfitChartAsync(startDate, endDate, period, cancellationToken)
            },
            "overview" => new AiAnalyticsQueryResponse
            {
                QueryType = normalized,
                PeriodLabel = periodLabel,
                Data = await _analytics.GetOverviewAsync(startDate, endDate, period, cancellationToken)
            },
            "sales-summary" => new AiAnalyticsQueryResponse
            {
                QueryType = normalized,
                PeriodLabel = periodLabel,
                Data = await _analytics.GetProjectAnalyticsAsync(startDate, endDate, period, cancellationToken)
            },
            _ => throw new ArgumentException($"Unsupported analytics query type: {queryType}")
        };
    }
}
