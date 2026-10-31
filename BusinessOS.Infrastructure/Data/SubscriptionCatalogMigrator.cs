using System.Text.Json;
using BusinessOS.Domain.Billing;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Data;

public static class SubscriptionCatalogMigrator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task ApplyAsync(
        BusinessOSDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var catalog = SubscriptionCatalog.CreatePlans();
        var subscriptions = await context.TenantSubscriptions
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);

        foreach (var plan in catalog)
        {
            var existing = await context.SubscriptionPlans
                .FirstOrDefaultAsync(x => x.Id == plan.Id || x.Slug == plan.Slug, cancellationToken);

            if (existing is null)
            {
                context.SubscriptionPlans.Add(plan);
                logger.LogInformation("Seeded subscription plan {Plan}", plan.Name);
                continue;
            }

            if (existing.CatalogRevision >= SubscriptionCatalog.Revision)
                continue;

            foreach (var subscription in subscriptions.Where(x => x.SubscriptionPlanId == existing.Id))
            {
                if (subscription.PreserveContract ||
                    !SubscriptionCatalog.ShouldPreserveContract(subscription.Status, existing.MonthlyPrice))
                {
                    continue;
                }

                subscription.PreserveContract = true;
                subscription.ContractedMonthlyPrice = existing.MonthlyPrice;
                subscription.ContractedAnnualPrice = existing.AnnualPrice;
                subscription.ContractedCurrency = string.IsNullOrWhiteSpace(existing.BaseCurrency)
                    ? SubscriptionCatalog.BaseCurrency
                    : existing.BaseCurrency;
                subscription.SettlementCurrency ??= subscription.ContractedCurrency;
                subscription.ContractedLimitsJson = JsonSerializer.Serialize(
                    SubscriptionCatalog.Snapshot(existing),
                    JsonOptions);
                if (subscription.TrialEndDate.HasValue)
                    subscription.TrialConsumed = true;

                context.SubscriptionHistories.Add(new SubscriptionHistory
                {
                    TenantId = subscription.TenantId,
                    SubscriptionPlanId = existing.Id,
                    Action = "ContractPreserved",
                    FromStatus = SubscriptionStatusNames.ToApi(subscription.Status),
                    ToStatus = SubscriptionStatusNames.ToApi(subscription.Status),
                    Amount = existing.MonthlyPrice,
                    Currency = subscription.ContractedCurrency,
                    Notes = "Public catalog changed. This paid subscription keeps its previous price and limits until the customer changes plan.",
                    CreatedAt = DateTime.UtcNow
                });
            }

            var stripeMonthly = existing.StripePriceIdMonthly;
            var stripeYearly = existing.StripePriceIdYearly;
            SubscriptionCatalog.CopyCatalog(existing, plan);
            existing.StripePriceIdMonthly = stripeMonthly;
            existing.StripePriceIdYearly = stripeYearly;
            logger.LogInformation("Updated public catalog for {Plan} to revision {Revision}", existing.Name, SubscriptionCatalog.Revision);
        }

        await SeedUsdPricesAsync(context, cancellationToken);
        await SeedProvidersAsync(context, logger, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedUsdPricesAsync(BusinessOSDbContext context, CancellationToken cancellationToken)
    {
        var plans = await context.SubscriptionPlans.ToListAsync(cancellationToken);
        foreach (var plan in plans.Where(x => x.CatalogRevision >= SubscriptionCatalog.Revision))
        {
            var exists = await context.RegionalPrices.AnyAsync(
                x => x.SubscriptionPlanId == plan.Id && x.CountryCode == "US" && x.CurrencyCode == "USD",
                cancellationToken);
            if (exists)
                continue;

            context.RegionalPrices.Add(new RegionalPrice
            {
                SubscriptionPlanId = plan.Id,
                CountryCode = "US",
                CurrencyCode = "USD",
                MonthlyAmount = plan.MonthlyPrice,
                AnnualAmount = plan.AnnualPrice,
                IsActive = true,
                ExchangeRateNote = "Configured USD price. This is not converted from another currency.",
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    private static async Task SeedProvidersAsync(
        BusinessOSDbContext context,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var providers = new[]
        {
            Provider(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), PaymentProviderType.Stripe, "Stripe",
                "Card and wallet payments via Stripe", true, "Stripe", "US", "USD,AED,GBP,EUR,CAD,AUD,SGD", true, true, true, 0),
            Provider(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), PaymentProviderType.JazzCash, "JazzCash",
                "Mobile wallet payments via JazzCash", false, "JazzCash", "PK", "PKR", false, false, false, 1),
            Provider(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), PaymentProviderType.EasyPaisa, "EasyPaisa",
                "Mobile wallet payments via EasyPaisa", false, "EasyPaisa", "PK", "PKR", false, false, false, 2),
            Provider(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), PaymentProviderType.Tap, "Tap Payments",
                "Hosted checkout via Tap Payments. Live charges require approved merchant credentials.", false, "Tap", "", "USD,AED,SAR,QAR,KWD,BHD,OMR", false, true, true, 3),
            Provider(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), PaymentProviderType.PayTabs, "PayTabs",
                "Hosted checkout via PayTabs. Live charges require an approved profile and server key.", false, "PayTabs", "", "AED,SAR,QAR,KWD,BHD,OMR,USD,EUR,GBP", false, true, false, 4)
        };

        foreach (var provider in providers)
        {
            var existing = await context.PaymentProviders
                .FirstOrDefaultAsync(x => x.ProviderType == provider.ProviderType, cancellationToken);
            if (existing is null)
            {
                context.PaymentProviders.Add(provider);
                logger.LogInformation("Seeded payment provider {Provider}", provider.Name);
                continue;
            }

            existing.MerchantCountry ??= provider.MerchantCountry;
            existing.SupportedCurrencies ??= provider.SupportedCurrencies;
            if (existing.ProviderType is PaymentProviderType.Tap or PaymentProviderType.PayTabs)
            {
                existing.Description = provider.Description;
                existing.SupportsOneTime = provider.SupportsOneTime;
                existing.SupportsRefunds = provider.SupportsRefunds;
            }

            if (existing.ProviderType == PaymentProviderType.Stripe)
            {
                existing.SupportsRecurring = true;
                existing.SupportsRefunds = true;
                existing.SupportsTokenization = true;
                existing.SupportedCurrencies ??= provider.SupportedCurrencies;
            }
        }
    }

    private static PaymentProvider Provider(
        Guid id,
        PaymentProviderType type,
        string name,
        string description,
        bool enabled,
        string key,
        string country,
        string currencies,
        bool recurring,
        bool refunds,
        bool tokenization,
        int sort) => new()
    {
        Id = id,
        ProviderType = type,
        Name = name,
        Description = description,
        IsEnabled = enabled,
        IsSandbox = true,
        ConfigurationKey = key,
        MerchantCountry = country,
        SupportedCurrencies = currencies,
        SupportsOneTime = true,
        SupportsRecurring = recurring,
        SupportsRefunds = refunds,
        SupportsTokenization = tokenization,
        SortOrder = sort,
        CreatedAt = DateTime.UtcNow
    };
}
