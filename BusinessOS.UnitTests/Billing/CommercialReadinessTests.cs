using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Domain.Billing;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Services;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.UnitTests.Billing;

public class CommercialEntitlementTests
{
    [Fact]
    public void Free_plan_does_not_include_professional_integrations()
    {
        var access = CommercialEntitlementEvaluator.Evaluate(Input(Plan("free"), SubscriptionStatus.Active));

        access.IsEnabled(CommercialFeatureKeys.Integrations).Should().BeFalse();
        access.IsEnabled(CommercialFeatureKeys.ClientPortal).Should().BeFalse();
        access.IsEnabled(CommercialFeatureKeys.MultiAgent).Should().BeFalse();
        access.IsEnabled(CommercialFeatureKeys.AiAssistant).Should().BeTrue();
        access.AgentExecutionLimit.Should().Be(10);
    }

    [Fact]
    public void Professional_plan_includes_integrations_and_portal()
    {
        var access = CommercialEntitlementEvaluator.Evaluate(Input(Plan("professional"), SubscriptionStatus.Active));

        access.IsEnabled(CommercialFeatureKeys.Integrations).Should().BeTrue();
        access.IsEnabled(CommercialFeatureKeys.ClientPortal).Should().BeTrue();
        access.IsEnabled(CommercialFeatureKeys.Storefront).Should().BeTrue();
        access.IsEnabled(CommercialFeatureKeys.MultiAgent).Should().BeTrue();
        access.IsEnabled(CommercialFeatureKeys.AdvancedSecurity).Should().BeFalse();
    }

    [Fact]
    public void Company_override_cannot_be_bypassed_by_asking_for_another_plan()
    {
        var input = Input(Plan("free"), SubscriptionStatus.Active);
        input.CompanyOverrides =
        [
            new FeatureGrantState(CommercialFeatureKeys.Integrations, false, null, DateTime.UtcNow.AddDays(-1), null)
        ];

        var access = CommercialEntitlementEvaluator.Evaluate(input);

        access.IsEnabled(CommercialFeatureKeys.Integrations).Should().BeFalse();
    }

    [Fact]
    public void Enterprise_override_enables_a_feature_the_free_catalog_does_not()
    {
        var input = Input(Plan("free"), SubscriptionStatus.Active);
        input.CompanyOverrides =
        [
            new FeatureGrantState(CommercialFeatureKeys.Integrations, true, 5, DateTime.UtcNow.AddDays(-1), null)
        ];

        var access = CommercialEntitlementEvaluator.Evaluate(input);

        access.IsEnabled(CommercialFeatureKeys.Integrations).Should().BeTrue();
    }

    [Fact]
    public void Immediate_cancellation_restricts_paid_features_and_keeps_a_finite_allowance()
    {
        var access = CommercialEntitlementEvaluator.Evaluate(Input(Plan("professional"), SubscriptionStatus.Cancelled));

        access.Mode.Should().Be(CommercialAccessMode.Restricted);
        access.IsEnabled(CommercialFeatureKeys.Integrations).Should().BeFalse();
        access.AgentExecutionLimit.Should().Be(10);
    }

    [Fact]
    public void Past_due_inside_grace_keeps_the_paid_feature()
    {
        var input = Input(Plan("professional"), SubscriptionStatus.PastDue);
        input.GraceEndsAt = DateTime.UtcNow.AddDays(2);

        var access = CommercialEntitlementEvaluator.Evaluate(input);

        access.Mode.Should().Be(CommercialAccessMode.Grace);
        access.IsEnabled(CommercialFeatureKeys.Integrations).Should().BeTrue();
    }

    [Fact]
    public void A_future_grant_does_not_apply_early()
    {
        var input = Input(Plan("free"), SubscriptionStatus.Active);
        input.PlanGrants =
        [
            new FeatureGrantState(CommercialFeatureKeys.Integrations, true, null, DateTime.UtcNow.AddDays(3), null)
        ];

        CommercialEntitlementEvaluator.Evaluate(input)
            .IsEnabled(CommercialFeatureKeys.Integrations)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(7, 1, 10, QuotaOutcome.Warning)]
    [InlineData(10, 1, 10, QuotaOutcome.Blocked)]
    [InlineData(1, 1, -1, QuotaOutcome.Allowed)]
    [InlineData(0, 1, 0, QuotaOutcome.Blocked)]
    public void Quota_decision_warns_before_it_blocks(long used, long additional, int maximum, QuotaOutcome expected)
    {
        QuotaDecision.Decide(used, additional, maximum).Should().Be(expected);
    }

    private static CommercialEntitlementInput Input(SubscriptionPlan plan, SubscriptionStatus status) => new()
    {
        Plan = plan,
        Status = status,
        UtcNow = DateTime.UtcNow
    };

    private static SubscriptionPlan Plan(string slug)
    {
        var quotas = CommercialFeatureCatalog.Quotas(slug);
        return new SubscriptionPlan
        {
            Slug = slug,
            Name = slug,
            HasAiAssistant = true,
            MaxAgentExecutions = quotas.AgentExecutions,
            MaxAutomationRuns = quotas.AutomationRuns,
            GracePeriodDays = quotas.GraceDays
        };
    }
}

public class AiCostAndRevenueTests
{
    [Fact]
    public void Estimated_cost_uses_the_supplied_price_card()
    {
        var quote = AiUsageCost.Quote(1_000_000, 500_000, 0.15m, 0.60m, null, "card");

        quote.EstimatedUsd.Should().Be(0.45m);
        quote.IsEstimate.Should().BeTrue();
        quote.ActualProviderChargeUsd.Should().BeNull();
    }

    [Fact]
    public void An_actual_provider_charge_is_not_rewritten_as_the_estimate()
    {
        var quote = AiUsageCost.Quote(1000, 1000, 2m, 8m, 1.25m, "invoice");

        quote.EstimatedUsd.Should().NotBe(quote.ActualProviderChargeUsd);
        quote.ActualProviderChargeUsd.Should().Be(1.25m);
        quote.IsEstimate.Should().BeFalse();
    }

    [Fact]
    public void Recurring_revenue_uses_the_locked_contract_and_excludes_trials()
    {
        var plan = new SubscriptionPlan { MonthlyPrice = 49m, AnnualPrice = 470m, BaseCurrency = "USD" };
        var active = new TenantSubscription
        {
            Status = SubscriptionStatus.Active,
            BillingInterval = BillingInterval.Monthly,
            PreserveContract = true,
            SubscriptionPlanId = plan.Id,
            ContractedMonthlyPrice = 19m,
            ContractedAnnualPrice = 182m,
            ContractedCurrency = "USD"
        };
        plan.Id = active.SubscriptionPlanId;

        SubscriptionRevenueMath.MonthlyRecurring(plan, active).Should().Be(19m);
        SubscriptionRevenueMath.CountsAsRecurring(SubscriptionStatus.Trial).Should().BeFalse();
        SubscriptionRevenueMath.CountsAsTrialPipeline(SubscriptionStatus.Trial).Should().BeTrue();
        SubscriptionRevenueMath.CountsAsAtRisk(SubscriptionStatus.PastDue).Should().BeTrue();
    }
}

public class SubscriptionGraceTests
{
    [Fact]
    public async Task Expired_grace_pauses_the_subscription_without_removing_the_plan_or_records()
    {
        var (db, tenantId, _) = TenantDatabase.Open();
        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Professional",
            Slug = "professional",
            BaseCurrency = "USD",
            InventoryAccess = "Full",
            SalesAccess = "Full",
            GracePeriodDays = 0,
            MonthlyPrice = 49
        };
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@company.test",
            PhoneNumber = "1",
            Address = "1",
            City = "1",
            Country = "1",
            PostalCode = "1"
        };
        db.SubscriptionPlans.Add(plan);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Ada Co",
            Slug = "ada",
            BusinessType = "Software",
            Email = "ada@company.test",
            Phone = "1",
            Address = "1",
            SubscriptionPlanId = plan.Id,
            SubscriptionPlan = plan.Name,
            OwnerUserId = "owner"
        });
        db.TenantSubscriptions.Add(new TenantSubscription
        {
            TenantId = tenantId,
            SubscriptionPlanId = plan.Id,
            Status = SubscriptionStatus.PastDue,
            GraceEndsAt = DateTime.UtcNow.AddMinutes(-5),
            Plan = plan
        });
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        await SubscriptionMaintenance.ApplyDueTransitionsAsync(db, DateTime.UtcNow);

        var subscription = await db.TenantSubscriptions.IgnoreQueryFilters().SingleAsync(x => x.TenantId == tenantId);
        subscription.Status.Should().Be(SubscriptionStatus.Expired);
        subscription.SubscriptionPlanId.Should().Be(plan.Id);
        (await db.Customers.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId && !x.IsDeleted)).Should().Be(1);
    }
}
