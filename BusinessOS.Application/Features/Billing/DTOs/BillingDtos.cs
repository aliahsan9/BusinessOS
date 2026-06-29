namespace BusinessOS.Application.Features.Billing.DTOs;

public class SubscriptionPlanDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Description { get; set; }
    public decimal MonthlyPrice { get; set; }
    public decimal AnnualPrice { get; set; }
    public decimal AnnualSavingsPercent { get; set; }
    public int MaxUsers { get; set; }
    public int MaxCustomers { get; set; }
    public int MaxProjects { get; set; }
    public int MaxTasks { get; set; }
    public long MaxStorageMb { get; set; }
    public int MaxAiRequests { get; set; }
    public bool HasAiAssistant { get; set; }
    public bool HasAdvancedAnalytics { get; set; }
    public bool HasPdfReports { get; set; }
    public bool HasAdvancedReports { get; set; }
    public bool HasPrioritySupport { get; set; }
    public IReadOnlyList<string> Features { get; set; } = [];
    public int SortOrder { get; set; }
}

public class CurrentPlanDto
{
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = default!;
    public string PlanSlug { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string BillingInterval { get; set; } = default!;
    public string PaymentProvider { get; set; } = default!;
    public DateTime StartDate { get; set; }
    public DateTime? TrialEndDate { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime? RenewalDate { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
    public bool IsTrialActive { get; set; }
    public int TrialDaysRemaining { get; set; }
    public decimal CurrentPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public PlanFeatureFlagsDto FeatureFlags { get; set; } = new();
}

public class PlanFeatureFlagsDto
{
    public bool HasAiAssistant { get; set; }
    public bool HasAdvancedAnalytics { get; set; }
    public bool HasPdfReports { get; set; }
    public bool HasAdvancedReports { get; set; }
    public bool HasPrioritySupport { get; set; }
}

public class BillingUsageDto
{
    public int UserCount { get; set; }
    public int MaxUsers { get; set; }
    public int CustomerCount { get; set; }
    public int MaxCustomers { get; set; }
    public int ProjectCount { get; set; }
    public int MaxProjects { get; set; }
    public int TaskCount { get; set; }
    public int MaxTasks { get; set; }
    public long StorageUsedMb { get; set; }
    public long MaxStorageMb { get; set; }
    public int AiRequestsUsed { get; set; }
    public int MaxAiRequests { get; set; }
    public DateTime LastCalculatedAt { get; set; }
}

public class BillingInvoiceDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = default!;
    public string PlanName { get; set; } = default!;
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = default!;
    public string BillingInterval { get; set; } = default!;
    public string? PaymentMethod { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BillingTransactionDto
{
    public Guid Id { get; set; }
    public string TransactionId { get; set; } = default!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = default!;
    public string Provider { get; set; } = default!;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class BillingDashboardDto
{
    public CurrentPlanDto CurrentPlan { get; set; } = default!;
    public BillingUsageDto Usage { get; set; } = default!;
    public IReadOnlyList<BillingInvoiceDto> RecentInvoices { get; set; } = [];
    public IReadOnlyList<BillingTransactionDto> RecentTransactions { get; set; } = [];
}

public class CheckoutSessionDto
{
    public string SessionId { get; set; } = default!;
    public string? CheckoutUrl { get; set; }
    public string Provider { get; set; } = default!;
}

public class BillingPortalDto
{
    public string PortalUrl { get; set; } = default!;
}

public class DowngradeValidationDto
{
    public bool IsValid { get; set; }
    public IReadOnlyList<string> Violations { get; set; } = [];
}

public class PaymentProviderDto
{
    public string Provider { get; set; } = default!;
    public string Name { get; set; } = default!;
    public bool IsEnabled { get; set; }
    public bool IsSandbox { get; set; }
}

public class BillingMetricsDto
{
    public decimal Mrr { get; set; }
    public decimal Arr { get; set; }
    public int ActiveSubscriptions { get; set; }
    public int TrialSubscriptions { get; set; }
    public int CancelledSubscriptions { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TotalTenants { get; set; }
}

public record UpgradePlanRequest(Guid PlanId, string BillingInterval = "monthly", string Provider = "stripe");
public record DowngradePlanRequest(Guid PlanId);
public record CancelPlanRequest(bool CancelImmediately = false);
public record RenewPlanRequest(string BillingInterval = "monthly", string Provider = "stripe");
public record CheckoutRequest(Guid PlanId, string BillingInterval = "monthly", string Provider = "stripe", string? SuccessUrl = null, string? CancelUrl = null);
public record JazzCashPaymentRequest(Guid PlanId, string BillingInterval = "monthly");
public record EasyPaisaPaymentRequest(Guid PlanId, string BillingInterval = "monthly");
