namespace BusinessOS.Application.Features.FinanceAgent;

public interface IFinanceAgentService
{
    Task<FinanceOverviewDto> GetOverviewAsync(DateTime? startDate = null, DateTime? endDate = null, string? period = null, CancellationToken cancellationToken = default);
    Task<FinanceIntelligenceDto> GetIntelligenceAsync(DateTime? startDate = null, DateTime? endDate = null, string? period = null, CancellationToken cancellationToken = default);
    Task<ExpenseIntelligenceDto> AnalyzeExpensesAsync(DateTime? startDate = null, DateTime? endDate = null, string? period = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseCategorySuggestionDto>> SuggestExpenseCategoriesAsync(CancellationToken cancellationToken = default);
    Task<ReceivablesIntelligenceDto> GetReceivablesAsync(CancellationToken cancellationToken = default);
    Task<PayablesIntelligenceDto> GetPayablesAsync(CancellationToken cancellationToken = default);
    Task<CashFlowIntelligenceDto> GetCashFlowAsync(DateTime? startDate = null, DateTime? endDate = null, int projectionDays = 30, CancellationToken cancellationToken = default);
    Task<ReconciliationSummaryDto> ReconcileAsync(CancellationToken cancellationToken = default);
    Task<BankImportResultDto> ImportBankStatementCsvAsync(BankStatementImportRequest request, CancellationToken cancellationToken = default);
    Task<FinanceReportDto> GenerateReportAsync(FinanceReportRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceBudgetDto>> GetBudgetsAsync(CancellationToken cancellationToken = default);
    Task<FinanceBudgetDto> UpsertBudgetAsync(FinanceBudgetWriteRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BudgetAlertDto>> GetBudgetAlertsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceAlertDto>> GetAlertsAsync(CancellationToken cancellationToken = default);
    Task<PreparedReminderDto> PreparePaymentReminderAsync(PrepareReminderRequest request, CancellationToken cancellationToken = default);
    Task<PreparedReminderDto> SubmitPaymentReminderAsync(Guid reminderId, bool approved, string? approvalFingerprint, string? idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceReminderDto>> GetRemindersAsync(CancellationToken cancellationToken = default);
    Task<FinanceAutomationSettingsDto> GetAutomationSettingsAsync(CancellationToken cancellationToken = default);
    Task<FinanceAutomationSettingsDto> SaveAutomationSettingsAsync(FinanceAutomationSettingsWriteRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceActivityDto>> GetActivityAsync(CancellationToken cancellationToken = default);
    Task RunScheduledMonitorAsync(CancellationToken cancellationToken = default);
}

public sealed class FinanceReportRequest
{
    public string ReportType { get; set; } = "daily-summary";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Period { get; set; }
}

public sealed class PrepareReminderRequest
{
    public Guid InvoiceId { get; set; }
    public string? Channel { get; set; }
    public string? CustomMessage { get; set; }
    public string? IdempotencyKey { get; set; }
}

public sealed class BankStatementImportRequest
{
    public string FileName { get; set; } = "statement.csv";
    public string CsvContent { get; set; } = default!;
    public string? Notes { get; set; }
}

public sealed class FinanceBudgetWriteRequest
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = default!;
    public Guid? ExpenseCategoryId { get; set; }
    public string? CategoryName { get; set; }
    public decimal Amount { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public bool IsRecommendation { get; set; }
}

public sealed class FinanceAutomationSettingsWriteRequest
{
    public bool? DailyFinanceSummary { get; set; }
    public bool? WeeklyFinancialReport { get; set; }
    public bool? OverdueInvoiceMonitoring { get; set; }
    public bool? PaymentReminderPreparation { get; set; }
    public bool? UpcomingPayableAlerts { get; set; }
    public bool? ExpenseAnomalyDetection { get; set; }
    public bool? CashFlowAnalysis { get; set; }
    public bool? BudgetMonitoring { get; set; }
    public bool? ReconciliationChecks { get; set; }
    public string? ReminderScheduleDays { get; set; }
    public string? ReminderChannel { get; set; }
    public bool? AutoSendReminders { get; set; }
    public decimal? AnomalyVariancePercent { get; set; }
    public int? PayableAlertDays { get; set; }
    public int? MinimumCashFlowHistoryMonths { get; set; }
    public string? TimeZoneId { get; set; }
    public string? ReportingBasis { get; set; }
}

public sealed record FinanceOverviewDto(
    string Currency,
    string TimeZoneId,
    string Period,
    DateTime StartDate,
    DateTime EndDate,
    decimal? SalesRevenue,
    decimal? CollectedCash,
    decimal? TotalExpenses,
    decimal? GrossProfit,
    decimal? NetProfit,
    decimal? OutstandingReceivables,
    decimal? OutstandingPayables,
    int OverdueInvoiceCount,
    decimal? CashBalance,
    bool CashBalanceAvailable,
    string DataClassification,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<string> Formulas);

public sealed record FinanceIntelligenceDto(
    string Currency,
    string TimeZoneId,
    string Period,
    DateTime StartDate,
    DateTime EndDate,
    MetricValueDto SalesRevenue,
    MetricValueDto CollectedCash,
    MetricValueDto OtherIncome,
    MetricValueDto TotalExpenses,
    MetricValueDto CostOfGoodsSold,
    MetricValueDto GrossProfit,
    MetricValueDto NetProfit,
    MetricValueDto OutstandingReceivables,
    MetricValueDto OutstandingPayables,
    MetricValueDto OverdueReceivables,
    MetricValueDto CashBalance,
    IReadOnlyList<TrendPointDto> MonthlyTrends,
    IReadOnlyList<ProfitabilityItemDto> ProfitabilityByProduct,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<string> Formulas,
    string DataClassification);

public sealed record MetricValueDto(
    bool Available,
    decimal? Value,
    string Basis,
    string? Limitation = null);

public sealed record TrendPointDto(
    string PeriodLabel,
    decimal SalesRevenue,
    decimal CollectedCash,
    decimal Expenses,
    decimal? GrossProfit,
    decimal? NetProfit);

public sealed record ProfitabilityItemDto(
    Guid? ProductId,
    string Name,
    decimal Revenue,
    decimal? Cost,
    decimal? GrossProfit,
    string Confidence,
    string Limitation);

public sealed record ExpenseIntelligenceDto(
    string Currency,
    DateTime StartDate,
    DateTime EndDate,
    decimal TotalRecognizedExpenses,
    decimal PriorPeriodTotal,
    decimal? ChangePercent,
    IReadOnlyList<ExpenseCategoryInsightDto> Categories,
    IReadOnlyList<RecurringExpenseDto> Recurring,
    IReadOnlyList<ExpenseAnomalyDto> Anomalies,
    IReadOnlyList<DuplicateExpenseHintDto> DuplicateHints,
    IReadOnlyList<ExpenseCategorySuggestionDto> CategorySuggestions,
    IReadOnlyList<string> Limitations);

public sealed record ExpenseCategoryInsightDto(
    Guid? CategoryId,
    string CategoryName,
    decimal Amount,
    decimal PriorAmount,
    decimal? ChangePercent,
    int Count,
    string Explanation);

public sealed record RecurringExpenseDto(
    Guid ExpenseId,
    string Title,
    decimal Amount,
    string CategoryName,
    string? RecurrencePattern,
    DateTime ExpenseDate);

public sealed record ExpenseAnomalyDto(
    Guid ExpenseId,
    string Title,
    string CategoryName,
    decimal Amount,
    decimal CategoryAverage,
    decimal VariancePercent,
    string Explanation);

public sealed record DuplicateExpenseHintDto(
    Guid ExpenseId,
    Guid PossibleDuplicateOfId,
    string Title,
    decimal Amount,
    DateTime ExpenseDate,
    string Reason);

public sealed record ExpenseCategorySuggestionDto(
    Guid ExpenseId,
    string Title,
    string? Vendor,
    Guid? SuggestedCategoryId,
    string SuggestedCategoryName,
    string Confidence,
    string Reason,
    bool Applied);

public sealed record ReceivablesIntelligenceDto(
    string Currency,
    decimal TotalOutstanding,
    decimal OverdueTotal,
    int OverdueCount,
    IReadOnlyList<AgingBucketDto> Aging,
    IReadOnlyList<ReceivableItemDto> Items,
    IReadOnlyList<LatePayingCustomerDto> LatePayers,
    IReadOnlyList<string> Limitations);

public sealed record AgingBucketDto(string Label, decimal Amount, int Count);

public sealed record ReceivableItemDto(
    Guid InvoiceId,
    string InvoiceNumber,
    Guid CustomerId,
    string CustomerName,
    DateTime InvoiceDate,
    DateTime DueDate,
    decimal GrandTotal,
    decimal AmountPaid,
    decimal OutstandingAmount,
    int DaysOverdue,
    string Status,
    int PriorityScore);

public sealed record LatePayingCustomerDto(
    Guid CustomerId,
    string CustomerName,
    int OverdueInvoiceCount,
    decimal OverdueAmount,
    int TimesLate);

public sealed record PayablesIntelligenceDto(
    string Currency,
    decimal TotalOutstanding,
    decimal OverdueTotal,
    decimal DueSoonTotal,
    IReadOnlyList<PayableItemDto> Items,
    IReadOnlyList<PaymentScheduleItemDto> RecommendedSchedule,
    IReadOnlyList<string> Limitations);

public sealed record PayableItemDto(
    Guid PurchaseInvoiceId,
    string InvoiceNumber,
    Guid SupplierId,
    string SupplierName,
    DateTime IssueDate,
    DateTime DueDate,
    decimal Total,
    decimal AmountPaid,
    decimal Outstanding,
    int DaysUntilDue,
    bool IsOverdue,
    string Status,
    int PriorityScore);

public sealed record PaymentScheduleItemDto(
    Guid PurchaseInvoiceId,
    string InvoiceNumber,
    string SupplierName,
    DateTime SuggestedPayDate,
    decimal Amount,
    string Rationale);

public sealed record CashFlowIntelligenceDto(
    string Currency,
    DateTime StartDate,
    DateTime EndDate,
    bool OpeningBalanceConfigured,
    decimal? OpeningBalance,
    decimal Inflows,
    decimal Outflows,
    decimal? ClosingBalance,
    bool ClosingBalanceAvailable,
    decimal ExpectedReceivableInflows,
    decimal ExpectedPayableOutflows,
    bool ProjectionAvailable,
    string ProjectionConfidence,
    IReadOnlyList<CashFlowProjectionPointDto> Projection,
    IReadOnlyList<string> ShortageWarnings,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Limitations,
    string DataClassification);

public sealed record CashFlowProjectionPointDto(
    DateTime Date,
    decimal ProjectedInflow,
    decimal ProjectedOutflow,
    decimal? ProjectedBalance,
    string Confidence);

public sealed record ReconciliationSummaryDto(
    int InvoicePaymentMismatches,
    int PurchaseBillPaymentMismatches,
    int PossibleDuplicatePayments,
    int UnmatchedBankLines,
    int PartialPayments,
    IReadOnlyList<ReconciliationIssueDto> Issues,
    IReadOnlyList<string> Limitations,
    string BankSyncClassification);

public sealed record ReconciliationIssueDto(
    string Kind,
    string Summary,
    Guid? InvoiceId,
    Guid? PaymentId,
    Guid? PurchaseInvoiceId,
    Guid? ExpenseId,
    Guid? BankLineId,
    decimal? Amount,
    string RecommendedAction);

public sealed record BankImportResultDto(
    Guid BatchId,
    int Imported,
    int DuplicatesSkipped,
    string Classification,
    string Summary);

public sealed record FinanceReportDto(
    string ReportType,
    string Currency,
    string TimeZoneId,
    DateTime StartDate,
    DateTime EndDate,
    string CalculationBasis,
    string BodyMarkdown,
    object Payload,
    IReadOnlyList<string> Limitations,
    string Disclaimer);

public sealed record FinanceBudgetDto(
    Guid Id,
    string Name,
    Guid? ExpenseCategoryId,
    string? CategoryName,
    decimal Amount,
    string Currency,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal ActualSpend,
    decimal Remaining,
    decimal UtilizationPercent,
    bool IsOverBudget,
    bool IsRecommendation,
    string Status);

public sealed record BudgetAlertDto(
    Guid BudgetId,
    string BudgetName,
    string Severity,
    string Message,
    decimal Amount,
    decimal ActualSpend,
    decimal UtilizationPercent);

public sealed record FinanceAlertDto(
    Guid Id,
    string Kind,
    string Title,
    string Summary,
    string Severity,
    string Status,
    DateTime CreatedAt);

public sealed record PreparedReminderDto(
    Guid ReminderId,
    Guid InvoiceId,
    string Status,
    string ApprovalFingerprint,
    bool RequiresApproval,
    bool Created,
    bool DuplicatePrevented,
    string Subject,
    string Body,
    string Channel,
    decimal OutstandingAmount,
    int DaysOverdue,
    string Summary,
    string IntegrationClassification);

public sealed record FinanceReminderDto(
    Guid Id,
    Guid InvoiceId,
    string InvoiceNumber,
    Guid CustomerId,
    string CustomerName,
    string Channel,
    string Status,
    string Subject,
    decimal OutstandingAmount,
    int DaysOverdue,
    DateTime CreatedAt,
    DateTime? SentAt);

public sealed record FinanceAutomationSettingsDto(
    bool DailyFinanceSummary,
    bool WeeklyFinancialReport,
    bool OverdueInvoiceMonitoring,
    bool PaymentReminderPreparation,
    bool UpcomingPayableAlerts,
    bool ExpenseAnomalyDetection,
    bool CashFlowAnalysis,
    bool BudgetMonitoring,
    bool ReconciliationChecks,
    string ReminderScheduleDays,
    string ReminderChannel,
    bool AutoSendReminders,
    decimal AnomalyVariancePercent,
    int PayableAlertDays,
    int MinimumCashFlowHistoryMonths,
    string TimeZoneId,
    string ReportingBasis,
    DateTime? UpdatedAt,
    DateTime? LastMonitorRunAt);

public sealed record FinanceActivityDto(
    Guid Id,
    string Kind,
    string Title,
    string Summary,
    string Severity,
    string Status,
    DateTime CreatedAt);
