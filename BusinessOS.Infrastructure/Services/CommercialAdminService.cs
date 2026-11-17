using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Billing.DTOs;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Domain.Billing;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class CommercialAdminService : ICommercialAdminService
{
    private readonly IDbContextFactory<BusinessOSDbContext> _factory;
    private readonly ISuperAdminContext _admin;
    private readonly ICurrentUserService _user;
    private readonly IHttpContextAccessor _http;

    public CommercialAdminService(
        IDbContextFactory<BusinessOSDbContext> factory,
        ISuperAdminContext admin,
        ICurrentUserService user,
        IHttpContextAccessor http)
    {
        _factory = factory;
        _admin = admin;
        _user = user;
        _http = http;
    }

    public async Task<SubscriptionPlanDto> UpdatePlanAsync(
        Guid planId,
        CommercialPlanUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        ValidatePrices(request);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var plan = await db.SubscriptionPlans.FirstOrDefaultAsync(x => x.Id == planId, cancellationToken)
            ?? throw new NotFoundException("Plan not found.");

        plan.MonthlyPrice = request.MonthlyPrice;
        plan.AnnualPrice = request.AnnualPrice;
        plan.BaseCurrency = request.Currency.Trim().ToUpperInvariant();
        plan.MaxUsers = request.MaxUsers;
        plan.MaxCustomers = request.MaxCustomers;
        plan.MaxProducts = request.MaxProducts;
        plan.MaxProjects = request.MaxProjects;
        plan.MaxTasks = request.MaxTasks;
        plan.MaxStorageMb = request.MaxStorageMb;
        plan.MaxAiRequests = request.MaxAiRequests;
        plan.MaxAgentExecutions = request.MaxAgentExecutions;
        plan.MaxAutomationRuns = request.MaxAutomationRuns;
        plan.GracePeriodDays = request.GracePeriodDays;
        plan.TrialDays = request.TrialDays;

        var regional = await db.RegionalPrices
            .Where(x => x.SubscriptionPlanId == plan.Id && x.CountryCode == "US")
            .ToListAsync(cancellationToken);
        foreach (var row in regional)
        {
            row.MonthlyAmount = request.MonthlyPrice;
            row.AnnualAmount = request.AnnualPrice;
            row.CurrencyCode = plan.BaseCurrency;
        }

        await AuditAsync(db, "PlanPricingUpdated", "SubscriptionPlan", plan.Id.ToString(), null, plan.Slug, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return MapPlan(plan);
    }

    public async Task UpsertPlanFeatureAsync(
        Guid planId,
        string featureKey,
        FeatureGrantRequest request,
        CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        var key = RequireKey(featureKey);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var plan = await db.SubscriptionPlans.FirstOrDefaultAsync(x => x.Id == planId, cancellationToken)
            ?? throw new NotFoundException("Plan not found.");
        ApplyColumn(plan, key, request.Enabled);
        var from = request.EffectiveFrom ?? DateTime.UtcNow;
        db.PlanFeatureGrants.Add(new PlanFeatureGrant
        {
            SubscriptionPlanId = plan.Id,
            FeatureKey = key,
            Enabled = request.Enabled,
            Limit = request.Limit,
            EffectiveFrom = from,
            EffectiveTo = request.EffectiveTo,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await AuditAsync(db, "PlanFeatureUpdated", "PlanFeatureGrant", key, null, plan.Slug, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertCompanyFeatureAsync(
        Guid companyId,
        string featureKey,
        FeatureGrantRequest request,
        CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        var key = RequireKey(featureKey);
        if (request.Reason is not null && request.Reason.Length > 500)
            throw new BadRequestException("The override reason must be 500 characters or fewer.");

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var exists = await db.Tenants.IgnoreQueryFilters().AnyAsync(x => x.Id == companyId && !x.IsDeleted, cancellationToken);
        if (!exists)
            throw new NotFoundException("Company not found.");

        db.CompanyFeatureOverrides.Add(new CompanyFeatureOverride
        {
            TenantId = companyId,
            FeatureKey = key,
            Enabled = request.Enabled,
            Limit = request.Limit,
            EffectiveFrom = request.EffectiveFrom ?? DateTime.UtcNow,
            EffectiveTo = request.EffectiveTo,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            CreatedAt = DateTime.UtcNow
        });
        await AuditAsync(db, "CompanyFeatureOverride", "CompanyFeatureOverride", key, companyId, request.Reason, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CurrentPlanDto> GrantTrialAsync(
        Guid companyId,
        GrantTrialRequest request,
        CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        if (request.Days is < 1 or > 90)
            throw new BadRequestException("A trial must last between 1 and 90 days.");

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var tenant = await RequireTenantAsync(db, companyId, cancellationToken);
        var plan = await db.SubscriptionPlans.FirstOrDefaultAsync(x => x.Id == request.PlanId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Plan not found.");
        if (plan.MonthlyPrice <= 0)
            throw new BadRequestException("The free plan does not use a trial.");

        var subscription = await RequireSubscriptionAsync(db, tenant, cancellationToken);
        var now = DateTime.UtcNow;
        subscription.SubscriptionPlanId = plan.Id;
        subscription.Status = SubscriptionStatus.Trial;
        subscription.TrialEndDate = now.AddDays(request.Days);
        subscription.TrialConsumed = false;
        subscription.CancelAtPeriodEnd = false;
        subscription.GraceEndsAt = null;
        subscription.EndDate = null;
        subscription.UpdatedAt = now;
        tenant.SubscriptionPlanId = plan.Id;
        tenant.SubscriptionPlan = plan.Name;
        tenant.Plan = plan;
        tenant.Status = TenantStatus.Active;
        db.SubscriptionHistories.Add(new SubscriptionHistory
        {
            TenantId = tenant.Id,
            SubscriptionPlanId = plan.Id,
            Action = "TrialGranted",
            ToStatus = SubscriptionStatusNames.Trialing,
            Notes = $"Platform administrator granted a {request.Days}-day trial. No charge was created.",
            CreatedAt = now
        });
        await AuditAsync(db, "TrialGranted", "TenantSubscription", plan.Slug, tenant.Id, null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return MapCurrent(tenant, subscription);
    }

    public async Task<CurrentPlanDto> SuspendSubscriptionAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var tenant = await RequireTenantAsync(db, companyId, cancellationToken);
        var subscription = await RequireSubscriptionAsync(db, tenant, cancellationToken);
        if (subscription.Status == SubscriptionStatus.Suspended)
            return MapCurrent(tenant, subscription);

        var now = DateTime.UtcNow;
        var from = SubscriptionStatusNames.ToApi(subscription.Status);
        subscription.Status = SubscriptionStatus.Suspended;
        subscription.AutoRenewEnabled = false;
        subscription.UpdatedAt = now;
        db.SubscriptionHistories.Add(new SubscriptionHistory
        {
            TenantId = tenant.Id,
            SubscriptionPlanId = subscription.SubscriptionPlanId,
            Action = "SubscriptionSuspended",
            FromStatus = from,
            ToStatus = SubscriptionStatusNames.Suspended,
            Notes = "Suspended by a platform administrator. Company data was not deleted.",
            CreatedAt = now
        });
        await AuditAsync(db, "SubscriptionSuspended", "TenantSubscription", subscription.Id.ToString(), tenant.Id, null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return MapCurrent(tenant, subscription);
    }

    public async Task<CurrentPlanDto> ReactivateSubscriptionAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var tenant = await RequireTenantAsync(db, companyId, cancellationToken);
        var subscription = await RequireSubscriptionAsync(db, tenant, cancellationToken);
        var now = DateTime.UtcNow;
        var from = SubscriptionStatusNames.ToApi(subscription.Status);
        var free = tenant.Plan.MonthlyPrice <= 0;
        subscription.Status = free ? SubscriptionStatus.Free : SubscriptionStatus.Active;
        subscription.EndDate = null;
        subscription.GraceEndsAt = null;
        subscription.UpdatedAt = now;
        tenant.Status = TenantStatus.Active;
        tenant.IsActive = true;
        db.SubscriptionHistories.Add(new SubscriptionHistory
        {
            TenantId = tenant.Id,
            SubscriptionPlanId = subscription.SubscriptionPlanId,
            Action = "SubscriptionReactivated",
            FromStatus = from,
            ToStatus = SubscriptionStatusNames.ToApi(subscription.Status),
            Notes = "Reactivated by a platform administrator. No new charge was created.",
            CreatedAt = now
        });
        await AuditAsync(db, "SubscriptionReactivated", "TenantSubscription", subscription.Id.ToString(), tenant.Id, null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return MapCurrent(tenant, subscription);
    }

    public async Task<IReadOnlyList<FailedPaymentDto>> ListFailedPaymentsAsync(CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.BillingTransactions.IgnoreQueryFilters().AsNoTracking()
            .Where(x => !x.IsDeleted && x.Status == BillingTransactionStatus.Failed)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .Select(x => new FailedPaymentDto
            {
                Id = x.Id,
                CompanyId = x.TenantId,
                Amount = x.Amount,
                Currency = x.Currency,
                Provider = x.Provider.ToString(),
                Description = x.Description,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ModelPriceDto>> ListModelPricesAsync(CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.AiModelPrices.AsNoTracking()
            .OrderByDescending(x => x.EffectiveFrom)
            .Select(x => new ModelPriceDto
            {
                Id = x.Id,
                Provider = x.Provider,
                Model = x.Model,
                UsageCategory = x.UsageCategory,
                InputUsdPerMillion = x.InputUsdPerMillion,
                OutputUsdPerMillion = x.OutputUsdPerMillion,
                PricingVersion = x.PricingVersion,
                EffectiveFrom = x.EffectiveFrom,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ModelPriceDto> UpsertModelPriceAsync(ModelPriceRequest request, CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        if (string.IsNullOrWhiteSpace(request.Model))
            throw new BadRequestException("A model name is required.");
        if (request.InputUsdPerMillion < 0 || request.OutputUsdPerMillion < 0)
            throw new BadRequestException("Model prices cannot be negative.");

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var model = request.Model.Trim();
        var current = await db.AiModelPrices
            .Where(x => x.IsActive && x.Model == model)
            .ToListAsync(cancellationToken);
        foreach (var row in current)
        {
            row.IsActive = false;
            row.EffectiveTo = now;
        }

        var created = new AiModelPrice
        {
            Provider = string.IsNullOrWhiteSpace(request.Provider) ? "OpenAI" : request.Provider.Trim(),
            Model = model,
            UsageCategory = string.IsNullOrWhiteSpace(request.UsageCategory) ? "chat" : request.UsageCategory.Trim(),
            InputUsdPerMillion = request.InputUsdPerMillion,
            OutputUsdPerMillion = request.OutputUsdPerMillion,
            PricingVersion = now.ToString("yyyy-MM-dd"),
            EffectiveFrom = now,
            IsActive = true,
            CreatedAt = now
        };
        db.AiModelPrices.Add(created);
        await AuditAsync(db, "ModelPriceUpdated", "AiModelPrice", model, null, null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new ModelPriceDto
        {
            Id = created.Id,
            Provider = created.Provider,
            Model = created.Model,
            UsageCategory = created.UsageCategory,
            InputUsdPerMillion = created.InputUsdPerMillion,
            OutputUsdPerMillion = created.OutputUsdPerMillion,
            PricingVersion = created.PricingVersion,
            EffectiveFrom = created.EffectiveFrom,
            IsActive = true
        };
    }

    private void RequireAdmin()
    {
        if (!_admin.IsSuperAdmin)
            throw new ForbiddenException("Platform administrator access is required.");
    }

    private static void ValidatePrices(CommercialPlanUpdateRequest request)
    {
        if (!CurrencyMath.TryNormalize(request.Currency, out var currency))
            throw new BadRequestException("Currency is not supported.");
        request.Currency = currency;
        if (request.MonthlyPrice < 0 || request.AnnualPrice < 0)
            throw new BadRequestException("Prices cannot be negative.");
        if (request.GracePeriodDays is < 0 or > 60)
            throw new BadRequestException("Grace period must be between 0 and 60 days.");
        if (request.TrialDays is < 0 or > 365)
            throw new BadRequestException("Trial length must be between 0 and 365 days.");
        foreach (var limit in new[] { request.MaxUsers, request.MaxCustomers, request.MaxProducts, request.MaxProjects, request.MaxTasks, request.MaxAiRequests, request.MaxAgentExecutions, request.MaxAutomationRuns })
        {
            if (limit < SubscriptionPlan.Unlimited)
                throw new BadRequestException("A limit must be -1 for unlimited or a non-negative allowance.");
        }
        if (request.MaxStorageMb < 0)
            throw new BadRequestException("Storage allowance cannot be negative.");
    }

    private static string RequireKey(string featureKey)
    {
        var key = featureKey.Trim().ToLowerInvariant();
        if (!CommercialFeatureKeys.IsKnown(key) && key is not ("agent_executions" or "automation_runs"))
            throw new BadRequestException("Unknown feature key.");

        return key;
    }

    private static void ApplyColumn(SubscriptionPlan plan, string key, bool enabled)
    {
        switch (key)
        {
            case CommercialFeatureKeys.AiAssistant: plan.HasAiAssistant = enabled; break;
            case CommercialFeatureKeys.AdvancedAnalytics: plan.HasAdvancedAnalytics = enabled; break;
            case CommercialFeatureKeys.PdfReports: plan.HasPdfReports = enabled; break;
            case CommercialFeatureKeys.AdvancedReports: plan.HasAdvancedReports = enabled; break;
            case CommercialFeatureKeys.PrioritySupport: plan.HasPrioritySupport = enabled; break;
            case CommercialFeatureKeys.ApiAccess: plan.HasApiAccess = enabled; break;
            case CommercialFeatureKeys.MultiBranch: plan.HasMultiBranch = enabled; break;
        }
    }

    private async Task<Tenant> RequireTenantAsync(BusinessOSDbContext db, Guid companyId, CancellationToken cancellationToken)
    {
        return await db.Tenants.IgnoreQueryFilters()
            .Include(x => x.Plan)
            .FirstOrDefaultAsync(x => x.Id == companyId && !x.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Company not found.");
    }

    private static async Task<TenantSubscription> RequireSubscriptionAsync(
        BusinessOSDbContext db,
        Tenant tenant,
        CancellationToken cancellationToken)
    {
        var subscription = await db.TenantSubscriptions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && !x.IsDeleted, cancellationToken);
        if (subscription is not null)
            return subscription;

        subscription = new TenantSubscription
        {
            TenantId = tenant.Id,
            SubscriptionPlanId = tenant.SubscriptionPlanId,
            Status = SubscriptionStatus.Free,
            StartDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        db.TenantSubscriptions.Add(subscription);
        return subscription;
    }

    private async Task AuditAsync(
        BusinessOSDbContext db,
        string action,
        string entityType,
        string entityId,
        Guid? companyId,
        string? metadata,
        CancellationToken cancellationToken)
    {
        db.PlatformAuditLogs.Add(new PlatformAuditLog
        {
            ActorUserId = _user.UserId ?? "platform",
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            SubjectTenantId = companyId,
            MetadataJson = metadata,
            IpAddress = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            CreatedAt = DateTime.UtcNow
        });
        await Task.CompletedTask;
    }

    private static SubscriptionPlanDto MapPlan(SubscriptionPlan plan) => new()
    {
        Id = plan.Id,
        Name = plan.Name,
        Slug = plan.Slug,
        Description = plan.Description,
        MonthlyPrice = plan.MonthlyPrice,
        AnnualPrice = plan.AnnualPrice,
        MaxUsers = plan.MaxUsers,
        MaxCustomers = plan.MaxCustomers,
        MaxProducts = plan.MaxProducts,
        MaxProjects = plan.MaxProjects,
        MaxTasks = plan.MaxTasks,
        MaxStorageMb = plan.MaxStorageMb,
        MaxAiRequests = plan.MaxAiRequests,
        Currency = plan.BaseCurrency,
        BaseCurrency = plan.BaseCurrency,
        TrialDays = plan.TrialDays,
        Features = SubscriptionCatalog.BuildFeatures(plan),
        SortOrder = plan.SortOrder
    };

    private static CurrentPlanDto MapCurrent(Tenant tenant, TenantSubscription subscription)
    {
        var access = CommercialEntitlementEvaluator.Evaluate(new CommercialEntitlementInput
        {
            Plan = tenant.Plan,
            Status = subscription.Status,
            CancelAtPeriodEnd = subscription.CancelAtPeriodEnd,
            CurrentPeriodEnd = subscription.CurrentPeriodEnd,
            GraceEndsAt = subscription.GraceEndsAt,
            PreserveContract = subscription.PreserveContract,
            Snapshot = EntitlementResolver.ReadSnapshot(subscription),
            UtcNow = DateTime.UtcNow
        });
        return new CurrentPlanDto
        {
            PlanId = tenant.Plan.Id,
            PlanName = tenant.Plan.Name,
            PlanSlug = tenant.Plan.Slug,
            Status = SubscriptionStatusNames.ToApi(subscription.Status),
            BillingInterval = subscription.BillingInterval.ToString(),
            StartDate = subscription.StartDate,
            TrialEndDate = subscription.TrialEndDate,
            CurrentPeriodEnd = subscription.CurrentPeriodEnd,
            GraceEndsAt = subscription.GraceEndsAt,
            AccessMode = access.Mode.ToString()
        };
    }
}
