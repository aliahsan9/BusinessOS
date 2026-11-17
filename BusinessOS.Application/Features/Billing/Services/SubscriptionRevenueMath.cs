using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Features.Billing.Services;

/// <summary>
/// Recurring revenue uses the price the company is actually contracted to pay.
/// Trials and failed payments are reported separately. Nothing here is a cash collection.
/// </summary>
public static class SubscriptionRevenueMath
{
    public static decimal MonthlyRecurring(SubscriptionPlan plan, TenantSubscription subscription)
    {
        var (monthly, annual, _) = EntitlementResolver.ResolvePrice(plan, subscription);
        var amount = subscription.BillingInterval == BillingInterval.Yearly ? annual / 12m : monthly;
        return decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    public static bool CountsAsRecurring(SubscriptionStatus status) =>
        status == SubscriptionStatus.Active;

    public static bool CountsAsTrialPipeline(SubscriptionStatus status) =>
        status == SubscriptionStatus.Trial;

    public static bool CountsAsAtRisk(SubscriptionStatus status) =>
        status == SubscriptionStatus.PastDue;
}
