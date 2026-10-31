using System.Text.Json;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Idempotency;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Billing.DTOs;
using BusinessOS.Application.Features.Tenant.Services;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Domain.Billing;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.Services;

public sealed partial class BillingService : IBillingService, IBillingPlanSyncService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantLimitService _limitService;
    private readonly ITenantAuditService _auditService;
    private readonly IStripePaymentService _stripeService;
    private readonly IJazzCashPaymentService _jazzCashService;
    private readonly IEasyPaisaPaymentService _easyPaisaService;
    private readonly IDbContextFactory<BusinessOSDbContext> _dbContextFactory;
    private readonly IIdempotencyService _idempotency;
    private readonly ILogger<BillingService> _logger;
    private readonly BillingOptions _billingOptions;
    private readonly IHostEnvironment _environment;
    private readonly IPaymentProviderSelector _paymentProviders;

    public BillingService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ITenantLimitService limitService,
        ITenantAuditService auditService,
        IStripePaymentService stripeService,
        IJazzCashPaymentService jazzCashService,
        IEasyPaisaPaymentService easyPaisaService,
        IDbContextFactory<BusinessOSDbContext> dbContextFactory,
        IIdempotencyService idempotency,
        ILogger<BillingService> logger,
        IOptions<BillingOptions> billingOptions,
        IHostEnvironment environment,
        IPaymentProviderSelector paymentProviders)
    {
        _context = context;
        _currentUserService = currentUserService;
        _limitService = limitService;
        _auditService = auditService;
        _stripeService = stripeService;
        _jazzCashService = jazzCashService;
        _easyPaisaService = easyPaisaService;
        _dbContextFactory = dbContextFactory;
        _idempotency = idempotency;
        _logger = logger;
        _billingOptions = billingOptions.Value;
        _environment = environment;
        _paymentProviders = paymentProviders;
    }

    public async Task<IReadOnlyList<SubscriptionPlanDto>> GetPlansAsync(
        string? currency = null,
        string? country = null,
        CancellationToken cancellationToken = default)
    {
        var plans = await _context.SubscriptionPlans
            .AsNoTracking()
            .Where(x => x.IsActive && x.IsPublic)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.MonthlyPrice)
            .ToListAsync(cancellationToken);

        var regional = await _context.RegionalPrices
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

        return plans.Select(plan => MapPlan(plan, ResolveRegional(plan, regional, currency, country), currency)).ToList();
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

        var subscription = await _context.TenantSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        var limits = EntitlementResolver.Resolve(tenant.Plan, subscription);
        var unlimitedAi = PlanLimitHelper.IsUnlimited(limits.MaxAiRequests);
        return new BillingUsageDto
        {
            UserCount = usage.UserCount,
            MaxUsers = limits.MaxUsers,
            CustomerCount = usage.CustomerCount,
            MaxCustomers = limits.MaxCustomers,
            ProductCount = usage.ProductCount,
            MaxProducts = limits.MaxProducts,
            ProjectCount = usage.ProjectCount,
            MaxProjects = limits.MaxProjects,
            TaskCount = usage.TaskCount,
            MaxTasks = limits.MaxTasks,
            StorageUsedMb = usage.StorageUsedMb,
            MaxStorageMb = limits.MaxStorageMb,
            AiRequestsUsed = usage.AiRequestsUsed,
            MaxAiRequests = limits.MaxAiRequests,
            AiRequestsRemaining = unlimitedAi ? SubscriptionPlan.Unlimited : Math.Max(0, limits.MaxAiRequests - usage.AiRequestsUsed),
            UsersAreCustom = tenant.Plan.IsCustomPricing && PlanLimitHelper.IsUnlimited(limits.MaxUsers),
            AiRequestsAreCustom = tenant.Plan.IsCustomPricing && unlimitedAi,
            ResetDate = subscription?.CurrentPeriodEnd ?? usage.AiPeriodStart?.AddMonths(1),
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

        if (!PlanLimitHelper.IsWithinLimit(usage.ProductCount, targetPlan.MaxProducts))
            violations.Add($"You have {usage.ProductCount} products but the {targetPlan.Name} plan allows {PlanLimitHelper.FormatLimit(targetPlan.MaxProducts)}.");

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
        var result = await ChangePlanAsync(
            new ChangePlanRequest(request.PlanId, request.BillingInterval, request.Provider, Confirm: true),
            cancellationToken);
        if (result.CheckoutRequired)
        {
            throw new BadRequestException(
                "This plan requires a verified checkout. The subscription was not activated.");
        }

        return result.Subscription ?? await GetCurrentPlanAsync(cancellationToken);
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
            .FirstOrDefaultAsync(x => x.Id == request.PlanId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Plan not found.");

        if (!string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId))
        {
            throw new BadRequestException(
                "This subscription is billed by Stripe. Change the plan through checkout so Stripe can prorate the invoice.");
        }

        var oldPlanName = tenant.Plan?.Name ?? tenant.SubscriptionPlan;
        await ApplyPlanChangeAsync(tenant, subscription, plan, subscription.BillingInterval.ToString(), subscription.PaymentProvider.ToString(), cancellationToken, verified: true);

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

        if (_stripeService.IsConfigured && !string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId))
        {
            if (request.CancelImmediately)
                await _stripeService.CancelSubscriptionNowAsync(subscription.StripeSubscriptionId, cancellationToken);
            else
                await _stripeService.SetCancelAtPeriodEndAsync(subscription.StripeSubscriptionId, true, cancellationToken);
        }

        if (request.CancelImmediately)
        {
            subscription.Status = SubscriptionStatus.Cancelled;
            subscription.EndDate = DateTime.UtcNow;
            tenant.Status = TenantStatus.Cancelled;
        }
        else
        {
            subscription.CancelAtPeriodEnd = true;
            subscription.CancelEffectiveAt = subscription.CurrentPeriodEnd;
            subscription.AutoRenewEnabled = false;
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

    public async Task<CheckoutSessionDto> CreateCheckoutSessionAsync(
        CheckoutRequest request,
        CancellationToken cancellationToken = default,
        string? idempotencyKey = null)
    {
        if (!IdempotencyKeyRules.IsValid(idempotencyKey))
        {
            throw new BadRequestException(
                "Idempotency-Key header is required and must be 1-128 visible ASCII characters without spaces.");
        }

        var tenantId = RequireTenantId();
        var (tenant, subscription) = await GetTenantWithSubscriptionAsync(cancellationToken);

        var plan = await _context.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.PlanId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Plan not found.");

        var providerName = request.Provider.ToLowerInvariant();
        var yearly = request.BillingInterval.Equals("yearly", StringComparison.OrdinalIgnoreCase)
            || request.BillingInterval.Equals("annual", StringComparison.OrdinalIgnoreCase);
        var (amount, currency, priceNote) = await ResolveCheckoutAmountAsync(
            plan, subscription, yearly, providerName, request.Currency, request.Country, cancellationToken);
        BillingReturnUrls.Validate(request.SuccessUrl, _billingOptions.AllowedReturnOrigins, _environment.IsDevelopment());
        BillingReturnUrls.Validate(request.CancelUrl, _billingOptions.AllowedReturnOrigins, _environment.IsDevelopment());
        var hash = CanonicalRequestHasher.BillingCheckout(
            tenantId,
            plan.Id,
            request.BillingInterval,
            providerName,
            amount,
            currency);

        var claim = await _idempotency.ClaimCommittedAsync(
            IdempotencyOperations.BillingCheckout,
            idempotencyKey!,
            hash,
            cancellationToken);

        if (claim.Kind == IdempotencyOutcomeKind.Replay)
            return ReadCheckoutSession(claim.ResponseBody);

        if (claim.Kind == IdempotencyOutcomeKind.Conflict)
        {
            throw new ConflictException(
                "This Idempotency-Key was already used for a different checkout request.",
                ErrorCodes.IdempotencyKeyReused);
        }

        if (claim.Kind == IdempotencyOutcomeKind.InProgress && claim.ResourceId is null)
        {
            throw new ConflictException(
                "This checkout is still processing. Retry the same request.",
                ErrorCodes.IdempotencyInProgress);
        }

        var record = claim.Record
            ?? throw new ConflictException(
                "This checkout is still processing. Retry the same request.",
                ErrorCodes.IdempotencyInProgress);

        var transaction = await FindOrCreateCheckoutTransactionAsync(
            claim,
            record,
            tenantId,
            providerName,
            amount,
            currency,
            plan.Name,
            cancellationToken);

        var stored = ReadStoredSession(transaction.Metadata);
        if (stored is not null)
            return stored;

        var planDto = MapPlan(plan);
        planDto.MonthlyPrice = yearly ? planDto.MonthlyPrice : amount;
        planDto.AnnualPrice = yearly ? amount : planDto.AnnualPrice;
        planDto.Currency = currency;
        planDto.SettlementCurrency = currency;
        CheckoutSessionDto session;
        try
        {
            session = providerName switch
            {
                "jazzcash" => await _jazzCashService.InitiatePaymentAsync(
                    tenantId, planDto, request.BillingInterval, cancellationToken, transaction.TransactionId),
                "easypaisa" => await _easyPaisaService.InitiatePaymentAsync(
                    tenantId, planDto, request.BillingInterval, cancellationToken, transaction.TransactionId),
                "tap" or "paytabs" => await _paymentProviders.Require(providerName, currency)
                    .CreateSubscriptionCheckoutAsync(new ProviderCheckoutCommand(
                        tenantId,
                        tenant.Name,
                        tenant.Email,
                        planDto,
                        request.BillingInterval,
                        amount,
                        currency,
                        request.SuccessUrl,
                        request.CancelUrl,
                        subscription.StripeCustomerId,
                        transaction.TransactionId), cancellationToken),
                _ => await _stripeService.CreateCheckoutSessionAsync(
                    tenantId,
                    tenant.Name,
                    tenant.Email,
                    planDto,
                    request.BillingInterval,
                    request.SuccessUrl,
                    request.CancelUrl,
                    subscription.StripeCustomerId,
                    cancellationToken,
                    transaction.TransactionId,
                    currency)
            };
        }
        catch (Exception ex) when (IsUnknownProviderOutcome(ex))
        {
            transaction.Status = BillingTransactionStatus.RequiresReconciliation;
            transaction.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(
                ex,
                "Unknown checkout outcome for transaction {TransactionId} in tenant {TenantId}",
                transaction.TransactionId,
                tenantId);
            throw new ConflictException(
                "The payment provider did not confirm the result. Retry this checkout with the same Idempotency-Key. Do not start a new payment.",
                ErrorCodes.IdempotencyInProgress);
        }

        session.Amount = amount;
        session.Currency = currency;
        session.PlanId = plan.Id;
        session.BillingInterval = request.BillingInterval;
        session.PriceNote = priceNote;
        if (string.IsNullOrWhiteSpace(session.CheckoutUrl) && providerName is "stripe" or "tap" or "paytabs")
            session.RequiresServerConfirmation = _environment.IsDevelopment() && providerName == "stripe" && !_stripeService.IsConfigured;

        transaction.ProviderReference = session.SessionId;
        transaction.Metadata = JsonSerializer.Serialize(session);
        transaction.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await _idempotency.CompleteCommittedAsync(record.Id, 200, transaction.Metadata, transaction.Id, cancellationToken);
        return session;
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

        var planChanged = subscription.SubscriptionPlanId != planId;
        tenant.SubscriptionPlanId = planId;
        tenant.SubscriptionPlan = plan.Name;
        tenant.Status = status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired
            ? TenantStatus.Cancelled
            : TenantStatus.Active;

        var fromStatus = SubscriptionStatusNames.ToApi(subscription.Status);
        if (planChanged)
        {
            subscription.PreserveContract = false;
            subscription.ContractedLimitsJson = null;
            subscription.ContractedMonthlyPrice = null;
            subscription.ContractedAnnualPrice = null;
            subscription.ContractedCurrency = null;
        }

        subscription.SubscriptionPlanId = planId;
        subscription.Status = plan.MonthlyPrice == 0 && status == SubscriptionStatus.Active
            ? SubscriptionStatus.Free
            : status;
        subscription.BillingInterval = interval;
        subscription.PaymentProvider = provider;
        subscription.StripeCustomerId = stripeCustomerId ?? subscription.StripeCustomerId;
        subscription.StripeSubscriptionId = stripeSubscriptionId ?? subscription.StripeSubscriptionId;
        subscription.CurrentPeriodEnd = periodEnd;
        subscription.SettlementCurrency ??= string.IsNullOrWhiteSpace(plan.BaseCurrency) ? "USD" : plan.BaseCurrency;
        subscription.AutoRenewEnabled = subscription.Status is SubscriptionStatus.Active or SubscriptionStatus.Trial &&
            !subscription.CancelAtPeriodEnd &&
            provider == PaymentProviderType.Stripe;
        if (string.IsNullOrWhiteSpace(subscription.PaymentMethodSummary) && provider == PaymentProviderType.Stripe)
            subscription.PaymentMethodSummary = "Managed in Stripe";
        subscription.UpdatedAt = DateTime.UtcNow;
        db.SubscriptionHistories.Add(new SubscriptionHistory
        {
            TenantId = tenantId,
            SubscriptionPlanId = planId,
            Action = "ProviderConfirmed",
            FromStatus = fromStatus,
            ToStatus = SubscriptionStatusNames.ToApi(subscription.Status),
            Currency = subscription.SettlementCurrency,
            Provider = provider.ToString(),
            Notes = "Subscription updated from a verified provider event.",
            CreatedAt = DateTime.UtcNow
        });

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
        var existing = await _context.BillingTransactions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                x => !x.IsDeleted &&
                    x.Provider == provider &&
                    (x.TransactionId == transactionId ||
                     x.ProviderOperationKey == transactionId ||
                     x.ProviderReference == transactionId),
                cancellationToken);

        if (existing is not null)
        {
            if (existing.TenantId != tenantId)
            {
                _logger.LogWarning(
                    "Ignored billing result {TransactionId} because it does not belong to tenant {TenantId}",
                    transactionId,
                    tenantId);
                return;
            }

            if (amount > 0 && Math.Abs(existing.Amount - amount) > 0.01m &&
                status == BillingTransactionStatus.Completed)
            {
                existing.Status = BillingTransactionStatus.RequiresReconciliation;
                existing.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogWarning(
                    "Billing transaction {TransactionId} amount {StoredAmount} did not match provider amount {ProviderAmount}",
                    existing.TransactionId,
                    existing.Amount,
                    amount);
                return;
            }

            if (!BillingTransactionTransitions.CanApplyVerifiedProviderResult(existing.Status, status))
                return;

            existing.Status = status;
            existing.ProviderReference ??= transactionId;
            existing.UpdatedAt = DateTime.UtcNow;
            if (status == BillingTransactionStatus.Completed)
                existing.CompletedAt ??= DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

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
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbExceptionClassifier.IsUniqueViolation(ex))
        {
            _context.Detach(transaction);
            _logger.LogInformation(
                "Billing transaction {TransactionId} was already recorded for provider {Provider}",
                transactionId,
                provider);
        }
    }

    public async Task<BillingTransactionDto> RefundTransactionAsync(
        Guid transactionId,
        decimal amount,
        string? currency,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!IdempotencyKeyRules.IsValid(idempotencyKey))
            throw new BadRequestException("Idempotency-Key header is required and must be 1-128 visible ASCII characters without spaces.");

        if (amount <= 0)
            throw new BadRequestException("Refund amount must be greater than zero.");

        var tenantId = RequireTenantId();
        var transaction = await _context.BillingTransactions
            .FirstOrDefaultAsync(x => x.Id == transactionId, cancellationToken)
            ?? throw new NotFoundException("Billing transaction not found.");

        var rounded = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        var requestedCurrency = string.IsNullOrWhiteSpace(currency)
            ? transaction.Currency
            : currency.Trim().ToUpperInvariant();
        var hash = CanonicalRequestHasher.BillingRefund(tenantId, transaction.Id, rounded, requestedCurrency);
        var claim = await _idempotency.BeginLocalAsync(
            IdempotencyOperations.BillingRefund,
            idempotencyKey,
            hash,
            cancellationToken);

        if (claim.Kind == IdempotencyOutcomeKind.Replay)
            return MapTransaction(transaction);

        if (claim.Kind == IdempotencyOutcomeKind.Conflict)
        {
            throw new ConflictException(
                "This Idempotency-Key was already used for a different refund request.",
                ErrorCodes.IdempotencyKeyReused);
        }

        if (claim.Kind == IdempotencyOutcomeKind.InProgress)
        {
            throw new ConflictException(
                "This refund is still processing. Retry the same request.",
                ErrorCodes.IdempotencyInProgress);
        }

        if (!string.Equals(requestedCurrency, transaction.Currency, StringComparison.OrdinalIgnoreCase))
        {
            if (claim.Record is not null)
                _idempotency.Detach(claim.Record);
            throw new BadRequestException("Refund currency does not match the transaction currency.");
        }

        if (rounded != transaction.Amount)
        {
            if (claim.Record is not null)
                _idempotency.Detach(claim.Record);
            throw new BadRequestException("Billing refunds must match the original transaction amount.");
        }

        if (!BillingTransactionTransitions.CanApply(transaction.Status, BillingTransactionStatus.Refunded))
        {
            if (claim.Record is not null)
                _idempotency.Detach(claim.Record);
            throw new ConflictException(
                "This transaction cannot be refunded from its current state.",
                ErrorCodes.ConcurrencyConflict);
        }

        transaction.Status = BillingTransactionStatus.Refunded;
        transaction.UpdatedAt = DateTime.UtcNow;
        if (claim.Record is not null)
        {
            _idempotency.CompleteLocal(
                claim.Record,
                200,
                JsonSerializer.Serialize(new { id = transaction.Id }),
                transaction.Id);
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict while refunding billing transaction {TransactionId}", transaction.Id);
            throw new ConflictException(
                "This transaction was changed by someone else. Refresh and try again.",
                ErrorCodes.ConcurrencyConflict);
        }
        catch (DbUpdateException ex) when (DbExceptionClassifier.IsUniqueViolation(ex))
        {
            if (claim.Record is not null)
                _idempotency.Detach(claim.Record);
            throw new ConflictException(
                "This refund request was already recorded.",
                ErrorCodes.IdempotencyInProgress);
        }

        _logger.LogInformation(
            "Refunded billing transaction {TransactionId} in tenant {TenantId}",
            transaction.Id,
            tenantId);
        return MapTransaction(transaction);
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
        CancellationToken cancellationToken,
        bool verified = false,
        bool startTrial = false)
    {
        var interval = ParseBillingInterval(billingInterval);
        var price = interval == BillingInterval.Yearly ? plan.AnnualPrice : plan.MonthlyPrice;
        if (!verified && price > 0 && !startTrial)
            throw new BadRequestException("Paid plans are activated only after the payment provider confirms the charge.");

        var fromStatus = SubscriptionStatusNames.ToApi(subscription.Status);
        tenant.SubscriptionPlanId = plan.Id;
        tenant.SubscriptionPlan = plan.Name;
        tenant.Plan = plan;
        tenant.Status = TenantStatus.Active;
        subscription.SubscriptionPlanId = plan.Id;
        subscription.BillingInterval = interval;
        subscription.PaymentProvider = ParseProvider(provider);
        subscription.Status = price == 0
            ? SubscriptionStatus.Free
            : startTrial ? SubscriptionStatus.Trial : SubscriptionStatus.Active;
        subscription.CancelAtPeriodEnd = false;
        subscription.CancelEffectiveAt = null;
        subscription.ScheduledPlanId = null;
        subscription.PreserveContract = false;
        subscription.ContractedMonthlyPrice = null;
        subscription.ContractedAnnualPrice = null;
        subscription.ContractedCurrency = null;
        subscription.ContractedLimitsJson = null;
        subscription.SettlementCurrency = string.IsNullOrWhiteSpace(plan.BaseCurrency) ? "USD" : plan.BaseCurrency;
        subscription.AutoRenewEnabled = subscription.Status == SubscriptionStatus.Active &&
            subscription.PaymentProvider == PaymentProviderType.Stripe &&
            !string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId);
        if (startTrial)
        {
            subscription.TrialConsumed = true;
            subscription.TrialEndDate = DateTime.UtcNow.AddDays(plan.TrialDays > 0 ? plan.TrialDays : _billingOptions.TrialDays);
            subscription.CurrentPeriodEnd = subscription.TrialEndDate;
        }
        else
        {
            subscription.CurrentPeriodEnd = interval == BillingInterval.Yearly
                ? DateTime.UtcNow.AddYears(1)
                : DateTime.UtcNow.AddMonths(1);
        }

        subscription.UpdatedAt = DateTime.UtcNow;
        AddHistory(tenant.Id, plan.Id, startTrial ? "TrialStarted" : "PlanChanged", fromStatus,
            SubscriptionStatusNames.ToApi(subscription.Status), price, subscription.SettlementCurrency, provider, null);
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

    private static SubscriptionPlanDto MapPlan(
        SubscriptionPlan plan,
        RegionalPrice? regional = null,
        string? requestedCurrency = null)
    {
        var monthly = regional?.MonthlyAmount ?? plan.MonthlyPrice;
        var annual = regional?.AnnualAmount ?? plan.AnnualPrice;
        var currency = regional?.CurrencyCode
            ?? (string.IsNullOrWhiteSpace(plan.BaseCurrency) ? "USD" : plan.BaseCurrency);
        var annualMonthly = annual / 12m;
        var savings = monthly > 0
            ? decimal.Round((1 - annualMonthly / monthly) * 100m, 0, MidpointRounding.AwayFromZero)
            : 0;
        string? note = regional?.ExchangeRateNote;
        if (regional is null &&
            CurrencyMath.TryNormalize(requestedCurrency, out var requested) &&
            !requested.Equals(currency, StringComparison.OrdinalIgnoreCase))
        {
            note = $"No regional price is configured for {requested}. The amount due is in {currency}.";
        }

        return new SubscriptionPlanDto
        {
            Id = plan.Id,
            Name = plan.Name,
            Slug = plan.Slug,
            Description = plan.Description,
            MonthlyPrice = monthly,
            AnnualPrice = annual,
            AnnualSavingsPercent = savings,
            MaxUsers = plan.MaxUsers,
            MaxCustomers = plan.MaxCustomers,
            MaxProducts = plan.MaxProducts,
            MaxProjects = plan.MaxProjects,
            MaxTasks = plan.MaxTasks,
            MaxStorageMb = plan.MaxStorageMb,
            MaxAiRequests = plan.MaxAiRequests,
            HasAiAssistant = plan.HasAiAssistant,
            HasAdvancedAnalytics = plan.HasAdvancedAnalytics,
            HasPdfReports = plan.HasPdfReports,
            HasAdvancedReports = plan.HasAdvancedReports,
            HasPrioritySupport = plan.HasPrioritySupport,
            HasApiAccess = plan.HasApiAccess,
            HasMultiBranch = plan.HasMultiBranch,
            InventoryAccess = plan.InventoryAccess,
            SalesAccess = plan.SalesAccess,
            TrialDays = plan.TrialDays,
            IsCustomPricing = plan.IsCustomPricing,
            IsRecommended = plan.Slug == "professional",
            Currency = currency,
            SettlementCurrency = currency,
            BaseCurrency = string.IsNullOrWhiteSpace(plan.BaseCurrency) ? "USD" : plan.BaseCurrency,
            BaseMonthlyPrice = plan.MonthlyPrice,
            BaseAnnualPrice = plan.AnnualPrice,
            CurrencyScale = CurrencyMath.Scale(currency),
            PriceNote = note,
            Features = SubscriptionCatalog.BuildFeatures(plan),
            SortOrder = plan.SortOrder
        };
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

        var (monthly, annual, contractedCurrency) = plan is null
            ? (0m, 0m, "USD")
            : EntitlementResolver.ResolvePrice(plan, subscription);
        var price = subscription.BillingInterval == BillingInterval.Yearly ? annual : monthly;
        var currency = subscription.SettlementCurrency
            ?? contractedCurrency;
        if (plan?.MonthlyPrice == 0 && subscription.Status == SubscriptionStatus.Trial)
            subscription.Status = SubscriptionStatus.Free;

        return new CurrentPlanDto
        {
            PlanId = plan?.Id ?? tenant.SubscriptionPlanId,
            PlanName = plan?.Name ?? tenant.SubscriptionPlan,
            PlanSlug = plan?.Slug ?? "free",
            Status = plan?.MonthlyPrice == 0 && subscription.Status is SubscriptionStatus.Trial or SubscriptionStatus.Active
                ? SubscriptionStatusNames.Free
                : SubscriptionStatusNames.ToApi(subscription.Status),
            BillingInterval = subscription.BillingInterval == BillingInterval.Yearly ? "Annual" : "Monthly",
            PaymentProvider = subscription.PaymentProvider.ToString(),
            StartDate = subscription.StartDate,
            TrialEndDate = subscription.TrialEndDate,
            CurrentPeriodEnd = subscription.CurrentPeriodEnd,
            RenewalDate = subscription.CancelAtPeriodEnd ? null : subscription.CurrentPeriodEnd ?? subscription.TrialEndDate,
            CancelAtPeriodEnd = subscription.CancelAtPeriodEnd,
            ScheduledCancellationDate = subscription.CancelAtPeriodEnd
                ? subscription.CancelEffectiveAt ?? subscription.CurrentPeriodEnd
                : null,
            IsTrialActive = isTrial && trialDaysRemaining > 0,
            TrialDaysRemaining = trialDaysRemaining,
            CurrentPrice = price,
            Currency = currency,
            SettlementCurrency = currency,
            BillingCountry = subscription.BillingCountry,
            AutoRenew = subscription.AutoRenewEnabled && !subscription.CancelAtPeriodEnd,
            PaymentMethodSummary = subscription.PaymentMethodSummary,
            CurrencyScale = CurrencyMath.Scale(currency),
            FeatureFlags = new PlanFeatureFlagsDto
            {
                HasAiAssistant = plan?.HasAiAssistant ?? false,
                HasAdvancedAnalytics = plan?.HasAdvancedAnalytics ?? false,
                HasPdfReports = plan?.HasPdfReports ?? false,
                HasAdvancedReports = plan?.HasAdvancedReports ?? false,
                HasPrioritySupport = plan?.HasPrioritySupport ?? false,
                HasApiAccess = plan?.HasApiAccess ?? false,
                HasMultiBranch = plan?.HasMultiBranch ?? false
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
        PaymentMethod = tx.PaymentMethod,
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
            "tap" => PaymentProviderType.Tap,
            "paytabs" => PaymentProviderType.PayTabs,
            _ => PaymentProviderType.Manual
        };

    private async Task<BillingTransaction> FindOrCreateCheckoutTransactionAsync(
        IdempotencyOutcome claim,
        IdempotencyRecord record,
        Guid tenantId,
        string providerName,
        decimal amount,
        string currency,
        string planName,
        CancellationToken cancellationToken)
    {
        if (claim.ResourceId is Guid existingId)
        {
            return await _context.BillingTransactions
                .FirstAsync(x => x.Id == existingId, cancellationToken);
        }

        var operationKey = ProviderOperationKey.Create(record.Id, providerName);
        var transaction = new BillingTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TransactionId = operationKey,
            ProviderOperationKey = operationKey,
            IdempotencyRecordId = record.Id,
            Amount = amount,
            Currency = currency,
            Status = BillingTransactionStatus.Pending,
            Provider = ParseProvider(providerName),
            Description = $"BusinessOS {planName} checkout",
            CreatedAt = DateTime.UtcNow
        };

        _context.BillingTransactions.Add(transaction);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbExceptionClassifier.IsUniqueViolation(ex))
        {
            _context.Detach(transaction);
            transaction = await _context.BillingTransactions
                .FirstAsync(x => x.IdempotencyRecordId == record.Id, cancellationToken);
        }

        await _idempotency.AttachResourceAsync(record.Id, transaction.Id, cancellationToken);
        return transaction;
    }

    private static CheckoutSessionDto ReadCheckoutSession(string? body)
    {
        var session = ReadStoredSession(body);
        if (session is null)
        {
            throw new ConflictException(
                "The original checkout result could not be read. Retry this request.",
                ErrorCodes.IdempotencyInProgress);
        }

        return session;
    }

    private static CheckoutSessionDto? ReadStoredSession(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            var session = JsonSerializer.Deserialize<CheckoutSessionDto>(body);
            return string.IsNullOrWhiteSpace(session?.SessionId) ? null : session;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsUnknownProviderOutcome(Exception exception) =>
        exception is HttpRequestException or TimeoutException or TaskCanceledException ||
        exception.GetType().Name == "StripeException";
}
