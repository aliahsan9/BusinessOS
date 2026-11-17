using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Billing.DTOs;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Domain.Billing;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Billing;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class FeatureFlagService : IFeatureFlagService
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantProvider _tenantProvider;

    public FeatureFlagService(IApplicationDbContext context, ITenantProvider tenantProvider)
    {
        _context = context;
        _tenantProvider = tenantProvider;
    }

    public async Task<PlanFeatureFlagsDto> GetFeatureFlagsAsync(CancellationToken cancellationToken = default)
    {
        if (_context is BusinessOSDbContext db && _tenantProvider.HasTenant())
        {
            var access = await CommercialGate.ResolveAsync(db, _tenantProvider.TenantId, cancellationToken);
            if (access is not null)
                return Map(access);
        }

        var plan = await GetCurrentPlanEntityAsync(cancellationToken);
        return MapFlags(plan);
    }

    public async Task EnsureFeatureEnabledAsync(string feature, CancellationToken cancellationToken = default)
    {
        var flags = await GetFeatureFlagsAsync(cancellationToken);
        var enabled = feature.ToLowerInvariant() switch
        {
            FeatureFlags.AiAssistant => flags.HasAiAssistant,
            FeatureFlags.AdvancedAnalytics => flags.HasAdvancedAnalytics,
            FeatureFlags.PdfReports => flags.HasPdfReports,
            FeatureFlags.AdvancedReports => flags.HasAdvancedReports,
            FeatureFlags.PrioritySupport => flags.HasPrioritySupport,
            FeatureFlags.ApiAccess => flags.HasApiAccess,
            FeatureFlags.MultiBranch => flags.HasMultiBranch,
            FeatureFlags.Automations => flags.HasAutomations,
            FeatureFlags.Integrations => flags.HasIntegrations,
            FeatureFlags.ClientPortal => flags.HasClientPortal,
            FeatureFlags.Storefront => flags.HasStorefront,
            FeatureFlags.MultiAgent => flags.HasMultiAgent,
            FeatureFlags.Finance => flags.HasFinance,
            FeatureFlags.AdvancedSecurity => flags.HasAdvancedSecurity,
            _ => false
        };

        if (!enabled)
        {
            throw new ForbiddenException($"This feature requires a higher subscription plan. Upgrade to access {feature.Replace('_', ' ')}.");
        }
    }

    private async Task<SubscriptionPlan> GetCurrentPlanEntityAsync(CancellationToken cancellationToken)
    {
        if (!_tenantProvider.HasTenant())
            throw new UnauthorizedException("Tenant context is required.");

        var tenant = await _context.Tenants
            .AsNoTracking()
            .Include(x => x.Plan)
            .FirstOrDefaultAsync(x => x.Id == _tenantProvider.TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant not found.");

        return tenant.Plan ?? throw new NotFoundException("Subscription plan not found.");
    }

    private static PlanFeatureFlagsDto Map(EffectiveCommercialAccess access) => new()
    {
        HasAiAssistant = access.IsEnabled(CommercialFeatureKeys.AiAssistant),
        HasAdvancedAnalytics = access.IsEnabled(CommercialFeatureKeys.AdvancedAnalytics),
        HasPdfReports = access.IsEnabled(CommercialFeatureKeys.PdfReports),
        HasAdvancedReports = access.IsEnabled(CommercialFeatureKeys.AdvancedReports),
        HasPrioritySupport = access.IsEnabled(CommercialFeatureKeys.PrioritySupport),
        HasApiAccess = access.IsEnabled(CommercialFeatureKeys.ApiAccess),
        HasMultiBranch = access.IsEnabled(CommercialFeatureKeys.MultiBranch),
        HasAutomations = access.IsEnabled(CommercialFeatureKeys.Automations),
        HasIntegrations = access.IsEnabled(CommercialFeatureKeys.Integrations),
        HasClientPortal = access.IsEnabled(CommercialFeatureKeys.ClientPortal),
        HasStorefront = access.IsEnabled(CommercialFeatureKeys.Storefront),
        HasMultiAgent = access.IsEnabled(CommercialFeatureKeys.MultiAgent),
        HasFinance = access.IsEnabled(CommercialFeatureKeys.Finance),
        HasAdvancedSecurity = access.IsEnabled(CommercialFeatureKeys.AdvancedSecurity),
        AccessMode = access.Mode.ToString()
    };

    private static PlanFeatureFlagsDto MapFlags(SubscriptionPlan plan) => new()
    {
        HasAiAssistant = plan.HasAiAssistant,
        HasAdvancedAnalytics = plan.HasAdvancedAnalytics,
        HasPdfReports = plan.HasPdfReports,
        HasAdvancedReports = plan.HasAdvancedReports,
        HasPrioritySupport = plan.HasPrioritySupport,
        HasApiAccess = plan.HasApiAccess,
        HasMultiBranch = plan.HasMultiBranch,
        HasAutomations = CommercialFeatureCatalog.DefaultEnabled(plan.Slug, CommercialFeatureKeys.Automations),
        HasIntegrations = CommercialFeatureCatalog.DefaultEnabled(plan.Slug, CommercialFeatureKeys.Integrations),
        HasClientPortal = CommercialFeatureCatalog.DefaultEnabled(plan.Slug, CommercialFeatureKeys.ClientPortal),
        HasStorefront = CommercialFeatureCatalog.DefaultEnabled(plan.Slug, CommercialFeatureKeys.Storefront),
        HasMultiAgent = CommercialFeatureCatalog.DefaultEnabled(plan.Slug, CommercialFeatureKeys.MultiAgent),
        HasFinance = CommercialFeatureCatalog.DefaultEnabled(plan.Slug, CommercialFeatureKeys.Finance),
        HasAdvancedSecurity = CommercialFeatureCatalog.DefaultEnabled(plan.Slug, CommercialFeatureKeys.AdvancedSecurity)
    };
}
