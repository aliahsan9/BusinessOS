using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Billing.DTOs;
using BusinessOS.Application.Features.Tenant.Services;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Services;

public sealed class BillingService : IBillingService, IBillingPlanSyncService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantLimitService _limitService;
    private readonly ITenantAuditService _auditService;
    private readonly IStripePaymentService _stripeService;
    private readonly IJazzCashPaymentService _jazzCashService;
    private readonly IEasyPaisaPaymentService _easyPaisaService;
    private readonly IDbContextFactory<BusinessOSDbContext> _dbContextFactory;
    private readonly ILogger<BillingService> _logger;

    public BillingService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ITenantLimitService limitService,
        ITenantAuditService auditService,
        IStripePaymentService stripeService,
        IJazzCashPaymentService jazzCashService,
        IEasyPaisaPaymentService easyPaisaService,
        IDbContextFactory<BusinessOSDbContext> dbContextFactory,
        ILogger<BillingService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _limitService = limitService;
        _auditService = auditService;
        _stripeService = stripeService;
        _jazzCashService = jazzCashService;
        _easyPaisaService = easyPaisaService;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SubscriptionPlanDto>> GetPlansAsync(CancellationToken cancellationToken = default)
    {
        var plans = await _context.SubscriptionPlans
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.MonthlyPrice)
            .ToListAsync(cancellationToken);

        return plans.Select(MapPlan).ToList();
    }

    public async Task<CurrentPlanDto> GetCurrentPlanAsync(CancellationToken cancellationToken = default)
    {
        var (tenant, subscription) = await GetTenantWithSubscriptionAsync(cancellationToken);
        return MapCurrentPlan(tenant, subscription);
    }

    public async Task<BillingUsageDto> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        await _limitService.RefreshUsageAsync(tenantId, cancellationToken);

        var tenant = await _context.Tenants
            .AsNoTracking()
            .Include(x => x.Plan)
            .FirstAsync(x => x.Id == tenantId, cancellationToken);

        var usage = await _context.TenantUsages
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? new TenantUsage();

        var plan = tenant.Plan;
        return new BillingUsageDto
        {
            UserCount = usage.UserCount,
            MaxUsers = plan.MaxUsers,
            CustomerCount = usage.CustomerCount,
            MaxCustomers = plan.MaxCustomers,
            ProjectCount = usage.ProjectCount,
            MaxProjects = plan.MaxProjects,
            TaskCount = usage.TaskCount,
            MaxTasks = plan.MaxTasks,
            StorageUsedMb = usage.StorageUsedMb,
            MaxStorageMb = plan.MaxStorageMb,
            AiRequestsUsed = usage.AiRequestsUsed,
            MaxAiRequests = plan.MaxAiRequests,
            LastCalculatedAt = usage.LastCalculatedAt
        };
    }

    public async Task<BillingDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var currentPlan = await GetCurrentPlanAsync(cancellationToken);
        var usage = await GetUsageAsync(cancellationToken);
        var invoices = await GetInvoicesAsync(cancellationToken);
        var transactions = await GetTransactionsAsync(cancellationToken);

        return new BillingDashboardDto
        {
            CurrentPlan = currentPlan,
            Usage = usage,
            RecentInvoices = invoices.Take(5).ToList(),
            RecentTransactions = transactions.Take(5).ToList()
        };
    }

    public async Task<IReadOnlyList<BillingInvoiceDto>> GetInvoicesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var invoices = await _context.BillingInvoices
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return invoices.Select(MapInvoice).ToList();
    }

    public async Task<IReadOnlyList<BillingTransactionDto>> GetTransactionsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var transactions = await _context.BillingTransactions
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return transactions.Select(MapTransaction).ToList();
    }

    public async Task<DowngradeValidationDto> ValidateDowngradeAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        await _limitService.RefreshUsageAsync(tenantId, cancellationToken);

        var targetPlan = await _context.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == planId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Plan not found.");

        var usage = await _context.TenantUsages
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? new TenantUsage();

        var violations = new List<string>();

        if (!PlanLimitHelper.IsWithinLimit(usage.UserCount, targetPlan.MaxUsers))
            violations.Add($"You have {usage.UserCount} users but the {targetPlan.Name} plan allows {PlanLimitHelper.FormatLimit(targetPlan.MaxUsers)}.");

        if (!PlanLimitHelper.IsWithinLimit(usage.CustomerCount, targetPlan.MaxCustomers))
            violations.Add($"You have {usage.CustomerCount} customers but the {targetPlan.Name} plan allows {PlanLimitHelper.FormatLimit(targetPlan.MaxCustomers)}.");

        if (!PlanLimitHelper.IsWithinLimit(usage.ProjectCount, targetPlan.MaxProjects))
            violations.Add($"You have {usage.ProjectCount} projects but the {targetPlan.Name} plan allows {PlanLimitHelper.FormatLimit(targetPlan.MaxProjects)}.");

        if (!PlanLimitHelper.IsWithinLimit(usage.TaskCount, targetPlan.MaxTasks))
            violations.Add($"You have {usage.TaskCount} tasks but the {targetPlan.Name} plan allows {PlanLimitHelper.FormatLimit(targetPlan.MaxTasks)}.");

        if (targetPlan.MaxStorageMb != SubscriptionPlan.Unlimited && usage.StorageUsedMb > targetPlan.MaxStorageMb)
            violations.Add($"You are using {usage.StorageUsedMb} MB but the {targetPlan.Name} plan allows {targetPlan.MaxStorageMb} MB.");

        if (!PlanLimitHelper.IsUnlimited(targetPlan.MaxAiRequests) && usage.AiRequestsUsed > targetPlan.MaxAiRequests)
            violations.Add($"You have used {usage.AiRequestsUsed} AI requests but the {targetPlan.Name} plan allows {PlanLimitHelper.FormatLimit(targetPlan.MaxAiRequests)}.");

        return new DowngradeValidationDto
        {
            IsValid = violations.Count == 0,
            Violations = violations
        };
    }

    public async Task<CurrentPlanDto> UpgradePlanAsync(UpgradePlanRequest request, CancellationToken cancellationToken = default)
    {
        var (tenant, subscription) = await GetTenantWithSubscriptionAsync(cancellationToken, tracked: true);

        var plan = await _context.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.PlanId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Plan not found.");

        var oldPlanName = tenant.Plan?.Name ?? tenant.SubscriptionPlan;
        await ApplyPlanChangeAsync(tenant, subscription, plan, request.BillingInterval, request.Provider, cancellationToken);

        await _auditService.LogAsync(
            tenant.Id,
            "SubscriptionUpgraded",
            "TenantSubscription",
            RbacAuditService.Serialize(new { OldPlan = oldPlanName }),
            RbacAuditService.Serialize(new { NewPlan = plan.Name, request.BillingInterval }),
            cancellationToken: cancellationToken);

        return MapCurrentPlan(tenant, subscription);
    }

    public async Task<CurrentPlanDto> DowngradePlanAsync(DowngradePlanRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateDowngradeAsync(request.PlanId, cancellationToken);
        if (!validation.IsValid)
        {
            throw new BadRequestException(string.Join(" ", validation.Violations));
        }

        var (tenant, subscription) = await GetTenantWithSubscriptionAsync(cancellationToken, tracked: true);

        var plan = await _context.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.PlanId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Plan not found.");

        var oldPlanName = tenant.Plan?.Name ?? tenant.SubscriptionPlan;
        await ApplyPlanChangeAsync(tenant, subscription, plan, subscription.BillingInterval.ToString(), subscription.PaymentProvider.ToString(), cancellationToken);

        await _auditService.LogAsync(
            tenant.Id,
            "SubscriptionDowngraded",
            "TenantSubscription",
            RbacAuditService.Serialize(new { OldPlan = oldPlanName }),
            RbacAuditService.Serialize(new { NewPlan = plan.Name }),
            cancellationToken: cancellationToken);

        return MapCurrentPlan(tenant, subscription);
    }

    public async Task<CurrentPlanDto> CancelPlanAsync(CancelPlanRequest request, CancellationToken cancellationToken = default)
    {
        var (tenant, subscription) = await GetTenantWithSubscriptionAsync(cancellationToken, tracked: true);

        if (request.CancelImmediately)
        {
            subscription.Status = SubscriptionStatus.Cancelled;
            subscription.EndDate = DateTime.UtcNow;
            tenant.Status = TenantStatus.Cancelled;
        }
        else
        {
            subscription.CancelAtPeriodEnd = true;
        }

        subscription.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            tenant.Id,
            "SubscriptionCancelled",
            "TenantSubscription",
            null,
            RbacAuditService.Serialize(new { request.CancelImmediately, subscription.CancelAtPeriodEnd }),
            cancellationToken: cancellationToken);

        return MapCurrentPlan(tenant, subscription);
    }

    public async Task<CurrentPlanDto> RenewPlanAsync(RenewPlanRequest request, CancellationToken cancellationToken = default)
    {
        var (tenant, subscription) = await GetTenantWithSubscriptionAsync(cancellationToken, tracked: true);

        subscription.Status = SubscriptionStatus.Active;
        subscription.CancelAtPeriodEnd = false;
        subscription.EndDate = null;
        subscription.CurrentPeriodEnd = ParseBillingInterval(request.BillingInterval) == BillingInterval.Yearly
            ? DateTime.UtcNow.AddYears(1)
            : DateTime.UtcNow.AddMonths(1);
        subscription.UpdatedAt = DateTime.UtcNow;
        tenant.Status = TenantStatus.Active;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            tenant.Id,
            "SubscriptionRenewed",
            "TenantSubscription",
            null,
            RbacAuditService.Serialize(new { request.BillingInterval }),
            cancellationToken: cancellationToken);

        return MapCurrentPlan(tenant, subscription);
    }

    public async Task<CheckoutSessionDto> CreateCheckoutSessionAsync(CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var (tenant, subscription) = await GetTenantWithSubscriptionAsync(cancellationToken);

        var plan = await _context.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.PlanId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Plan not found.");

        var planDto = MapPlan(plan);
        var provider = request.Provider.ToLowerInvariant();

        return provider switch
        {
            "jazzcash" => await _jazzCashService.InitiatePaymentAsync(tenantId, planDto, request.BillingInterval, cancellationToken),
            "easypaisa" => await _easyPaisaService.InitiatePaymentAsync(tenantId, planDto, request.BillingInterval, cancellationToken),
            _ => await _stripeService.CreateCheckoutSessionAsync(
                tenantId,
                tenant.Name,
                tenant.Email,
                planDto,
                request.BillingInterval,
                request.SuccessUrl,
                request.CancelUrl,
                subscription.StripeCustomerId,
                cancellationToken)
        };
    }

    public async Task<BillingPortalDto> CreateBillingPortalSessionAsync(string returnUrl, CancellationToken cancellationToken = default)
    {
        var (_, subscription) = await GetTenantWithSubscriptionAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(subscription.StripeCustomerId))
        {
            throw new BadRequestException("No Stripe customer found. Complete a checkout first.");
        }

        return await _stripeService.CreateBillingPortalSessionAsync(subscription.StripeCustomerId, returnUrl, cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentProviderDto>> GetPaymentProvidersAsync(CancellationToken cancellationToken = default)
    {
        var providers = await _context.PaymentProviders
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        if (providers.Count == 0)
        {
            return
            [
                new PaymentProviderDto { Provider = "stripe", Name = "Stripe", IsEnabled = _stripeService.IsConfigured, IsSandbox = true },
                new PaymentProviderDto { Provider = "jazzcash", Name = "JazzCash", IsEnabled = _jazzCashService.IsConfigured, IsSandbox = true },
                new PaymentProviderDto { Provider = "easypaisa", Name = "EasyPaisa", IsEnabled = _easyPaisaService.IsConfigured, IsSandbox = true }
            ];
        }

        return providers.Select(x => new PaymentProviderDto
        {
            Provider = x.ProviderType.ToString().ToLowerInvariant(),
            Name = x.Name,
            IsEnabled = x.IsEnabled,
            IsSandbox = x.IsSandbox
        }).ToList();
    }

    public async Task ApplyPlanFromWebhookAsync(
        Guid tenantId,
        Guid planId,
        SubscriptionStatus status,
        BillingInterval interval,
        PaymentProviderType provider,
        string? stripeCustomerId,
        string? stripeSubscriptionId,
        DateTime? periodEnd,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var tenant = await db.Tenants.IgnoreQueryFilters()
            .Include(x => x.Plan)
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant not found.");

        var plan = await db.SubscriptionPlans.AsNoTracking().FirstAsync(x => x.Id == planId, cancellationToken);

        var subscription = await db.TenantSubscriptions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        if (subscription is null)
        {
            subscription = new TenantSubscription { TenantId = tenantId, CreatedAt = DateTime.UtcNow };
            db.TenantSubscriptions.Add(subscription);
        }

        tenant.SubscriptionPlanId = planId;
        tenant.SubscriptionPlan = plan.Name;
        tenant.Status = status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired
            ? TenantStatus.Cancelled
            : TenantStatus.Active;

        subscription.SubscriptionPlanId = planId;
        subscription.Status = status;
        subscription.BillingInterval = interval;
        subscription.PaymentProvider = provider;
        subscription.StripeCustomerId = stripeCustomerId ?? subscription.StripeCustomerId;
        subscription.StripeSubscriptionId = stripeSubscriptionId ?? subscription.StripeSubscriptionId;
        subscription.CurrentPeriodEnd = periodEnd;
        subscription.UpdatedAt = DateTime.UtcNow;

        if (status == SubscriptionStatus.Active && subscription.TrialEndDate.HasValue && subscription.TrialEndDate > DateTime.UtcNow)
        {
            subscription.TrialEndDate = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordTransactionAsync(
        Guid tenantId,
        decimal amount,
        string currency,
        BillingTransactionStatus status,
        PaymentProviderType provider,
        string transactionId,
        string? description,
        Guid? invoiceId,
        CancellationToken cancellationToken)
    {
        var transaction = new BillingTransaction
        {
            TenantId = tenantId,
            BillingInvoiceId = invoiceId,
            TransactionId = transactionId,
            Amount = amount,
            Currency = currency,
            Status = status,
            Provider = provider,
            Description = description,
            CompletedAt = status == BillingTransactionStatus.Completed ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow
        };

        _context.BillingTransactions.Add(transaction);
        await _context.SaveChangesAsync(cancellationToken);
    }

    internal async Task<BillingInvoice> CreateInvoiceAsync(
        Guid tenantId,
        SubscriptionPlan plan,
        BillingInterval interval,
        PaymentProviderType provider,
        decimal taxRate,
        CancellationToken cancellationToken)
    {
        var subtotal = interval == BillingInterval.Yearly ? plan.AnnualPrice : plan.MonthlyPrice;
        var tax = Math.Round(subtotal * taxRate, 2);
        var total = subtotal + tax;
        var now = DateTime.UtcNow;
        var periodEnd = interval == BillingInterval.Yearly ? now.AddYears(1) : now.AddMonths(1);

        var invoice = new BillingInvoice
        {
            TenantId = tenantId,
            InvoiceNumber = $"INV-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}",
            SubscriptionPlanId = plan.Id,
            PlanName = plan.Name,
            Subtotal = subtotal,
            TaxAmount = tax,
            TotalAmount = total,
            Currency = "USD",
            Status = BillingInvoiceStatus.Paid,
            BillingInterval = interval,
            PaymentProvider = provider,
            PeriodStart = now,
            PeriodEnd = periodEnd,
            PaidAt = now,
            CreatedAt = now
        };

        _context.BillingInvoices.Add(invoice);
        await _context.SaveChangesAsync(cancellationToken);
        return invoice;
    }

    private async Task ApplyPlanChangeAsync(
        Tenant tenant,
        TenantSubscription subscription,
        SubscriptionPlan plan,
        string billingInterval,
        string provider,
        CancellationToken cancellationToken)
    {
        tenant.SubscriptionPlanId = plan.Id;
        tenant.SubscriptionPlan = plan.Name;
        subscription.SubscriptionPlanId = plan.Id;
        subscription.BillingInterval = ParseBillingInterval(billingInterval);
        subscription.PaymentProvider = ParseProvider(provider);
        subscription.Status = SubscriptionStatus.Active;
        subscription.CancelAtPeriodEnd = false;
        subscription.CurrentPeriodEnd = subscription.BillingInterval == BillingInterval.Yearly
            ? DateTime.UtcNow.AddYears(1)
            : DateTime.UtcNow.AddMonths(1);
        subscription.UpdatedAt = DateTime.UtcNow;
        tenant.Status = TenantStatus.Active;

        await _context.SaveChangesAsync(cancellationToken);
        await _limitService.RefreshUsageAsync(tenant.Id, cancellationToken);
    }

    private async Task<(Tenant tenant, TenantSubscription subscription)> GetTenantWithSubscriptionAsync(
        CancellationToken cancellationToken,
        bool tracked = false)
    {
        var tenantId = RequireTenantId();

        var tenantQuery = tracked ? _context.Tenants : _context.Tenants.AsNoTracking();
        var tenant = await tenantQuery
            .Include(x => x.Plan)
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant not found.");

        if (tracked)
        {
            var subscription = await _context.TenantSubscriptions
                .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

            if (subscription is null)
            {
                subscription = new TenantSubscription
                {
                    TenantId = tenantId,
                    SubscriptionPlanId = tenant.SubscriptionPlanId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.TenantSubscriptions.Add(subscription);
            }

            return (tenant, subscription);
        }

        var sub = await _context.TenantSubscriptions
            .AsNoTracking()
            .Include(x => x.Plan)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? new TenantSubscription
            {
                TenantId = tenantId,
                SubscriptionPlanId = tenant.SubscriptionPlanId,
                Status = SubscriptionStatus.Trial,
                TrialEndDate = DateTime.UtcNow.AddDays(14)
            };

        return (tenant, sub);
    }

    private Guid RequireTenantId() =>
        _currentUserService.TenantId ?? throw new BadRequestException("Tenant context is required.");

    private static SubscriptionPlanDto MapPlan(SubscriptionPlan plan)
    {
        var annualMonthly = plan.AnnualPrice / 12m;
        var savings = plan.MonthlyPrice > 0
            ? Math.Round((1 - annualMonthly / plan.MonthlyPrice) * 100, 0)
            : 0;

        var features = BuildFeatureList(plan);

        return new SubscriptionPlanDto
        {
            Id = plan.Id,
            Name = plan.Name,
            Slug = plan.Slug,
            Description = plan.Description,
            MonthlyPrice = plan.MonthlyPrice,
            AnnualPrice = plan.AnnualPrice,
            AnnualSavingsPercent = savings,
            MaxUsers = plan.MaxUsers,
            MaxCustomers = plan.MaxCustomers,
            MaxProjects = plan.MaxProjects,
            MaxTasks = plan.MaxTasks,
            MaxStorageMb = plan.MaxStorageMb,
            MaxAiRequests = plan.MaxAiRequests,
            HasAiAssistant = plan.HasAiAssistant,
            HasAdvancedAnalytics = plan.HasAdvancedAnalytics,
            HasPdfReports = plan.HasPdfReports,
            HasAdvancedReports = plan.HasAdvancedReports,
            HasPrioritySupport = plan.HasPrioritySupport,
            Features = features,
            SortOrder = plan.SortOrder
        };
    }

    private static List<string> BuildFeatureList(SubscriptionPlan plan)
    {
        var features = new List<string>
        {
            $"{PlanLimitHelper.FormatLimit(plan.MaxUsers)} Users",
            $"{PlanLimitHelper.FormatLimit(plan.MaxCustomers)} Customers",
            $"{PlanLimitHelper.FormatLimit(plan.MaxProjects)} Projects",
            $"{PlanLimitHelper.FormatLimit(plan.MaxTasks)} Tasks"
        };

        if (plan.HasAdvancedAnalytics) features.Add("Advanced Analytics");
        else features.Add("Basic Analytics");

        if (plan.HasPdfReports) features.Add("PDF Reports");
        if (plan.HasAdvancedReports) features.Add("Advanced Reports");
        if (plan.HasAiAssistant) features.Add("AI Assistant");
        if (plan.HasPrioritySupport) features.Add("Priority Support");

        return features;
    }

    private static CurrentPlanDto MapCurrentPlan(Tenant tenant, TenantSubscription subscription)
    {
        var plan = tenant.Plan;
        var isTrial = subscription.Status == SubscriptionStatus.Trial;
        var trialDaysRemaining = 0;

        if (isTrial && subscription.TrialEndDate.HasValue)
        {
            trialDaysRemaining = Math.Max(0, (int)Math.Ceiling((subscription.TrialEndDate.Value - DateTime.UtcNow).TotalDays));
        }

        var price = subscription.BillingInterval == BillingInterval.Yearly
            ? plan?.AnnualPrice ?? 0
            : plan?.MonthlyPrice ?? 0;

        return new CurrentPlanDto
        {
            PlanId = plan?.Id ?? tenant.SubscriptionPlanId,
            PlanName = plan?.Name ?? tenant.SubscriptionPlan,
            PlanSlug = plan?.Slug ?? "free",
            Status = subscription.Status.ToString(),
            BillingInterval = subscription.BillingInterval.ToString(),
            PaymentProvider = subscription.PaymentProvider.ToString(),
            StartDate = subscription.StartDate,
            TrialEndDate = subscription.TrialEndDate,
            CurrentPeriodEnd = subscription.CurrentPeriodEnd,
            RenewalDate = subscription.CurrentPeriodEnd ?? subscription.TrialEndDate,
            CancelAtPeriodEnd = subscription.CancelAtPeriodEnd,
            IsTrialActive = isTrial && trialDaysRemaining > 0,
            TrialDaysRemaining = trialDaysRemaining,
            CurrentPrice = price,
            Currency = tenant.Currency,
            FeatureFlags = new PlanFeatureFlagsDto
            {
                HasAiAssistant = plan?.HasAiAssistant ?? false,
                HasAdvancedAnalytics = plan?.HasAdvancedAnalytics ?? false,
                HasPdfReports = plan?.HasPdfReports ?? false,
                HasAdvancedReports = plan?.HasAdvancedReports ?? false,
                HasPrioritySupport = plan?.HasPrioritySupport ?? false
            }
        };
    }

    private static BillingInvoiceDto MapInvoice(BillingInvoice invoice) => new()
    {
        Id = invoice.Id,
        InvoiceNumber = invoice.InvoiceNumber,
        PlanName = invoice.PlanName,
        Subtotal = invoice.Subtotal,
        TaxAmount = invoice.TaxAmount,
        TotalAmount = invoice.TotalAmount,
        Currency = invoice.Currency,
        Status = invoice.Status.ToString(),
        BillingInterval = invoice.BillingInterval.ToString(),
        PaymentMethod = invoice.PaymentMethod,
        PeriodStart = invoice.PeriodStart,
        PeriodEnd = invoice.PeriodEnd,
        PaidAt = invoice.PaidAt,
        CreatedAt = invoice.CreatedAt
    };

    private static BillingTransactionDto MapTransaction(BillingTransaction tx) => new()
    {
        Id = tx.Id,
        TransactionId = tx.TransactionId,
        Amount = tx.Amount,
        Currency = tx.Currency,
        Status = tx.Status.ToString(),
        Provider = tx.Provider.ToString(),
        Description = tx.Description,
        CreatedAt = tx.CreatedAt,
        CompletedAt = tx.CompletedAt
    };

    private static BillingInterval ParseBillingInterval(string interval) =>
        interval.Equals("yearly", StringComparison.OrdinalIgnoreCase) ||
        interval.Equals("annual", StringComparison.OrdinalIgnoreCase)
            ? BillingInterval.Yearly
            : BillingInterval.Monthly;

    private static PaymentProviderType ParseProvider(string provider) =>
        provider.ToLowerInvariant() switch
        {
            "jazzcash" => PaymentProviderType.JazzCash,
            "easypaisa" => PaymentProviderType.EasyPaisa,
            "stripe" => PaymentProviderType.Stripe,
            _ => PaymentProviderType.Manual
        };
}
