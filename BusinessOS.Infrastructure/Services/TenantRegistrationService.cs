using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Tenant.Services;
using BusinessOS.Domain.Companies;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class TenantRegistrationService : ITenantRegistrationService
{
    private readonly IDbContextFactory<BusinessOSDbContext> _dbContextFactory;
    private readonly ITenantAuditService _auditService;

    public TenantRegistrationService(
        IDbContextFactory<BusinessOSDbContext> dbContextFactory,
        ITenantAuditService auditService)
    {
        _dbContextFactory = dbContextFactory;
        _auditService = auditService;
    }

    public async Task<Guid> CreateTenantAsync(
        CreateTenantOptions options,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var freePlan = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Slug == "free", cancellationToken);

        var planId = freePlan?.Id ?? SubscriptionPlanSeeder.FreePlanId;
        var planName = freePlan?.Name ?? "Free";

        var baseSlug = SlugHelper.GenerateSlug(options.BusinessName);
        var slug = SlugHelper.EnsureUniqueSlug(
            baseSlug,
            candidate => dbContext.Tenants.IgnoreQueryFilters().Any(x => x.Slug == candidate && !x.IsDeleted));

        var modules = await ModulesForIndustryAsync(dbContext, options.Industry, cancellationToken);
        var templateName = await TemplateNameAsync(dbContext, options.Industry, cancellationToken);
        var tenant = new Tenant
        {
            Id = options.TenantId,
            Name = options.BusinessName.Trim(),
            Slug = slug,
            BusinessType = string.IsNullOrWhiteSpace(templateName) ? options.Industry.Trim() : templateName,
            Email = options.Email.Trim(),
            Phone = string.Empty,
            Address = string.Empty,
            Timezone = string.IsNullOrWhiteSpace(options.Timezone) ? "UTC" : options.Timezone.Trim(),
            Currency = string.IsNullOrWhiteSpace(options.Currency) ? "USD" : options.Currency.Trim(),
            Status = TenantStatus.Active,
            SubscriptionPlanId = planId,
            SubscriptionPlan = planName,
            IsActive = true,
            OwnerUserId = options.OwnerUserId,
            Country = string.Empty,
            EnabledModulesJson = modules
        };

        dbContext.Tenants.Add(tenant);

        var settings = new TenantSettings
        {
            TenantId = options.TenantId,
            Currency = tenant.Currency,
            Timezone = tenant.Timezone,
            Language = "en",
            Theme = "light",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.TenantSettings.Add(settings);

        var subscription = new TenantSubscription
        {
            TenantId = options.TenantId,
            SubscriptionPlanId = planId,
            Status = SubscriptionStatus.Trial,
            StartDate = DateTime.UtcNow,
            TrialEndDate = DateTime.UtcNow.AddDays(14)
        };
        dbContext.TenantSubscriptions.Add(subscription);

        var usage = new TenantUsage
        {
            TenantId = options.TenantId,
            UserCount = 0,
            CustomerCount = 0,
            ProjectCount = 0,
            LastCalculatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.TenantUsages.Add(usage);

        await dbContext.SaveChangesAsync(cancellationToken);

        await ExpenseCategorySeeder.SeedForTenantAsync(dbContext, options.TenantId, cancellationToken: cancellationToken);

        await _auditService.LogAsync(
            options.TenantId,
            "TenantCreated",
            "Tenant",
            null,
            RbacAuditService.Serialize(new { tenant.Name, tenant.Slug, Plan = planName }),
            options.OwnerUserId,
            cancellationToken: cancellationToken);

        return tenant.Id;
    }

    private static async Task<string?> ModulesForIndustryAsync(
        BusinessOSDbContext dbContext,
        string industry,
        CancellationToken cancellationToken)
    {
        var key = industry.Trim();
        var stored = await dbContext.BusinessTypeTemplates.AsNoTracking()
            .Where(x => x.IsActive && (x.Key == key || x.Name == key))
            .Select(x => x.ModulesJson)
            .FirstOrDefaultAsync(cancellationToken);
        return stored ?? BusinessTypeCatalog.Defaults
            .FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase) || x.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
            ?.ModulesJson;
    }

    private static async Task<string?> TemplateNameAsync(
        BusinessOSDbContext dbContext,
        string industry,
        CancellationToken cancellationToken)
    {
        var key = industry.Trim();
        var stored = await dbContext.BusinessTypeTemplates.AsNoTracking()
            .Where(x => x.IsActive && (x.Key == key || x.Name == key))
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);
        return stored ?? BusinessTypeCatalog.Defaults
            .FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase) || x.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
            ?.Name;
    }
}
