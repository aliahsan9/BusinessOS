using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Tenant-scoped automation and reminder preferences for the Finance agent.
/// </summary>
public class FinanceAgentSettings : AuditableEntity
{
    public Guid TenantId { get; set; }

    public bool DailyFinanceSummary { get; set; } = true;
    public bool WeeklyFinancialReport { get; set; } = true;
    public bool OverdueInvoiceMonitoring { get; set; } = true;
    public bool PaymentReminderPreparation { get; set; } = true;
    public bool UpcomingPayableAlerts { get; set; } = true;
    public bool ExpenseAnomalyDetection { get; set; } = true;
    public bool CashFlowAnalysis { get; set; } = true;
    public bool BudgetMonitoring { get; set; } = true;
    public bool ReconciliationChecks { get; set; } = true;

    /// <summary>Comma-separated overdue day thresholds, e.g. 3,7,14,30.</summary>
    public string ReminderScheduleDays { get; set; } = "3,7,14,30";

    /// <summary>Preferred reminder channel: email | none. Sending still requires an authorized integration + approval when guarded.</summary>
    public string ReminderChannel { get; set; } = "email";

    public bool AutoSendReminders { get; set; }

    /// <summary>Variance percent above historical average that flags an expense anomaly.</summary>
    public decimal AnomalyVariancePercent { get; set; } = 40m;

    /// <summary>Days ahead to scan upcoming payables.</summary>
    public int PayableAlertDays { get; set; } = 14;

    /// <summary>Minimum months of history before cash-flow projections are offered.</summary>
    public int MinimumCashFlowHistoryMonths { get; set; } = 2;

    public string TimeZoneId { get; set; } = "UTC";
    public string ReportingBasis { get; set; } = "hybrid"; // hybrid | cash | accrual-orders | accrual-invoices
    public DateTime? LastMonitorRunAt { get; set; }
}

/// <summary>
/// Owner-configured budget for a category and reporting period.
/// </summary>
public class FinanceBudget : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public Guid? ExpenseCategoryId { get; set; }
    public string? CategoryName { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string Status { get; set; } = "Active";
    public string Source { get; set; } = "owner"; // owner | recommended
    public bool IsRecommendation { get; set; }

    public ExpenseCategory? ExpenseCategory { get; set; }
}

/// <summary>
/// Prepared payment reminder. Sending externally requires approval and a connected channel.
/// </summary>
public class FinancePaymentReminder : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid CustomerId { get; set; }
    public string Channel { get; set; } = "email";
    public string Status { get; set; } = "Draft"; // Draft | Queued | Sent | Failed | Cancelled | SkippedDuplicate
    public string Subject { get; set; } = default!;
    public string Body { get; set; } = default!;
    public decimal OutstandingAmount { get; set; }
    public int DaysOverdue { get; set; }
    public string ApprovalFingerprint { get; set; } = default!;
    public string? IdempotencyKey { get; set; }
    public Guid? ApprovalId { get; set; }
    public DateTime? ScheduledFor { get; set; }
    public DateTime? SentAt { get; set; }
    public string? ExternalMessageId { get; set; }
    public string? FailureReason { get; set; }

    public Invoice? Invoice { get; set; }
    public Customer? Customer { get; set; }
}

/// <summary>
/// Imported bank statement line for reconciliation support when live bank sync is unavailable.
/// </summary>
public class FinanceBankStatementLine : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid ImportBatchId { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = default!;
    public decimal Amount { get; set; }
    public string Direction { get; set; } = "outflow"; // inflow | outflow
    public string? Reference { get; set; }
    public string Fingerprint { get; set; } = default!;
    public string MatchStatus { get; set; } = "Unmatched"; // Unmatched | Matched | PossibleDuplicate | Ignored
    public Guid? MatchedPaymentId { get; set; }
    public Guid? MatchedExpenseId { get; set; }
    public string? Notes { get; set; }
}

public class FinanceBankImportBatch : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string FileName { get; set; } = default!;
    public string Status { get; set; } = "Imported";
    public int LineCount { get; set; }
    public int DuplicateCount { get; set; }
    public string Classification { get; set; } = "REAL"; // imported CSV is real tenant data; bank sync remains UNAVAILABLE
    public string? Notes { get; set; }
}

/// <summary>
/// Durable agent activity / alert for the finance workspace.
/// </summary>
public class FinanceAgentActivity : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Kind { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Summary { get; set; } = default!;
    public string Severity { get; set; } = "info";
    public string Status { get; set; } = "Open";
    public Guid? RelatedInvoiceId { get; set; }
    public Guid? RelatedPurchaseInvoiceId { get; set; }
    public Guid? RelatedExpenseId { get; set; }
    public Guid? RelatedBudgetId { get; set; }
    public string? PayloadJson { get; set; }
}
