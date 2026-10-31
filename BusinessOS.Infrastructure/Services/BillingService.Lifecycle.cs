using System.Text.Json;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Features.Billing.DTOs;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Domain.Billing;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace BusinessOS.Infrastructure.Services;

public sealed partial class BillingService
{
    public async Task<PlanChangeResultDto> ChangePlanAsync(
        ChangePlanRequest request,
        CancellationToken cancellationToken = default,
        string? idempotencyKey = null)
    {
        var (tenant, subscription) = await GetTenantWithSubscriptionAsync(cancellationToken, tracked: request.Confirm);
        var plan = await _context.SubscriptionPlans
            .FirstOrDefaultAsync(x => x.Id == request.PlanId && x.IsActive && x.IsPublic, cancellationToken)
            ?? throw new NotFoundException("Plan not found.");

        var interval = ParseBillingInterval(request.BillingInterval);
        var samePlan = subscription.SubscriptionPlanId == plan.Id && subscription.BillingInterval == interval;
        if (samePlan &&
            !request.StartTrial &&
            subscription.Status is SubscriptionStatus.Active or SubscriptionStatus.Trial or SubscriptionStatus.Free &&
            !subscription.CancelAtPeriodEnd)
        {
            throw new ConflictException("An active subscription for this plan already exists.");
        }

        if (!string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId) &&
            request.Provider is not null &&
            ParseProvider(request.Provider) != subscription.PaymentProvider &&
            ParseProvider(request.Provider) != PaymentProviderType.Stripe)
        {
            throw new BadRequestException(
                "An existing recurring subscription is not moved to another provider without a new authorized checkout.");
        }

        if (request.StartTrial)
        {
            if (plan.MonthlyPrice <= 0)
                throw new BadRequestException("The free plan does not use a trial.");
            if (subscription.TrialConsumed)
                throw new BadRequestException("This workspace has already used its free trial.");
        }

        var preview = await BuildPreviewAsync(tenant, subscription, plan, interval, request, cancellationToken);
        if (!request.Confirm)
        {
            return new PlanChangeResultDto { Applied = false, Preview = preview };
        }

        if (request.StartTrial)
        {
            await ApplyPlanChangeAsync(
                tenant, subscription, plan, request.BillingInterval,
                request.Provider ?? PaymentProviderType.Manual.ToString(),
                cancellationToken, verified: true, startTrial: true);
            await _auditService.LogAsync(tenant.Id, "TrialStarted", "TenantSubscription", null,
                RbacAuditService.Serialize(new { plan.Name }), cancellationToken: cancellationToken);
            return new PlanChangeResultDto
            {
                Applied = true,
                Preview = preview,
                Subscription = MapCurrentPlan(tenant, subscription)
            };
        }

        if (preview.RequiresCheckout &&
            !string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId) &&
            _stripeService.IsConfigured &&
            ParseProvider(request.Provider ?? "stripe") == PaymentProviderType.Stripe)
        {
            var (recurringAmount, recurringCurrency, _) = await ResolveCheckoutAmountAsync(
                plan,
                subscription,
                interval == BillingInterval.Yearly,
                "stripe",
                request.Currency,
                request.Country,
                cancellationToken);
            var change = await _stripeService.ChangeSubscriptionPriceAsync(
                subscription.StripeSubscriptionId,
                tenant.Id,
                plan.Id,
                plan.Name,
                request.BillingInterval,
                recurringAmount,
                recurringCurrency,
                idempotencyKey,
                cancellationToken);
            if (!change.Confirmed)
            {
                subscription.Status = change.Status == "past_due"
                    ? SubscriptionStatus.PastDue
                    : SubscriptionStatus.PendingPayment;
                subscription.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
                preview.AmountDue = change.AmountDue;
                preview.Currency = change.Currency;
                preview.SettlementCurrency = change.Currency;
                preview.ProrationNote = "Stripe has not confirmed this change. The current plan stays in place until the invoice is paid.";
                return new PlanChangeResultDto { Applied = false, Preview = preview };
            }

            await ApplyPlanChangeAsync(
                tenant, subscription, plan, request.BillingInterval, "stripe",
                cancellationToken, verified: true);
            subscription.SettlementCurrency = change.Currency;
            subscription.DisplayCurrency ??= change.Currency;
            if (change.PeriodEnd is DateTime periodEnd)
                subscription.CurrentPeriodEnd = periodEnd;
            if (change.AmountDue > 0)
            {
                var operationKey = string.IsNullOrWhiteSpace(idempotencyKey)
                    ? change.InvoiceId ?? Guid.NewGuid().ToString("N")
                    : idempotencyKey;
                _context.BillingTransactions.Add(new BillingTransaction
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    TransactionId = operationKey,
                    ProviderOperationKey = operationKey,
                    Amount = change.AmountDue,
                    Currency = change.Currency,
                    Status = BillingTransactionStatus.Completed,
                    Provider = PaymentProviderType.Stripe,
                    ProviderReference = change.InvoiceId,
                    PaymentMethod = "card",
                    Description = $"Prorated change to {plan.Name}",
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                });
                await CreateInvoiceForAsync(
                    tenant.Id, plan, subscription.BillingInterval, PaymentProviderType.Stripe,
                    change.AmountDue, change.Currency, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            preview.AmountDue = change.AmountDue;
            preview.Currency = change.Currency;
            preview.SettlementCurrency = change.Currency;
            preview.ProrationNote = "Stripe calculated this prorated charge and confirmed the subscription change.";
            return new PlanChangeResultDto
            {
                Applied = true,
                Preview = preview,
                Subscription = MapCurrentPlan(tenant, subscription)
            };
        }

        if (preview.RequiresCheckout)
        {
            var session = await CreateCheckoutSessionAsync(
                new CheckoutRequest(
                    plan.Id,
                    request.BillingInterval,
                    request.Provider ?? "stripe",
                    request.SuccessUrl,
                    request.CancelUrl,
                    request.Country,
                    request.Currency),
                cancellationToken,
                idempotencyKey ?? Guid.NewGuid().ToString("N"));
            return new PlanChangeResultDto
            {
                Applied = false,
                CheckoutRequired = true,
                Preview = preview,
                Checkout = session
            };
        }

        if (plan.MonthlyPrice <= 0 &&
            !request.StartTrial &&
            !string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId) &&
            _stripeService.IsConfigured)
        {
            await _stripeService.SetCancelAtPeriodEndAsync(subscription.StripeSubscriptionId, true, cancellationToken);
            subscription.CancelAtPeriodEnd = true;
            subscription.CancelEffectiveAt = subscription.CurrentPeriodEnd;
            subscription.ScheduledPlanId = plan.Id;
            subscription.AutoRenewEnabled = false;
            subscription.UpdatedAt = DateTime.UtcNow;
            AddHistory(tenant.Id, plan.Id, "DowngradeScheduled", SubscriptionStatusNames.ToApi(subscription.Status),
                SubscriptionStatusNames.ToApi(subscription.Status), 0, subscription.SettlementCurrency, "stripe",
                "Paid access continues until the current period ends. Stripe will not renew.");
            await _context.SaveChangesAsync(cancellationToken);
            preview.AmountDue = 0;
            preview.ProrationNote = "The paid subscription stays active until the current period ends. Stripe will not renew it.";
            return new PlanChangeResultDto
            {
                Applied = true,
                Preview = preview,
                Subscription = MapCurrentPlan(tenant, subscription)
            };
        }

        await ApplyPlanChangeAsync(
            tenant, subscription, plan, request.BillingInterval,
            request.Provider ?? subscription.PaymentProvider.ToString(),
            cancellationToken, verified: true);
        return new PlanChangeResultDto
        {
            Applied = true,
            Preview = preview,
            Subscription = MapCurrentPlan(tenant, subscription)
        };
    }

    public async Task<CurrentPlanDto> ReactivateAsync(CancellationToken cancellationToken = default)
    {
        var (tenant, subscription) = await GetTenantWithSubscriptionAsync(cancellationToken, tracked: true);
        if (!subscription.CancelAtPeriodEnd && subscription.Status != SubscriptionStatus.Cancelled)
            throw new BadRequestException("This subscription is not scheduled for cancellation.");

        if (subscription.Status == SubscriptionStatus.Cancelled &&
            subscription.CurrentPeriodEnd is DateTime end &&
            end < DateTime.UtcNow)
        {
            throw new BadRequestException("This subscription has already ended. Start a new checkout to subscribe again.");
        }

        if (_stripeService.IsConfigured && !string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId))
            await _stripeService.SetCancelAtPeriodEndAsync(subscription.StripeSubscriptionId, false, cancellationToken);

        var from = SubscriptionStatusNames.ToApi(subscription.Status);
        subscription.CancelAtPeriodEnd = false;
        subscription.CancelEffectiveAt = null;
        if (subscription.Status == SubscriptionStatus.Cancelled)
            subscription.Status = SubscriptionStatus.Active;
        subscription.AutoRenewEnabled = subscription.PaymentProvider == PaymentProviderType.Stripe &&
            !string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId);
        subscription.UpdatedAt = DateTime.UtcNow;
        tenant.Status = TenantStatus.Active;
        AddHistory(tenant.Id, subscription.SubscriptionPlanId, "Reactivated", from,
            SubscriptionStatusNames.ToApi(subscription.Status), null, subscription.SettlementCurrency,
            subscription.PaymentProvider.ToString(), "Scheduled cancellation removed.");
        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(tenant.Id, "SubscriptionReactivated", "TenantSubscription", null, null,
            cancellationToken: cancellationToken);
        return MapCurrentPlan(tenant, subscription);
    }

    public async Task<CurrentPlanDto> ConfirmSandboxCheckoutAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment() || _stripeService.IsConfigured)
            throw new BadRequestException("Sandbox confirmation is only available in development when Stripe is not configured.");

        if (string.IsNullOrWhiteSpace(sessionId))
            throw new BadRequestException("Checkout session is required.");

        var tenantId = RequireTenantId();
        var transaction = await _context.BillingTransactions
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && (x.ProviderReference == sessionId || x.TransactionId == sessionId), cancellationToken)
            ?? throw new NotFoundException("Checkout session not found.");

        if (transaction.Status == BillingTransactionStatus.Completed)
            return await GetCurrentPlanAsync(cancellationToken);

        if (transaction.Status != BillingTransactionStatus.Pending)
            throw new BadRequestException("This checkout can no longer be confirmed.");

        var session = ReadStoredSession(transaction.Metadata)
            ?? throw new BadRequestException("Checkout session is missing plan details.");
        if (!session.RequiresServerConfirmation)
            throw new BadRequestException("This checkout must be confirmed by the payment provider.");

        var plan = await _context.SubscriptionPlans.FirstAsync(x => x.Id == session.PlanId, cancellationToken);
        var (tenant, subscription) = await GetTenantWithSubscriptionAsync(cancellationToken, tracked: true);
        await ApplyPlanChangeAsync(tenant, subscription, plan, session.BillingInterval, session.Provider, cancellationToken, verified: true);
        subscription.SettlementCurrency = session.Currency;
        subscription.PaymentProvider = ParseProvider(session.Provider);
        transaction.Status = BillingTransactionStatus.Completed;
        transaction.CompletedAt = DateTime.UtcNow;
        transaction.Currency = session.Currency;
        transaction.PaymentMethod = "sandbox";
        await CreateInvoiceForAsync(tenant.Id, plan, subscription.BillingInterval, subscription.PaymentProvider, session.Amount, session.Currency, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return MapCurrentPlan(tenant, subscription);
    }

    public async Task<IReadOnlyList<SubscriptionHistoryDto>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var rows = await _context.SubscriptionHistories
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        return rows.Select(x => new SubscriptionHistoryDto
        {
            Id = x.Id,
            Action = x.Action,
            FromStatus = x.FromStatus,
            ToStatus = x.ToStatus,
            Amount = x.Amount,
            Currency = x.Currency,
            Provider = x.Provider,
            Notes = x.Notes,
            CreatedAt = x.CreatedAt
        }).ToList();
    }

    public async Task<IReadOnlyList<PaymentMethodCapabilityDto>> GetPaymentMethodsAsync(
        string? currency,
        string? country,
        CancellationToken cancellationToken = default)
    {
        var enabled = await EnabledProviderCodesAsync(cancellationToken);
        var code = currency;
        if (!CurrencyMath.TryNormalize(code, out var normalized))
            normalized = "USD";
        if (string.IsNullOrWhiteSpace(currency))
            normalized = "USD";

        return PaymentMethodCatalog.For(normalized, enabled)
            .Select(x => new PaymentMethodCapabilityDto
            {
                Code = x.Code,
                Name = x.Name,
                Region = x.Region,
                OneTime = x.OneTime,
                Recurring = x.Recurring,
                CustomerInitiatedRenewal = x.CustomerInitiatedRenewal,
                Refunds = x.Refunds,
                Tokenization = x.Tokenization,
                Providers = x.Providers
            })
            .ToList();
    }

    public async Task<byte[]> DownloadInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var invoice = await _context.BillingInvoices
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == invoiceId && x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Invoice not found.");

        return BillingInvoicePdf.Render(invoice);
    }

    public async Task<CurrentPlanDto> SetEnterpriseContractAsync(
        SetEnterpriseContractRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!CurrencyMath.TryNormalize(request.Currency, out var currency))
            throw new BadRequestException("Currency is not supported.");

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var tenant = await db.Tenants.IgnoreQueryFilters()
            .Include(x => x.Plan)
            .FirstOrDefaultAsync(x => x.Id == request.TenantId && !x.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Tenant not found.");

        var enterprise = await db.SubscriptionPlans
            .FirstAsync(x => x.Slug == "enterprise", cancellationToken);
        var subscription = await db.TenantSubscriptions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && !x.IsDeleted, cancellationToken);
        if (subscription is null)
        {
            subscription = new TenantSubscription
            {
                TenantId = tenant.Id,
                CreatedAt = DateTime.UtcNow
            };
            db.TenantSubscriptions.Add(subscription);
        }

        var limits = new ContractedEntitlement
        {
            MonthlyPrice = CurrencyMath.Round(request.MonthlyPrice, currency),
            AnnualPrice = CurrencyMath.Round(request.AnnualPrice, currency),
            Currency = currency,
            MaxUsers = request.MaxUsers ?? enterprise.MaxUsers,
            MaxCustomers = request.MaxCustomers ?? enterprise.MaxCustomers,
            MaxProducts = request.MaxProducts ?? enterprise.MaxProducts,
            MaxProjects = request.MaxProjects ?? enterprise.MaxProjects,
            MaxTasks = request.MaxTasks ?? enterprise.MaxTasks,
            MaxStorageMb = enterprise.MaxStorageMb,
            MaxAiRequests = request.MaxAiRequests ?? enterprise.MaxAiRequests,
            HasAiAssistant = true,
            HasAdvancedAnalytics = true,
            HasPdfReports = true,
            HasAdvancedReports = true,
            HasPrioritySupport = request.HasPrioritySupport,
            HasApiAccess = request.HasApiAccess,
            HasMultiBranch = request.HasMultiBranch,
            InventoryAccess = "Full",
            SalesAccess = "Full"
        };

        tenant.SubscriptionPlanId = enterprise.Id;
        tenant.SubscriptionPlan = enterprise.Name;
        subscription.SubscriptionPlanId = enterprise.Id;
        subscription.PreserveContract = true;
        subscription.ContractedMonthlyPrice = limits.MonthlyPrice;
        subscription.ContractedAnnualPrice = limits.AnnualPrice;
        subscription.ContractedCurrency = currency;
        subscription.SettlementCurrency = currency;
        subscription.ContractedLimitsJson = EntitlementResolver.Serialize(limits);
        subscription.AiRequestAllowance = request.MaxAiRequests;
        subscription.UpdatedAt = DateTime.UtcNow;
        db.SubscriptionHistories.Add(new SubscriptionHistory
        {
            TenantId = tenant.Id,
            SubscriptionPlanId = enterprise.Id,
            Action = "EnterpriseContract",
            ToStatus = SubscriptionStatusNames.ToApi(subscription.Status),
            Amount = limits.MonthlyPrice,
            Currency = currency,
            Notes = "Negotiated Enterprise contract saved. A provider subscription is not changed automatically.",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);

        tenant.Plan = enterprise;
        return MapCurrentPlan(tenant, subscription);
    }

    private async Task<PlanChangePreviewDto> BuildPreviewAsync(
        Tenant tenant,
        TenantSubscription subscription,
        SubscriptionPlan plan,
        BillingInterval interval,
        ChangePlanRequest request,
        CancellationToken cancellationToken)
    {
        var (amount, currency, note) = await ResolveCheckoutAmountAsync(
            plan,
            subscription,
            interval == BillingInterval.Yearly,
            (request.Provider ?? "stripe").ToLowerInvariant(),
            request.Currency,
            request.Country,
            cancellationToken);

        var current = EntitlementResolver.ResolvePrice(tenant.Plan, subscription);
        var currentPrice = subscription.BillingInterval == BillingInterval.Yearly ? current.Annual : current.Monthly;
        var periodEnd = subscription.CurrentPeriodEnd ?? DateTime.UtcNow;
        var periodStart = subscription.BillingInterval == BillingInterval.Yearly
            ? periodEnd.AddYears(-1)
            : periodEnd.AddMonths(-1);
        var due = amount == 0 || request.StartTrial
            ? 0
            : ProrationCalculator.EstimateAmountDue(currentPrice, amount, periodStart, periodEnd, DateTime.UtcNow, currency);

        var currentRank = tenant.Plan?.SortOrder ?? 0;
        var isDowngrade = plan.SortOrder < currentRank;
        var requiresCheckout = amount > 0 && !request.StartTrial;
        var proration = request.StartTrial
            ? "The trial starts immediately. No charge is created until the trial ends and a payment method is confirmed."
            : amount == 0
                ? "No payment is due."
                : "This amount is an estimate of the unused time on the current period. The payment provider calculates the final prorated charge. The subscription is not activated until that charge is confirmed.";

        return new PlanChangePreviewDto
        {
            PlanId = plan.Id,
            PlanName = plan.Name,
            BillingInterval = interval == BillingInterval.Yearly ? "Annual" : "Monthly",
            AmountDue = due,
            Currency = currency,
            SettlementCurrency = currency,
            IsDowngrade = isDowngrade,
            RequiresCheckout = requiresCheckout,
            IsTrial = request.StartTrial,
            ProrationNote = proration,
            PriceNote = note
        };
    }

    private async Task<(decimal Amount, string Currency, string? Note)> ResolveCheckoutAmountAsync(
        SubscriptionPlan plan,
        TenantSubscription subscription,
        bool yearly,
        string providerName,
        string? requestedCurrency,
        string? country,
        CancellationToken cancellationToken)
    {
        if (providerName is "jazzcash" or "easypaisa")
        {
            var pkr = await _context.RegionalPrices.AsNoTracking().FirstOrDefaultAsync(
                x => x.SubscriptionPlanId == plan.Id && x.IsActive && x.CurrencyCode == "PKR" &&
                     (country == null || x.CountryCode == country || x.CountryCode == "PK"),
                cancellationToken);
            if (pkr is null)
            {
                throw new BadRequestException(
                    "JazzCash and EasyPaisa require a configured PKR price. BusinessOS does not convert USD prices with an exchange rate.");
            }

            return (yearly ? pkr.AnnualAmount : pkr.MonthlyAmount, "PKR", pkr.ExchangeRateNote);
        }

        if (subscription.PreserveContract &&
            subscription.SubscriptionPlanId == plan.Id &&
            subscription.ContractedMonthlyPrice is decimal contracted)
        {
            var contractedCurrency = subscription.ContractedCurrency ?? "USD";
            return (
                yearly ? subscription.ContractedAnnualPrice ?? plan.AnnualPrice : contracted,
                contractedCurrency,
                "This charge uses the price locked on the existing contract.");
        }

        var regionalPrices = await _context.RegionalPrices.AsNoTracking()
            .Where(x => x.SubscriptionPlanId == plan.Id && x.IsActive)
            .ToListAsync(cancellationToken);
        var regional = ResolveRegional(plan, regionalPrices, requestedCurrency, country);
        if (regional is not null)
            return (yearly ? regional.AnnualAmount : regional.MonthlyAmount, regional.CurrencyCode, regional.ExchangeRateNote);

        var currency = string.IsNullOrWhiteSpace(plan.BaseCurrency) ? "USD" : plan.BaseCurrency;
        string? note = null;
        if (CurrencyMath.TryNormalize(requestedCurrency, out var requested) &&
            !requested.Equals(currency, StringComparison.OrdinalIgnoreCase))
        {
            note = $"No regional price is configured for {requested}. The amount due is in {currency}.";
        }

        return (yearly ? plan.AnnualPrice : plan.MonthlyPrice, currency, note);
    }

    private static RegionalPrice? ResolveRegional(
        SubscriptionPlan plan,
        IReadOnlyList<RegionalPrice> prices,
        string? currency,
        string? country)
    {
        var matches = prices.Where(x => x.SubscriptionPlanId == plan.Id && x.IsActive).ToList();
        if (!string.IsNullOrWhiteSpace(country))
        {
            var countryMatch = matches.FirstOrDefault(x =>
                x.CountryCode.Equals(country, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(currency) || x.CurrencyCode.Equals(currency, StringComparison.OrdinalIgnoreCase)));
            if (countryMatch is not null)
                return countryMatch;
        }

        if (!string.IsNullOrWhiteSpace(currency))
        {
            return matches.FirstOrDefault(x => x.CurrencyCode.Equals(currency, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private async Task<HashSet<string>> EnabledProviderCodesAsync(CancellationToken cancellationToken)
    {
        var providers = await _context.PaymentProviders.AsNoTracking().Where(x => x.IsEnabled).ToListAsync(cancellationToken);
        var codes = providers.Select(x => x.ProviderType.ToString().ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (_stripeService.IsConfigured)
            codes.Add("stripe");
        return codes;
    }

    private async Task CreateInvoiceForAsync(
        Guid tenantId,
        SubscriptionPlan plan,
        BillingInterval interval,
        PaymentProviderType provider,
        decimal amount,
        string currency,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var rounded = CurrencyMath.Round(amount, currency);
        _context.BillingInvoices.Add(new BillingInvoice
        {
            TenantId = tenantId,
            InvoiceNumber = $"INV-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}",
            SubscriptionPlanId = plan.Id,
            PlanName = plan.Name,
            Subtotal = rounded,
            TaxAmount = 0,
            TotalAmount = rounded,
            Currency = currency,
            Status = BillingInvoiceStatus.Paid,
            BillingInterval = interval,
            PaymentProvider = provider,
            PeriodStart = now,
            PeriodEnd = interval == BillingInterval.Yearly ? now.AddYears(1) : now.AddMonths(1),
            PaidAt = now,
            CreatedAt = now
        });
        await Task.CompletedTask;
    }

    private void AddHistory(
        Guid tenantId,
        Guid? planId,
        string action,
        string? fromStatus,
        string? toStatus,
        decimal? amount,
        string? currency,
        string? provider,
        string? notes)
    {
        _context.SubscriptionHistories.Add(new SubscriptionHistory
        {
            TenantId = tenantId,
            SubscriptionPlanId = planId,
            Action = action,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Amount = amount,
            Currency = currency,
            Provider = provider,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        });
    }
}
