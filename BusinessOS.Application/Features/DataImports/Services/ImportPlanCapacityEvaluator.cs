using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Application.Features.DataImports.Models;
using BusinessOS.Domain.Billing;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Features.DataImports.Services;

public static class ImportPlanCapacityEvaluator
{
    public static async Task<ImportPlanCapacityDto?> EvaluateAsync(
        string entityType,
        int createCount,
        ISubscriptionEntitlementService entitlements,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!DataImportEntityTypes.IsQuotaCountedCreate(entityType))
            return null;

        var limits = await entitlements.GetLimitsAsync(tenantId, cancellationToken);
        var usage = await entitlements.GetUsageAsync(tenantId, cancellationToken);

        var (resource, current, maximum) = entityType switch
        {
            DataImportEntityTypes.Products => ("Products", (long)usage.Products, (long)limits.MaxProducts),
            DataImportEntityTypes.Customers => ("Customers", (long)usage.Customers, (long)limits.MaxCustomers),
            _ => (DataImportEntityTypes.QuotaResourceName(entityType) ?? "Records", 0L, -1L)
        };

        var unlimited = PlanLimitHelper.IsUnlimited((int)maximum);
        var remaining = unlimited ? long.MaxValue : Math.Max(0, maximum - current);
        var exceeding = unlimited ? 0 : Math.Max(0, createCount - (int)Math.Min(remaining, int.MaxValue));
        var within = unlimited || createCount <= remaining;

        return new ImportPlanCapacityDto(
            resource,
            current,
            unlimited ? -1 : maximum,
            unlimited ? -1 : remaining,
            createCount,
            createCount,
            exceeding,
            within,
            within
                ? null
                : $"Your plan allows {FormatMax(maximum)} {resource.ToLowerInvariant()}. " +
                  $"This import would create {createCount} new records but only {remaining} slots remain. Upgrade to continue.");
    }

    public static ImportPreflightResponse ToPreflight(
        string entityType,
        ImportPlanCapacityDto? capacity,
        int totalRows,
        string planName,
        SubscriptionStatus status)
    {
        if (capacity is null)
        {
            return new ImportPreflightResponse(
                entityType,
                null,
                0,
                -1,
                -1,
                0,
                totalRows,
                0,
                true,
                status.ToString(),
                planName,
                "This import type is not limited by seat quotas.",
                null);
        }

        return new ImportPreflightResponse(
            entityType,
            capacity.Resource,
            capacity.CurrentUsage,
            capacity.Maximum,
            capacity.Remaining,
            capacity.CreateCount,
            totalRows,
            capacity.RowsExceedingLimit,
            capacity.WithinLimit,
            status.ToString(),
            planName,
            capacity.WithinLimit
                ? $"You can import {capacity.CreateCount} new {capacity.Resource.ToLowerInvariant()} within your plan."
                : capacity.UpgradeHint,
            capacity.WithinLimit ? null : "Open Subscription to upgrade, then retry this import.");
    }

    private static string FormatMax(long maximum) =>
        maximum < 0 ? "unlimited" : maximum.ToString();
}
