using BusinessOS.Application.Features.FinanceAgent;
using BusinessOS.Application.Features.ProcurementAgent;

namespace BusinessOS.Application.Features.BusinessIntelligence;

// ── Central BI ──────────────────────────────────────────────────────────────

public interface IBusinessIntelligenceService
{
    Task<BusinessHealthOverviewDto> GetHealthOverviewAsync(DateTime? startDate = null, DateTime? endDate = null, string? period = null, CancellationToken cancellationToken = default);
    Task<RevenueForecastDto> ForecastRevenueAsync(int horizonMonths = 1, CancellationToken cancellationToken = default);
    Task<PeriodComparisonDto> ComparePeriodsAsync(DateTime? startDate = null, DateTime? endDate = null, string? period = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BusinessRiskDto>> GetRisksAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BiRecommendationDto>> GetRecommendationsAsync(CancellationToken cancellationToken = default);
    Task<ExecutiveDashboardDto> GetExecutiveDashboardAsync(string? period = null, CancellationToken cancellationToken = default);
}

public interface ISalesIntelligenceService
{
    Task<SalesIntelligenceDto> GetIntelligenceAsync(DateTime? startDate = null, DateTime? endDate = null, string? period = null, CancellationToken cancellationToken = default);
    Task<RevenueForecastDto> ForecastSalesAsync(int horizonMonths = 1, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductPerformanceDto>> GetProductPerformanceAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DecliningProductDto>> GetDecliningProductsAsync(CancellationToken cancellationToken = default);
    Task<ConversionAnalyticsDto> GetConversionAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BiRecommendationDto>> GetRevenueOpportunitiesAsync(CancellationToken cancellationToken = default);
}

public interface ICustomerIntelligenceService
{
    Task<CustomerIntelligenceDto> GetIntelligenceAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerSegmentInsightDto>> GetBehavioralSegmentsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChurnRiskCustomerDto>> GetChurnRisksAsync(CancellationToken cancellationToken = default);
    Task<CustomerRetentionDto> GetRetentionAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BiRecommendationDto>> GetRetentionStrategiesAsync(CancellationToken cancellationToken = default);
}

public interface IExecutiveIntelligenceService
{
    Task<DecisionBriefDto> AnswerAsync(string question, CancellationToken cancellationToken = default);
    Task<DecisionBriefDto> BusinessPerformanceAsync(CancellationToken cancellationToken = default);
    Task<DecisionBriefDto> ProductsToPromoteAsync(CancellationToken cancellationToken = default);
    Task<DecisionBriefDto> ImmediateProblemsAsync(CancellationToken cancellationToken = default);
    Task<DecisionBriefDto> CustomersAtRiskAsync(CancellationToken cancellationToken = default);
    Task<DecisionBriefDto> InventoryPurchaseAdviceAsync(CancellationToken cancellationToken = default);
    Task<DecisionBriefDto> ExpectedRevenueNextMonthAsync(CancellationToken cancellationToken = default);
    Task<DecisionBriefDto> WhereLosingMoneyAsync(CancellationToken cancellationToken = default);
    Task<RecommendedTaskResultDto> CreateRecommendedTaskAsync(CreateRecommendedTaskRequest request, CancellationToken cancellationToken = default);
}

public interface IPredictionAccuracyService
{
    Task RecordForecastAsync(PredictionRecordRequest request, CancellationToken cancellationToken = default);
    Task EvaluateDuePredictionsAsync(CancellationToken cancellationToken = default);
    Task<PredictionAccuracySummaryDto> GetAccuracySummaryAsync(CancellationToken cancellationToken = default);
}

public sealed class CreateRecommendedTaskRequest
{
    public string AgentKey { get; set; } = "analyst";
    public string Objective { get; set; } = default!;
    public string? RecommendationId { get; set; }
    public bool RequiresApproval { get; set; }
}

public sealed class PredictionRecordRequest
{
    public string MetricKey { get; set; } = default!;
    public string Method { get; set; } = default!;
    public decimal PredictedValue { get; set; }
    public string Confidence { get; set; } = "low";
    public DateTime TargetPeriodStart { get; set; }
    public DateTime TargetPeriodEnd { get; set; }
    public string? AssumptionsJson { get; set; }
}

// ── Shared DTOs ─────────────────────────────────────────────────────────────

public sealed record BiMetricDto(
    decimal? Value,
    bool Available,
    bool IsPrediction,
    string Confidence,
    string? Basis);

public sealed record HistoricalPointDto(DateTime PeriodStart, decimal ActualValue);

public sealed record ForecastHorizonPointDto(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal PredictedValue,
    string Confidence,
    bool IsPrediction);

public sealed record RevenueForecastDto(
    string Currency,
    bool Available,
    string Confidence,
    string Method,
    decimal? NextPeriodPredicted,
    IReadOnlyList<HistoricalPointDto> History,
    IReadOnlyList<ForecastHorizonPointDto> Horizon,
    decimal? BacktestMapePercent,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Limitations,
    string DataClassification,
    bool IsPrediction);

public sealed record PeriodComparisonDto(
    string Currency,
    DateTime CurrentStart,
    DateTime CurrentEnd,
    DateTime PriorStart,
    DateTime PriorEnd,
    decimal CurrentRevenue,
    decimal PriorRevenue,
    decimal? RevenueChangePercent,
    decimal CurrentOrderCount,
    decimal PriorOrderCount,
    decimal CurrentExpenses,
    decimal PriorExpenses,
    decimal? ExpenseChangePercent,
    string DataClassification);

public sealed record BusinessRiskDto(
    string Category,
    string Severity,
    string Title,
    string Explanation,
    IReadOnlyList<string> SupportingMetrics,
    string? SuggestedAgentKey,
    bool IsPrediction);

public sealed record BiRecommendationDto(
    string Id,
    string Category,
    string Title,
    string Explanation,
    IReadOnlyList<string> SupportingMetrics,
    string? SuggestedAgentKey,
    string? SuggestedObjective,
    bool RequiresHumanApproval,
    bool IsPrediction,
    string Confidence);

public sealed record BusinessHealthOverviewDto(
    string Currency,
    string Period,
    DateTime StartDate,
    DateTime EndDate,
    BiMetricDto SalesRevenue,
    BiMetricDto CollectedCash,
    BiMetricDto Expenses,
    BiMetricDto GrossProfit,
    BiMetricDto OutstandingReceivables,
    BiMetricDto OutstandingPayables,
    int LowStockCount,
    int ChurnRiskCount,
    int OverdueInvoiceCount,
    string OverallHealthLabel,
    IReadOnlyList<string> Formulas,
    IReadOnlyList<string> Limitations,
    string DataClassification);

public sealed record ProductPerformanceDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal UnitsSold,
    decimal Revenue,
    decimal? PriorPeriodRevenue,
    decimal? ChangePercent,
    string Trend,
    string Confidence);

public sealed record DecliningProductDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal CurrentRevenue,
    decimal PriorRevenue,
    decimal DeclinePercent,
    string Explanation);

public sealed record ConversionAnalyticsDto(
    int TotalLeads,
    int ConvertedLeads,
    decimal LeadConversionRate,
    int CompletedOrders,
    int CancelledOrders,
    decimal OrderCompletionRate,
    string DataClassification,
    IReadOnlyList<string> Limitations);

public sealed record SalesIntelligenceDto(
    string Currency,
    string Period,
    DateTime StartDate,
    DateTime EndDate,
    decimal SalesRevenue,
    decimal AverageOrderValue,
    int CompletedOrders,
    RevenueForecastDto Forecast,
    IReadOnlyList<ProductPerformanceDto> TopProducts,
    IReadOnlyList<DecliningProductDto> DecliningProducts,
    ConversionAnalyticsDto Conversion,
    IReadOnlyList<BiRecommendationDto> Opportunities,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Limitations,
    string DataClassification);

public sealed record CustomerSegmentInsightDto(
    string SegmentKey,
    string Label,
    int CustomerCount,
    decimal TotalRevenue,
    string Explanation);

public sealed record ChurnRiskCustomerDto(
    Guid CustomerId,
    string CustomerName,
    string Email,
    DateTime? LastOrderDate,
    int DaysSinceLastOrder,
    int LifetimeOrders,
    decimal LifetimeRevenue,
    string RiskLevel,
    string Explanation,
    bool IsPrediction);

public sealed record CustomerRetentionDto(
    int ActiveCustomers,
    int RepeatCustomers,
    decimal RepeatRate,
    int AtRiskCustomers,
    int LostCustomers,
    decimal? RetentionEstimatePercent,
    string Confidence,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Limitations,
    bool IsPrediction);

public sealed record CustomerIntelligenceDto(
    CustomerRetentionDto Retention,
    IReadOnlyList<CustomerSegmentInsightDto> Segments,
    IReadOnlyList<ChurnRiskCustomerDto> ChurnRisks,
    IReadOnlyList<BiRecommendationDto> RetentionStrategies,
    string DataClassification);

public sealed record InventoryTurnoverDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal UnitsSold90Days,
    decimal AverageStock,
    decimal? TurnoverRatio,
    decimal? DaysOfInventory,
    int? EstimatedLeadTimeDays,
    string Confidence,
    string Explanation);

public sealed record DecisionBriefDto(
    string QuestionKey,
    string Question,
    string Answer,
    IReadOnlyList<string> SupportingMetrics,
    IReadOnlyList<BiRecommendationDto> RecommendedActions,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Limitations,
    string Confidence,
    bool ContainsPredictions,
    string DataClassification);

public sealed record RecommendedTaskResultDto(
    bool Created,
    Guid? TaskId,
    string Status,
    string Message,
    bool RequiresHumanApproval);

public sealed record PredictionAccuracySummaryDto(
    int TotalRecorded,
    int Evaluated,
    int Pending,
    decimal? AverageAbsolutePercentError,
    IReadOnlyList<PredictionAccuracyRowDto> Recent,
    string DataClassification);

public sealed record PredictionAccuracyRowDto(
    Guid Id,
    string MetricKey,
    string Method,
    decimal PredictedValue,
    decimal? ActualValue,
    decimal? AbsolutePercentError,
    string Confidence,
    DateTime TargetPeriodStart,
    DateTime TargetPeriodEnd,
    DateTime? EvaluatedAt,
    string Status);

public sealed record ExecutiveDashboardDto(
    BusinessHealthOverviewDto Health,
    RevenueForecastDto RevenueForecast,
    PeriodComparisonDto PeriodComparison,
    SalesIntelligenceDto Sales,
    CustomerIntelligenceDto Customers,
    IReadOnlyList<InventoryRiskDto> InventoryRisks,
    IReadOnlyList<DemandInsightDto> InventoryDemand,
    IReadOnlyList<InventoryTurnoverDto> InventoryTurnover,
    CashFlowIntelligenceDto? CashFlow,
    FinanceIntelligenceDto? Finance,
    IReadOnlyList<BusinessRiskDto> Risks,
    IReadOnlyList<BiRecommendationDto> Recommendations,
    PredictionAccuracySummaryDto PredictionAccuracy,
    string DataClassification,
    IReadOnlyList<string> GlobalLimitations);
