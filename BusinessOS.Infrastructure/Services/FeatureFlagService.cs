using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Billing.DTOs;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Domain.Entities;
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
            _ => false
        };

        if (!enabled)
        {
            throw new BadRequestException($"This feature requires a higher subscription plan. Upgrade to access {feature.Replace('_', ' ')}.");
        }
    }

    private async Task<SubscriptionPlan> GetCurrentPlanEntityAsync(CancellationToken cancellationToken)
    {
        if (!_tenantProvider.HasTenant())
        {
            throw new UnauthorizedException("Tenant context is required.");
        }

        var tenant = await _context.Tenants
            .AsNoTracking()
            .Include(x => x.Plan)
            .FirstOrDefaultAsync(x => x.Id == _tenantProvider.TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant not found.");

        return tenant.Plan ?? throw new NotFoundException("Subscription plan not found.");
    }

    private static PlanFeatureFlagsDto MapFlags(SubscriptionPlan plan) => new()
    {
        HasAiAssistant = plan.HasAiAssistant,
        HasAdvancedAnalytics = plan.HasAdvancedAnalytics,
        HasPdfReports = plan.HasPdfReports,
        HasAdvancedReports = plan.HasAdvancedReports,
        HasPrioritySupport = plan.HasPrioritySupport
    };
}
