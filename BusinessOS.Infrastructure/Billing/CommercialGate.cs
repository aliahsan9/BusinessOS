using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Domain.Billing;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Billing;

public static class CommercialGate
{
    public static async Task<EffectiveCommercialAccess?> ResolveAsync(
        BusinessOSDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return null;

        var now = DateTime.UtcNow;
        var tenant = await db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .Include(x => x.Plan)
            .FirstOrDefaultAsync(x => x.Id == tenantId && !x.IsDeleted, cancellationToken);
        if (tenant?.Plan is null)
            return null;

        var subscription = await db.TenantSubscriptions.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted, cancellationToken);
        var grants = await db.PlanFeatureGrants.AsNoTracking()
            .Where(x => x.SubscriptionPlanId == tenant.Plan.Id && x.IsActive)
            .ToListAsync(cancellationToken);
        var overrides = await db.CompanyFeatureOverrides.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        return CommercialEntitlementEvaluator.Evaluate(new CommercialEntitlementInput
        {
            Plan = tenant.Plan,
            Status = subscription?.Status ?? Domain.Enums.SubscriptionStatus.Free,
            CancelAtPeriodEnd = subscription?.CancelAtPeriodEnd ?? false,
            CurrentPeriodEnd = subscription?.CurrentPeriodEnd,
            GraceEndsAt = subscription?.GraceEndsAt,
            PreserveContract = subscription?.PreserveContract ?? false,
            Snapshot = EntitlementResolver.ReadSnapshot(subscription),
            UtcNow = now,
            PlanGrants = grants.Select(Map).ToList(),
            CompanyOverrides = overrides.Select(Map).ToList()
        });
    }

    public static async Task EnsureFeatureAsync(
        BusinessOSDbContext db,
        Guid tenantId,
        string featureKey,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAsync(db, tenantId, cancellationToken);
        if (access is null)
            return;
        if (!access.IsEnabled(featureKey))
            throw new ForbiddenException("This feature is not included in the current subscription.");
    }

    public static async Task EnsureQuotaAsync(
        BusinessOSDbContext db,
        Guid tenantId,
        string resource,
        int used,
        int maximum,
        CancellationToken cancellationToken = default)
    {
        var outcome = QuotaDecision.Decide(used, 1, maximum);
        if (outcome == QuotaOutcome.Warning)
            await RecordAlertAsync(db, tenantId, resource, "Warning", cancellationToken);
        if (outcome != QuotaOutcome.Blocked)
            return;

        await RecordAlertAsync(db, tenantId, resource, "Exhausted", cancellationToken);
        throw new PlanLimitExceededException(resource, used, maximum);
    }

    public static async Task<decimal> EstimateModelCostAsync(
        BusinessOSDbContext db,
        string? model,
        int inputTokens,
        int outputTokens,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var name = (model ?? string.Empty).Trim().ToLowerInvariant();
        AiModelPrice? row = null;
        if (name.Length > 0)
        {
            var prices = await db.AiModelPrices.AsNoTracking()
                .Where(x => x.IsActive && x.EffectiveFrom <= now && (x.EffectiveTo == null || x.EffectiveTo > now))
                .ToListAsync(cancellationToken);
            row = prices.FirstOrDefault(x => string.Equals(x.Model, model, StringComparison.OrdinalIgnoreCase));
        }

        if (row is null)
            return NalvaModelCatalogEstimate(model, inputTokens, outputTokens);

        return AiUsageCost.Estimate(inputTokens, outputTokens, row.InputUsdPerMillion, row.OutputUsdPerMillion);
    }

    private static decimal NalvaModelCatalogEstimate(string? model, int inputTokens, int outputTokens) =>
        Application.Features.Nalva.NalvaModelCatalog.EstimateUsd(model, inputTokens, outputTokens);

    private static async Task RecordAlertAsync(
        BusinessOSDbContext db,
        Guid tenantId,
        string resource,
        string level,
        CancellationToken cancellationToken)
    {
        var period = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var exists = await db.UsageAlerts.IgnoreQueryFilters().AnyAsync(
            x => x.TenantId == tenantId && x.Resource == resource && x.Level == level && x.PeriodStart == period && !x.IsDeleted,
            cancellationToken);
        if (exists)
            return;

        db.UsageAlerts.Add(new UsageAlert
        {
            TenantId = tenantId,
            Resource = resource,
            Level = level,
            PeriodStart = period,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static FeatureGrantState Map(PlanFeatureGrant grant) =>
        new(grant.FeatureKey, grant.Enabled, grant.Limit, grant.EffectiveFrom, grant.EffectiveTo);

    private static FeatureGrantState Map(CompanyFeatureOverride grant) =>
        new(grant.FeatureKey, grant.Enabled, grant.Limit, grant.EffectiveFrom, grant.EffectiveTo);
}
