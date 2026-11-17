using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Platform;
using BusinessOS.Application.Features.VectorSearch.Options;
using BusinessOS.Application.Features.VectorSearch.Services;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.AI;
using BusinessOS.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.Services;

public sealed class PlatformAdminService : IPlatformAdminService
{
    private readonly IDbContextFactory<BusinessOSDbContext> _factory;
    private readonly ISuperAdminContext _admin;
    private readonly ICurrentUserService _currentUser;
    private readonly IVectorStore _vectors;
    private readonly IHttpContextAccessor _http;
    private readonly AiOptions _ai;
    private readonly QdrantOptions _qdrant;

    public PlatformAdminService(
        IDbContextFactory<BusinessOSDbContext> factory,
        ISuperAdminContext admin,
        ICurrentUserService currentUser,
        IVectorStore vectors,
        IHttpContextAccessor http,
        IOptions<AiOptions> ai,
        IOptions<QdrantOptions> qdrant)
    {
        _factory = factory;
        _admin = admin;
        _currentUser = currentUser;
        _vectors = vectors;
        _http = http;
        _ai = ai.Value;
        _qdrant = qdrant.Value;
    }

    public PlatformSessionDto GetSession()
    {
        RequireAdmin();
        return new PlatformSessionDto { IsSuperAdmin = true };
    }

    public async Task<PagedResult<CompanySummaryDto>> ListCompaniesAsync(
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize == 0 ? 25 : pageSize, 1, 100);

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var query = db.Tenants.IgnoreQueryFilters().AsNoTracking().Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Name.Contains(term) || x.Email.Contains(term) || x.BusinessType.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TenantStatus>(status, true, out var parsed))
            query = query.Where(x => x.Status == parsed);

        var total = await query.CountAsync(cancellationToken);
        var tenants = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new { x.Id, x.Name, x.BusinessType, x.Status, x.IsActive, x.Email, x.Country, x.SubscriptionPlan, x.CreatedAt })
            .ToListAsync(cancellationToken);

        var ids = tenants.Select(x => x.Id).ToList();
        var members = await db.CompanyMemberships.IgnoreQueryFilters().AsNoTracking()
            .Where(x => ids.Contains(x.TenantId) && x.IsActive && !x.IsDeleted)
            .GroupBy(x => x.TenantId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var memberMap = members.ToDictionary(x => x.Key, x => x.Count);

        var subscriptions = await db.TenantSubscriptions.IgnoreQueryFilters().AsNoTracking()
            .Where(x => ids.Contains(x.TenantId) && !x.IsDeleted)
            .Select(x => new { x.TenantId, x.Status })
            .ToListAsync(cancellationToken);
        var subscriptionMap = subscriptions.GroupBy(x => x.TenantId).ToDictionary(g => g.Key, g => g.First().Status.ToString());

        return new PagedResult<CompanySummaryDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = tenants.Select(x => new CompanySummaryDto
            {
                Id = x.Id,
                Name = x.Name,
                BusinessType = x.BusinessType,
                Status = x.Status.ToString(),
                IsActive = x.IsActive,
                Email = x.Email,
                Country = x.Country,
                SubscriptionPlan = x.SubscriptionPlan,
                SubscriptionStatus = subscriptionMap.GetValueOrDefault(x.Id),
                CreatedAt = x.CreatedAt,
                MemberCount = memberMap.GetValueOrDefault(x.Id)
            }).ToList()
        };
    }

    public async Task<CompanyProfileDto> GetCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await MapCompanyAsync(db, companyId, cancellationToken);
    }

    public async Task<CompanyProfileDto> SetCompanyStatusAsync(
        Guid companyId,
        string status,
        CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        if (!Enum.TryParse<TenantStatus>(status, true, out var parsed) ||
            parsed is not (TenantStatus.Active or TenantStatus.Suspended))
        {
            throw new BadRequestException("Status must be Active or Suspended.");
        }

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var tenant = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == companyId && !x.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Company was not found.");

        tenant.Status = parsed;
        tenant.IsActive = parsed == TenantStatus.Active;
        tenant.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(db, parsed == TenantStatus.Suspended ? "CompanySuspended" : "CompanyReactivated", "Tenant", tenant.Id.ToString(), tenant.Id, null, cancellationToken);
        return await MapCompanyAsync(db, companyId, cancellationToken);
    }

    public async Task<IReadOnlyList<PlatformSubscriptionRowDto>> ListSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.TenantSubscriptions.IgnoreQueryFilters().AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.StartDate)
            .Select(x => new PlatformSubscriptionRowDto
            {
                CompanyId = x.TenantId,
                CompanyName = x.Tenant.Name,
                PlanName = x.Plan.Name,
                Status = x.Status.ToString(),
                BillingInterval = x.BillingInterval.ToString(),
                MonthlyPrice = x.ContractedMonthlyPrice ?? x.Plan.MonthlyPrice,
                AnnualPrice = x.ContractedAnnualPrice ?? x.Plan.AnnualPrice,
                Currency = x.ContractedCurrency ?? x.Plan.BaseCurrency,
                StartDate = x.StartDate,
                CurrentPeriodEnd = x.CurrentPeriodEnd
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<PlatformRevenueDto> GetRevenueAsync(CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var subscriptions = await db.TenantSubscriptions.IgnoreQueryFilters().AsNoTracking()
            .Include(x => x.Plan)
            .Where(x => !x.IsDeleted)
            .ToListAsync(cancellationToken);

        decimal mrr = 0;
        decimal trial = 0;
        decimal atRisk = 0;
        foreach (var subscription in subscriptions.Where(x => x.Plan is not null))
        {
            var monthly = SubscriptionRevenueMath.MonthlyRecurring(subscription.Plan, subscription);
            if (SubscriptionRevenueMath.CountsAsRecurring(subscription.Status))
                mrr += monthly;
            else if (SubscriptionRevenueMath.CountsAsTrialPipeline(subscription.Status))
                trial += monthly;
            else if (SubscriptionRevenueMath.CountsAsAtRisk(subscription.Status))
                atRisk += monthly;
        }

        var completed = await db.BillingTransactions.IgnoreQueryFilters().AsNoTracking()
            .Where(x => !x.IsDeleted && x.Status == BillingTransactionStatus.Completed)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var failed = await db.BillingTransactions.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.Status == BillingTransactionStatus.Failed, cancellationToken);

        var trends = await db.BillingTransactions.IgnoreQueryFilters().AsNoTracking()
            .Where(x => !x.IsDeleted && x.Status == BillingTransactionStatus.Completed)
            .GroupBy(x => new { x.CreatedAt.Year, x.CreatedAt.Month, x.Currency })
            .Select(g => new RevenueTrendPointDto
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Currency = g.Key.Currency,
                CompletedAmount = g.Sum(x => x.Amount),
                PaymentCount = g.Count()
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(cancellationToken);

        var distribution = subscriptions
            .GroupBy(x => x.Plan.Name)
            .Select(g => new PlanDistributionDto { PlanName = g.Key, Subscriptions = g.Count() })
            .OrderByDescending(x => x.Subscriptions)
            .ToList();

        var booked = await db.BillingInvoices.IgnoreQueryFilters().AsNoTracking()
            .Where(x => !x.IsDeleted && x.Status != BillingInvoiceStatus.Void)
            .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0m;
        var copilot = await db.AiUsageLedgers.IgnoreQueryFilters().AsNoTracking()
            .Where(x => !x.IsDeleted)
            .SumAsync(x => (decimal?)x.EstimatedProviderCostUsd, cancellationToken) ?? 0m;
        var agents = await db.NalvaExecutions.IgnoreQueryFilters().AsNoTracking()
            .Where(x => !x.IsDeleted)
            .SumAsync(x => (decimal?)x.EstimatedCostUsd, cancellationToken) ?? 0m;
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        return new PlatformRevenueDto
        {
            MonthlyRecurringRevenue = mrr,
            AnnualRecurringRevenue = mrr * 12m,
            CompletedPaymentTotal = completed,
            CollectedRevenue = completed,
            BookedRevenue = booked,
            TrialPipelineMonthly = trial,
            AtRiskMonthly = atRisk,
            EstimatedCopilotCostUsd = copilot,
            EstimatedAgentCostUsd = agents,
            ActiveSubscriptions = subscriptions.Count(x => x.Status == SubscriptionStatus.Active),
            TrialSubscriptions = subscriptions.Count(x => x.Status == SubscriptionStatus.Trial),
            CancelledSubscriptions = subscriptions.Count(x => x.Status == SubscriptionStatus.Cancelled),
            FailedPayments = failed,
            NewSubscriptionsThisMonth = subscriptions.Count(x => x.Status == SubscriptionStatus.Active && x.StartDate >= monthStart),
            ChurnedThisMonth = subscriptions.Count(x => x.Status == SubscriptionStatus.Cancelled && x.EndDate >= monthStart),
            ActiveAfterTrial = subscriptions.Count(x => x.Status == SubscriptionStatus.Active && x.TrialConsumed),
            Distribution = distribution,
            Trends = trends
        };
    }

    public async Task<AiUsageReportDto> GetAiUsageAsync(
        Guid? companyId,
        string? operation,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var query = db.AiUsageLedgers.IgnoreQueryFilters().AsNoTracking().Where(x => !x.IsDeleted);
        if (companyId is Guid id && id != Guid.Empty)
            query = query.Where(x => x.TenantId == id);
        if (!string.IsNullOrWhiteSpace(operation))
        {
            var key = operation.Trim();
            query = query.Where(x => x.OperationKey == key);
        }
        if (from is DateTime start)
            query = query.Where(x => x.CreatedAt >= start);
        if (to is DateTime end)
            query = query.Where(x => x.CreatedAt < end);

        var totals = new AiUsageTotalsDto
        {
            Requests = await query.CountAsync(cancellationToken),
            FailedRequests = await query.CountAsync(x => x.ProviderFailed, cancellationToken),
            InputTokens = await query.SumAsync(x => (int?)x.InputTokens, cancellationToken) ?? 0,
            OutputTokens = await query.SumAsync(x => (int?)x.OutputTokens, cancellationToken) ?? 0,
            TotalTokens = await query.SumAsync(x => (int?)x.TotalTokens, cancellationToken) ?? 0,
            EstimatedCostUsd = await query.SumAsync(x => (decimal?)x.EstimatedProviderCostUsd, cancellationToken) ?? 0m
        };

        var grouped = await query
            .GroupBy(x => new { x.TenantId, x.OperationKey })
            .Select(g => new
            {
                g.Key.TenantId,
                g.Key.OperationKey,
                Requests = g.Count(),
                Failed = g.Count(x => x.ProviderFailed),
                Input = g.Sum(x => x.InputTokens),
                Output = g.Sum(x => x.OutputTokens),
                Total = g.Sum(x => x.TotalTokens),
                Cost = g.Sum(x => x.EstimatedProviderCostUsd)
            })
            .OrderByDescending(x => x.Total)
            .Take(200)
            .ToListAsync(cancellationToken);

        var names = await CompanyNamesAsync(db, grouped.Select(x => x.TenantId), cancellationToken);
        return new AiUsageReportDto
        {
            Totals = totals,
            Rows = grouped.Select(x => new AiUsageCompanyDto
            {
                CompanyId = x.TenantId,
                CompanyName = names.GetValueOrDefault(x.TenantId, "Unknown company"),
                OperationKey = x.OperationKey,
                Requests = x.Requests,
                FailedRequests = x.Failed,
                InputTokens = x.Input,
                OutputTokens = x.Output,
                TotalTokens = x.Total,
                EstimatedCostUsd = x.Cost
            }).ToList()
        };
    }

    public async Task<PagedResult<AgentExecutionDto>> ListAgentExecutionsAsync(
        Guid? companyId,
        string? agent,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize == 0 ? 25 : pageSize, 1, 100);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var query = db.AgentWorkflowRuns.IgnoreQueryFilters().AsNoTracking().Where(x => !x.IsDeleted);
        if (companyId is Guid id && id != Guid.Empty)
            query = query.Where(x => x.TenantId == id);
        if (!string.IsNullOrWhiteSpace(agent))
        {
            var key = agent.Trim();
            query = query.Where(x => x.AgentKey == key);
        }
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AgentWorkflowStatus>(status, true, out var parsed))
            query = query.Where(x => x.Status == parsed);

        var total = await query.CountAsync(cancellationToken);
        var runs = await query
            .OrderByDescending(x => x.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new { x.Id, x.TenantId, x.AgentKey, x.Title, x.Status, x.StartedAt, x.CompletedAt, x.ErrorMessage })
            .ToListAsync(cancellationToken);
        var names = await CompanyNamesAsync(db, runs.Select(x => x.TenantId), cancellationToken);

        return new PagedResult<AgentExecutionDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = runs.Select(x => MapExecution(x.Id, x.TenantId, names.GetValueOrDefault(x.TenantId, "Unknown company"), x.AgentKey, x.Title, x.Status, x.StartedAt, x.CompletedAt, x.ErrorMessage)).ToList()
        };
    }

    public async Task<AgentExecutionDetailDto> GetAgentExecutionAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var run = await db.AgentWorkflowRuns.IgnoreQueryFilters().AsNoTracking()
            .Include(x => x.Steps)
            .FirstOrDefaultAsync(x => x.Id == executionId && !x.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Agent execution was not found.");

        var name = await db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == run.TenantId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Unknown company";

        var summary = MapExecution(run.Id, run.TenantId, name, run.AgentKey, run.Title, run.Status, run.StartedAt, run.CompletedAt, run.ErrorMessage);
        return new AgentExecutionDetailDto
        {
            Id = summary.Id,
            CompanyId = summary.CompanyId,
            CompanyName = summary.CompanyName,
            AgentKey = summary.AgentKey,
            Title = summary.Title,
            Status = summary.Status,
            StartedAt = summary.StartedAt,
            CompletedAt = summary.CompletedAt,
            DurationSeconds = summary.DurationSeconds,
            ErrorMessage = summary.ErrorMessage,
            Steps = run.Steps
                .OrderBy(x => x.SortOrder)
                .Select(x => new AgentExecutionStepDto
                {
                    StepKey = x.StepKey,
                    Title = x.Title,
                    Status = x.Status.ToString(),
                    Message = x.Message
                })
                .ToList()
        };
    }

    public async Task<PlatformHealthDto> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var database = false;
        try
        {
            database = await db.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception)
        {
            database = false;
        }

        bool? qdrant = null;
        if (_qdrant.Enabled)
        {
            try
            {
                qdrant = await _vectors.IsHealthyAsync(cancellationToken);
            }
            catch (Exception)
            {
                qdrant = false;
            }
        }

        var failed = await db.VectorSyncOutboxMessages.CountAsync(x => x.Status == VectorSyncStatus.Failed, cancellationToken);
        var dead = await db.VectorSyncOutboxMessages.CountAsync(x => x.Status == VectorSyncStatus.Dead, cancellationToken);
        var aiConfigured = !string.IsNullOrWhiteSpace(_ai.OpenAiApiKey) || !string.IsNullOrWhiteSpace(_ai.ApiKey);
        var status = database && qdrant != false ? "Healthy" : database ? "Degraded" : "Unhealthy";

        return new PlatformHealthDto
        {
            Status = status,
            DatabaseConnected = database,
            QdrantConfigured = _qdrant.Enabled,
            QdrantReachable = qdrant,
            AiProviderConfigured = aiConfigured,
            ChatModel = string.IsNullOrWhiteSpace(_ai.OpenAiModel) ? null : _ai.OpenAiModel,
            EmbeddingModel = string.IsNullOrWhiteSpace(_ai.OpenAiEmbeddingModel) ? null : _ai.OpenAiEmbeddingModel,
            FailedJobs = failed,
            DeadJobs = dead,
            CheckedAt = DateTime.UtcNow
        };
    }

    public async Task<IReadOnlyList<PlatformSettingDto>> GetAiSettingsAsync(CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var allowed = PlatformSettingKeys.Allowed.ToArray();
        return await db.PlatformSettings.AsNoTracking()
            .Where(x => allowed.Contains(x.Key))
            .OrderBy(x => x.Key)
            .Select(x => new PlatformSettingDto { Key = x.Key, Value = x.Value, UpdatedAt = x.UpdatedAt })
            .ToListAsync(cancellationToken);
    }

    public async Task<PlatformSettingDto> UpsertAiSettingAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        if (!PlatformSettingKeys.Allowed.Contains(key))
            throw new BadRequestException("That platform setting is not configurable.");

        var cleaned = (value ?? string.Empty).Trim();
        if (cleaned.Length is 0 or > 500)
            throw new BadRequestException("Setting values must be between 1 and 500 characters.");

        RejectSecret(cleaned);
        if (key.Equals(PlatformSettingKeys.RequestLimit, StringComparison.OrdinalIgnoreCase) &&
            (!int.TryParse(cleaned, out var limit) || limit < 0))
        {
            throw new BadRequestException("The AI request limit must be a whole number.");
        }

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var setting = await db.PlatformSettings.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (setting is null)
        {
            setting = new PlatformSetting { Key = key };
            db.PlatformSettings.Add(setting);
        }

        setting.Value = cleaned;
        setting.UpdatedAt = DateTime.UtcNow;
        setting.UpdatedByUserId = _currentUser.UserId;
        await db.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(db, "PlatformAiSettingUpdated", "PlatformSetting", key, null, null, cancellationToken);
        return new PlatformSettingDto { Key = setting.Key, Value = setting.Value, UpdatedAt = setting.UpdatedAt };
    }

    public async Task<IReadOnlyList<AnnouncementDto>> ListAnnouncementsAsync(CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = await db.PlatformAnnouncements.AsNoTracking()
            .Include(x => x.Targets)
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(MapAnnouncement).ToList();
    }

    public async Task<AnnouncementDto> SaveAnnouncementAsync(
        Guid? id,
        SaveAnnouncementRequest request,
        CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
            throw new BadRequestException("Title and body are required.");

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        PlatformAnnouncement announcement;
        if (id is Guid existing)
        {
            announcement = await db.PlatformAnnouncements.Include(x => x.Targets)
                .FirstOrDefaultAsync(x => x.Id == existing && !x.IsDeleted, cancellationToken)
                ?? throw new NotFoundException("Announcement was not found.");
        }
        else
        {
            announcement = new PlatformAnnouncement { CreatedByUserId = _currentUser.UserId ?? "platform" };
            db.PlatformAnnouncements.Add(announcement);
        }

        announcement.Title = request.Title.Trim();
        announcement.Body = request.Body.Trim();
        announcement.IsPublished = request.Publish;
        announcement.IsActive = true;
        announcement.StartsAt = request.StartsAt;
        announcement.EndsAt = request.EndsAt;
        announcement.TargetAllCompanies = request.TargetAllCompanies;
        announcement.UpdatedAt = DateTime.UtcNow;
        announcement.Targets.Clear();
        if (!request.TargetAllCompanies)
        {
            foreach (var companyId in (request.CompanyIds ?? []).Where(x => x != Guid.Empty).Distinct())
                announcement.Targets.Add(new PlatformAnnouncementTarget { TargetTenantId = companyId });
        }

        await db.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(db, id is null ? "AnnouncementCreated" : "AnnouncementUpdated", "PlatformAnnouncement", announcement.Id.ToString(), null, announcement.Title, cancellationToken);
        return MapAnnouncement(announcement);
    }

    public async Task DeactivateAnnouncementAsync(Guid id, CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var announcement = await db.PlatformAnnouncements.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Announcement was not found.");
        announcement.IsActive = false;
        announcement.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(db, "AnnouncementDeactivated", "PlatformAnnouncement", id.ToString(), null, announcement.Title, cancellationToken);
    }

    private async Task<CompanyProfileDto> MapCompanyAsync(BusinessOSDbContext db, Guid companyId, CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == companyId && !x.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Company was not found.");

        var members = await db.CompanyMemberships.IgnoreQueryFilters().CountAsync(
            x => x.TenantId == companyId && x.IsActive && !x.IsDeleted, cancellationToken);
        var subscription = await db.TenantSubscriptions.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == companyId && !x.IsDeleted)
            .Select(x => x.Status.ToString())
            .FirstOrDefaultAsync(cancellationToken);
        var usage = await db.TenantUsages.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == companyId && !x.IsDeleted)
            .Select(x => new { x.AiRequestsUsed, x.StorageUsedMb })
            .FirstOrDefaultAsync(cancellationToken);

        return new CompanyProfileDto
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            BusinessType = tenant.BusinessType,
            Status = tenant.Status.ToString(),
            IsActive = tenant.IsActive,
            Email = tenant.Email,
            Country = tenant.Country,
            SubscriptionPlan = tenant.SubscriptionPlan,
            SubscriptionStatus = subscription,
            CreatedAt = tenant.CreatedAt,
            UpdatedAt = tenant.UpdatedAt,
            MemberCount = members,
            Description = tenant.Description,
            Phone = tenant.Phone,
            Website = tenant.Website,
            Address = tenant.Address,
            Timezone = tenant.Timezone,
            Currency = tenant.Currency,
            RegistrationNumber = tenant.RegistrationNumber,
            EnabledModulesJson = tenant.EnabledModulesJson,
            AiRequestsUsed = usage?.AiRequestsUsed ?? 0,
            StorageUsedMb = usage?.StorageUsedMb ?? 0
        };
    }

    private static AgentExecutionDto MapExecution(
        Guid id,
        Guid companyId,
        string companyName,
        string agentKey,
        string title,
        AgentWorkflowStatus status,
        DateTime started,
        DateTime? completed,
        string? error) => new()
    {
        Id = id,
        CompanyId = companyId,
        CompanyName = companyName,
        AgentKey = agentKey,
        Title = title,
        Status = status.ToString(),
        StartedAt = started,
        CompletedAt = completed,
        DurationSeconds = completed is DateTime done ? Math.Max(0, (done - started).TotalSeconds) : null,
        ErrorMessage = error,
        RetryAttempts = null,
        TotalTokens = null
    };

    private static AnnouncementDto MapAnnouncement(PlatformAnnouncement announcement) => new()
    {
        Id = announcement.Id,
        Title = announcement.Title,
        Body = announcement.Body,
        IsPublished = announcement.IsPublished,
        IsActive = announcement.IsActive,
        StartsAt = announcement.StartsAt,
        EndsAt = announcement.EndsAt,
        TargetAllCompanies = announcement.TargetAllCompanies,
        CompanyIds = announcement.Targets.Select(x => x.TargetTenantId).ToList(),
        CreatedAt = announcement.CreatedAt
    };

    private static async Task<Dictionary<Guid, string>> CompanyNamesAsync(
        BusinessOSDbContext db,
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0)
            return [];

        return await db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .Where(x => list.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
    }

    private async Task WriteAuditAsync(
        BusinessOSDbContext db,
        string action,
        string entityType,
        string entityId,
        Guid? companyId,
        string? metadata,
        CancellationToken cancellationToken)
    {
        var ip = _http.HttpContext?.Connection.RemoteIpAddress?.ToString();
        db.PlatformAuditLogs.Add(new PlatformAuditLog
        {
            ActorUserId = _currentUser.UserId ?? "platform",
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            SubjectTenantId = companyId,
            MetadataJson = metadata,
            IpAddress = string.IsNullOrWhiteSpace(ip) ? null : ip.Length <= 64 ? ip : ip[..64],
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private void RequireAdmin()
    {
        if (!_admin.IsSuperAdmin)
            throw new ForbiddenException("Platform administrator access is required.");
    }

    private static void RejectSecret(string value)
    {
        var lowered = value.ToLowerInvariant();
        if (lowered.Contains("sk-", StringComparison.Ordinal) ||
            lowered.Contains("crsr_", StringComparison.Ordinal) ||
            lowered.Contains("api_key", StringComparison.Ordinal) ||
            lowered.Contains("apikey", StringComparison.Ordinal) ||
            lowered.Contains("secret", StringComparison.Ordinal) ||
            lowered.Contains("password", StringComparison.Ordinal))
        {
            throw new BadRequestException("API keys and secrets cannot be stored in platform settings.");
        }
    }
}
