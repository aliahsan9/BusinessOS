using BusinessOS.Application.Features.Billing.DTOs;

namespace BusinessOS.Application.Features.Billing.Services;

public interface IBillingService
{
    Task<IReadOnlyList<SubscriptionPlanDto>> GetPlansAsync(CancellationToken cancellationToken = default);
    Task<CurrentPlanDto> GetCurrentPlanAsync(CancellationToken cancellationToken = default);
    Task<BillingUsageDto> GetUsageAsync(CancellationToken cancellationToken = default);
    Task<BillingDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BillingInvoiceDto>> GetInvoicesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BillingTransactionDto>> GetTransactionsAsync(CancellationToken cancellationToken = default);
    Task<DowngradeValidationDto> ValidateDowngradeAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<CurrentPlanDto> UpgradePlanAsync(UpgradePlanRequest request, CancellationToken cancellationToken = default);
    Task<CurrentPlanDto> DowngradePlanAsync(DowngradePlanRequest request, CancellationToken cancellationToken = default);
    Task<CurrentPlanDto> CancelPlanAsync(CancelPlanRequest request, CancellationToken cancellationToken = default);
    Task<CurrentPlanDto> RenewPlanAsync(RenewPlanRequest request, CancellationToken cancellationToken = default);
    Task<CheckoutSessionDto> CreateCheckoutSessionAsync(CheckoutRequest request, CancellationToken cancellationToken = default);
    Task<BillingPortalDto> CreateBillingPortalSessionAsync(string returnUrl, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentProviderDto>> GetPaymentProvidersAsync(CancellationToken cancellationToken = default);
}

public interface IBillingWebhookService
{
    Task HandleStripeWebhookAsync(string payload, string signature, CancellationToken cancellationToken = default);
    Task HandleJazzCashWebhookAsync(string payload, CancellationToken cancellationToken = default);
    Task HandleEasyPaisaWebhookAsync(string payload, CancellationToken cancellationToken = default);
}

public interface IBillingMetricsService
{
    Task<BillingMetricsDto> GetMetricsAsync(CancellationToken cancellationToken = default);
}

public interface IFeatureFlagService
{
    Task<PlanFeatureFlagsDto> GetFeatureFlagsAsync(CancellationToken cancellationToken = default);
    Task EnsureFeatureEnabledAsync(string feature, CancellationToken cancellationToken = default);
}

public static class FeatureFlags
{
    public const string AiAssistant = "ai_assistant";
    public const string AdvancedAnalytics = "advanced_analytics";
    public const string PdfReports = "pdf_reports";
    public const string AdvancedReports = "advanced_reports";
    public const string PrioritySupport = "priority_support";
}
