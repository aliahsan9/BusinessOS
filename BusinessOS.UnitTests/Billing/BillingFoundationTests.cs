using BusinessOS.Application.Common.Idempotency;
using BusinessOS.Domain.Billing;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Payments;
using BusinessOS.Infrastructure.Services;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BusinessOS.UnitTests.Billing;

public class BillingCurrencyTests
{
    [Theory]
    [InlineData(19.99, "USD", 1999)]
    [InlineData(1.234, "KWD", 1234)]
    [InlineData(1.234, "BHD", 1234)]
    [InlineData(182, "USD", 18200)]
    public void Minor_units_keep_currency_scale(decimal amount, string currency, long expected)
    {
        CurrencyMath.ToMinorUnits(amount, currency).Should().Be(expected);
        CurrencyMath.FromMinorUnits(expected, currency).Should().Be(CurrencyMath.Round(amount, currency));
    }

    [Fact]
    public void Rounding_uses_decimal_not_binary_float()
    {
        var rounded = CurrencyMath.Round(1.005m, "USD");
        rounded.Should().Be(1.01m);
        CurrencyMath.Scale("OMR").Should().Be(3);
    }

    [Fact]
    public void Unsupported_currency_is_rejected()
    {
        var ok = CurrencyMath.TryNormalize("XYZ", out _);
        ok.Should().BeFalse();
    }
}

public class BillingCatalogTests
{
    [Fact]
    public void Public_catalog_matches_the_commercial_packages()
    {
        var plans = SubscriptionCatalog.CreatePlans().ToDictionary(x => x.Slug);

        plans["free"].MonthlyPrice.Should().Be(0);
        plans["free"].MaxUsers.Should().Be(1);
        plans["free"].MaxCustomers.Should().Be(25);
        plans["free"].MaxProducts.Should().Be(50);
        plans["free"].MaxProjects.Should().Be(3);
        plans["free"].MaxAiRequests.Should().Be(20);
        plans["free"].HasApiAccess.Should().BeFalse();

        plans["starter"].MonthlyPrice.Should().Be(19);
        plans["starter"].AnnualPrice.Should().Be(182);
        plans["starter"].MaxUsers.Should().Be(3);
        plans["starter"].MaxAiRequests.Should().Be(100);
        plans["starter"].HasPdfReports.Should().BeTrue();
        plans["starter"].HasAdvancedAnalytics.Should().BeFalse();

        plans["professional"].MonthlyPrice.Should().Be(49);
        plans["professional"].AnnualPrice.Should().Be(470);
        plans["professional"].MaxUsers.Should().Be(10);
        plans["professional"].MaxAiRequests.Should().Be(500);
        plans["professional"].HasApiAccess.Should().BeTrue();

        plans["enterprise"].MonthlyPrice.Should().Be(149);
        plans["enterprise"].AnnualPrice.Should().Be(1430);
        plans["enterprise"].IsCustomPricing.Should().BeTrue();
        plans["enterprise"].HasMultiBranch.Should().BeTrue();
        plans["enterprise"].HasPrioritySupport.Should().BeTrue();
    }

    [Fact]
    public async Task Paid_subscriber_keeps_the_previous_contract_when_the_catalog_changes()
    {
        var (context, tenantId, _) = TenantDatabase.Open();
        var oldProfessional = new SubscriptionPlan
        {
            Id = SubscriptionCatalog.ProfessionalPlanId,
            Name = "Professional",
            Slug = "professional",
            MonthlyPrice = 79,
            AnnualPrice = 790,
            MaxUsers = 20,
            MaxCustomers = -1,
            MaxProducts = 100,
            MaxProjects = -1,
            MaxTasks = -1,
            MaxStorageMb = 51200,
            MaxAiRequests = -1,
            BaseCurrency = "USD",
            IsActive = true,
            IsPublic = true,
            CatalogRevision = 1
        };
        context.SubscriptionPlans.Add(oldProfessional);
        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Acme",
            Slug = "acme",
            Email = "a@example.com",
            Phone = "1",
            Address = "1",
            BusinessType = "Retail",
            SubscriptionPlanId = oldProfessional.Id,
            SubscriptionPlan = "Professional",
            OwnerUserId = "owner",
            Currency = "PKR"
        });
        context.TenantSubscriptions.Add(new TenantSubscription
        {
            TenantId = tenantId,
            SubscriptionPlanId = oldProfessional.Id,
            Status = SubscriptionStatus.Active,
            BillingInterval = BillingInterval.Monthly
        });
        context.SaveChanges();

        await SubscriptionCatalogMigrator.ApplyAsync(context, NullLogger.Instance);

        var plan = context.SubscriptionPlans.Single(x => x.Slug == "professional");
        var subscription = context.TenantSubscriptions.IgnoreQueryFilters().Single(x => x.TenantId == tenantId);
        plan.MonthlyPrice.Should().Be(49);
        plan.CatalogRevision.Should().Be(SubscriptionCatalog.Revision);
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.PreserveContract.Should().BeTrue();
        subscription.ContractedMonthlyPrice.Should().Be(79);
        subscription.ContractedCurrency.Should().Be("USD");
    }

    [Fact]
    public void Proration_estimate_is_decimal()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var midway = new DateTime(2026, 1, 16, 12, 0, 0, DateTimeKind.Utc);
        var due = ProrationCalculator.EstimateAmountDue(19m, 49m, start, end, midway, "USD");
        due.Should().BeGreaterThan(0);
        due.Should().BeLessThan(49m);
    }

    [Fact]
    public void Status_names_keep_stored_values()
    {
        SubscriptionStatusNames.ToApi(SubscriptionStatus.Trial).Should().Be("Trialing");
        SubscriptionStatusNames.ToApi(SubscriptionStatus.Cancelled).Should().Be("Canceled");
        ((int)SubscriptionStatus.Trial).Should().Be(0);
        ((int)SubscriptionStatus.Free).Should().Be(5);
    }
}

public class BillingSecurityTests
{
    [Fact]
    public async Task Invalid_stripe_signature_is_rejected()
    {
        var service = new StripePaymentService(
            Options.Create(new StripeOptions { SecretKey = "sk_test_example", WebhookSecret = "whsec_test" }),
            new StubServiceProvider(),
            NullLogger<StripePaymentService>.Instance);

        var act = () => service.HandleWebhookAsync("{}", "t=1,v1=bad", CancellationToken.None);
        await act.Should().ThrowAsync<Application.Common.Exceptions.UnauthorizedException>();
    }

    [Fact]
    public async Task Duplicate_webhook_claim_is_not_processed_twice()
    {
        var (context, _, _) = TenantDatabase.Open();
        var store = new WebhookEventStore(context);
        var first = await store.TryBeginAsync(PaymentProviderType.Stripe, "evt_1");
        await store.CompleteAsync(((WebhookClaim.Started)first).EventRecordId, null);
        var second = await store.TryBeginAsync(PaymentProviderType.Stripe, "evt_1");
        second.Should().BeOfType<WebhookClaim.Duplicate>();
    }

    [Fact]
    public void Tap_signature_rejects_a_modified_payload()
    {
        const string secret = "sk_test_secret";
        const string payload = """{"id":"chg_1","amount":19.00,"currency":"USD","status":"CAPTURED","reference":{"gateway":"g","payment":"p"},"transaction":{"created":"1"}}""";
        ProviderWebhookSignatures.TapMatches(payload, "not-the-signature", secret).Should().BeFalse();
    }

    [Fact]
    public void Return_urls_must_match_an_allowed_origin()
    {
        var act = () => BillingReturnUrls.Validate("https://evil.example/billing", ["http://localhost:4200"], allowLocalHttp: true);
        act.Should().Throw<Application.Common.Exceptions.BadRequestException>();
        var local = () => BillingReturnUrls.Validate("http://localhost:4200/billing", ["http://localhost:4200"], allowLocalHttp: true);
        local.Should().NotThrow();
    }

    [Fact]
    public void Ai_period_follows_the_subscription_boundary()
    {
        var end = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc);
        var start = TenantLimitService.ResolveAiPeriodStart(
            new TenantSubscription { CurrentPeriodEnd = end, BillingInterval = BillingInterval.Monthly },
            new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));
        start.Should().Be(end.AddMonths(-1));
    }

    private sealed class StubServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
