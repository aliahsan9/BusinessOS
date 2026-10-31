using System.Text.Json;
using BusinessOS.Application.Common.Models;
using BusinessOS.Domain.Billing;
using BusinessOS.Domain.Entities;

namespace BusinessOS.Application.Features.Billing.Services;

public static class EntitlementResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static TenantLimits Resolve(SubscriptionPlan plan, TenantSubscription? subscription)
    {
        var snapshot = ReadSnapshot(subscription);
        if (snapshot is not null)
        {
            var ai = subscription?.AiRequestAllowance ?? snapshot.MaxAiRequests;
            return new TenantLimits(
                snapshot.MaxUsers,
                snapshot.MaxCustomers,
                snapshot.MaxProjects,
                snapshot.MaxTasks,
                snapshot.MaxStorageMb,
                ai,
                snapshot.MaxProducts,
                snapshot.HasApiAccess,
                snapshot.HasMultiBranch);
        }

        return new TenantLimits(
            plan.MaxUsers,
            plan.MaxCustomers,
            plan.MaxProjects,
            plan.MaxTasks,
            plan.MaxStorageMb,
            subscription?.AiRequestAllowance ?? plan.MaxAiRequests,
            plan.MaxProducts,
            plan.HasApiAccess,
            plan.HasMultiBranch);
    }

    public static (decimal Monthly, decimal Annual, string Currency) ResolvePrice(
        SubscriptionPlan plan,
        TenantSubscription? subscription)
    {
        if (subscription?.PreserveContract == true &&
            subscription.SubscriptionPlanId == plan.Id &&
            subscription.ContractedMonthlyPrice is decimal monthly)
        {
            return (
                monthly,
                subscription.ContractedAnnualPrice ?? plan.AnnualPrice,
                string.IsNullOrWhiteSpace(subscription.ContractedCurrency)
                    ? plan.BaseCurrency
                    : subscription.ContractedCurrency!);
        }

        return (plan.MonthlyPrice, plan.AnnualPrice, string.IsNullOrWhiteSpace(plan.BaseCurrency) ? "USD" : plan.BaseCurrency);
    }

    public static ContractedEntitlement? ReadSnapshot(TenantSubscription? subscription)
    {
        if (subscription?.PreserveContract != true || string.IsNullOrWhiteSpace(subscription.ContractedLimitsJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ContractedEntitlement>(subscription.ContractedLimitsJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string Serialize(ContractedEntitlement entitlement) =>
        JsonSerializer.Serialize(entitlement, JsonOptions);
}
