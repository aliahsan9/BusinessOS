using System.Security.Cryptography;
using System.Text.RegularExpressions;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Application.Features.Growth;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class GrowthService : IGrowthService
{
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IDbContextFactory<BusinessOSDbContext> _factory;
    private readonly ICurrentUserService _user;
    private readonly ITenantProvider _tenant;
    private readonly ISuperAdminContext _admin;

    public GrowthService(
        IDbContextFactory<BusinessOSDbContext> factory,
        ICurrentUserService user,
        ITenantProvider tenant,
        ISuperAdminContext admin)
    {
        _factory = factory;
        _user = user;
        _tenant = tenant;
        _admin = admin;
    }

    public async Task<MarketingLeadDto> SubmitLeadAsync(MarketingLeadWrite request, CancellationToken cancellationToken = default)
    {
        var email = RequireEmail(request.Email);
        var kind = NormalizeKind(request.Kind);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        var recent = await db.MarketingLeads.AsNoTracking()
            .Where(x => !x.IsDeleted && x.Email == email && x.CreatedAt >= DateTime.UtcNow.AddMinutes(-10))
            .CountAsync(cancellationToken);
        if (recent >= 3)
            throw new BadRequestException("Too many requests from this email. Please wait a few minutes.");

        var lead = new MarketingLead
        {
            FullName = Required(request.FullName, "Full name", 200),
            Email = email,
            Company = Optional(request.Company, 200),
            Kind = kind,
            Topic = Required(request.Topic, "Topic", 100),
            Subject = Required(request.Subject, "Subject", 200),
            Message = Required(request.Message, "Message", 4000),
            SourcePath = Optional(request.SourcePath, 200),
            ReferralCode = Optional(request.ReferralCode, 32)?.ToUpperInvariant(),
            MarketingOptIn = request.MarketingOptIn,
            ConsentAt = request.MarketingOptIn ? DateTime.UtcNow : null,
            Status = MarketingLeadStatuses.New
        };

        db.MarketingLeads.Add(lead);
        await db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(lead.ReferralCode))
            await AttributeReferralAsync(lead.ReferralCode, email, null, cancellationToken);

        return MapLead(lead);
    }

    public async Task<IReadOnlyList<MarketingLeadDto>> ListLeadsAsync(string? status, string? kind, CancellationToken cancellationToken = default)
    {
        RequirePlatform();
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var query = db.MarketingLeads.AsNoTracking().Where(x => !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status.Trim());
        if (!string.IsNullOrWhiteSpace(kind))
            query = query.Where(x => x.Kind == kind.Trim());

        var rows = await query.OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(cancellationToken);
        return rows.Select(MapLead).ToList();
    }

    public async Task<MarketingLeadDto> UpdateLeadStatusAsync(Guid id, MarketingLeadStatusWrite request, CancellationToken cancellationToken = default)
    {
        RequirePlatform();
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var lead = await db.MarketingLeads.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Lead not found.");

        lead.Status = Required(request.Status, "Status", 32);
        if (request.Notes is not null)
            lead.Notes = Optional(request.Notes, 4000);
        lead.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return MapLead(lead);
    }

    public async Task<PlatformSupportTicketDto> CreateSupportTicketAsync(PlatformSupportTicketWrite request, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var name = request.RequesterName;
        var email = request.RequesterEmail;

        var authenticated = !string.IsNullOrWhiteSpace(_user.UserId);
        if (authenticated)
        {
            name = string.IsNullOrWhiteSpace(name) ? (_user.Email ?? _user.UserId ?? "Customer") : name;
            email = string.IsNullOrWhiteSpace(email) ? (_user.Email ?? "") : email;
        }

        var ticket = new PlatformSupportTicket
        {
            TenantId = _tenant.HasTenant() ? _tenant.TenantId : null,
            UserId = authenticated ? _user.UserId : null,
            RequesterName = Required(name, "Name", 200),
            RequesterEmail = RequireEmail(email),
            Category = NormalizeCategory(request.Category),
            Subject = Required(request.Subject, "Subject", 200),
            Message = Required(request.Message, "Message", 4000),
            Priority = string.IsNullOrWhiteSpace(request.Priority) ? "Normal" : Required(request.Priority, "Priority", 16),
            Status = PlatformSupportStatuses.Open
        };

        db.PlatformSupportTickets.Add(ticket);
        await db.SaveChangesAsync(cancellationToken);
        return MapTicket(ticket);
    }

    public async Task<IReadOnlyList<PlatformSupportTicketDto>> ListMySupportTicketsAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_user.UserId))
            throw new ForbiddenException("Sign in to view support tickets.");

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var email = _user.Email ?? "";
        var userId = _user.UserId;
        var rows = await db.PlatformSupportTickets.AsNoTracking()
            .Where(x => !x.IsDeleted && (x.UserId == userId || x.RequesterEmail == email))
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
        return rows.Select(MapTicket).ToList();
    }

    public async Task<IReadOnlyList<PlatformSupportTicketDto>> ListPlatformSupportTicketsAsync(string? status, CancellationToken cancellationToken = default)
    {
        RequirePlatform();
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var query = db.PlatformSupportTickets.AsNoTracking().Where(x => !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status.Trim());
        var rows = await query.OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(cancellationToken);
        return rows.Select(MapTicket).ToList();
    }

    public async Task<PlatformSupportTicketDto> UpdateSupportTicketAsync(Guid id, PlatformSupportUpdateWrite request, CancellationToken cancellationToken = default)
    {
        RequirePlatform();
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var ticket = await db.PlatformSupportTickets.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Support ticket not found.");

        ticket.Status = Required(request.Status, "Status", 32);
        ticket.AgentReply = Optional(request.AgentReply, 4000);
        ticket.EscalatedToHuman = request.EscalatedToHuman;
        if (ticket.Status is PlatformSupportStatuses.Resolved or PlatformSupportStatuses.Closed)
            ticket.ResolvedAt ??= DateTime.UtcNow;
        ticket.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return MapTicket(ticket);
    }

    public async Task<ReferralCodeDto> GetOrCreateReferralCodeAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenant.HasTenant())
            throw new ForbiddenException("A company context is required.");

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var existing = await db.ReferralCodes.FirstOrDefaultAsync(x => x.TenantId == _tenant.TenantId && !x.IsDeleted, cancellationToken);
        if (existing is null)
        {
            existing = new ReferralCode
            {
                TenantId = _tenant.TenantId,
                Code = await GenerateUniqueCodeAsync(db, cancellationToken),
                IsActive = true
            };
            db.ReferralCodes.Add(existing);
            await db.SaveChangesAsync(cancellationToken);
        }

        return MapReferral(existing);
    }

    public async Task<ReferralValidateDto> ValidateReferralCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = (code ?? "").Trim().ToUpperInvariant();
        if (normalized.Length == 0)
            return new ReferralValidateDto { Valid = false, Message = "Referral code is required." };

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var row = await db.ReferralCodes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == normalized && !x.IsDeleted, cancellationToken);
        if (row is null || !row.IsActive)
            return new ReferralValidateDto { Valid = false, Code = normalized, Message = "Referral code is not valid." };
        if (row.RedemptionCount >= row.MaxRedemptions)
            return new ReferralValidateDto { Valid = false, Code = normalized, Message = "Referral code has reached its limit." };

        return new ReferralValidateDto { Valid = true, Code = normalized, Message = "Referral code accepted." };
    }

    public async Task AttributeReferralAsync(string code, string email, Guid? companyId, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateReferralCodeAsync(code, cancellationToken);
        if (!validation.Valid || string.IsNullOrWhiteSpace(validation.Code))
            return;

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var referral = await db.ReferralCodes.FirstOrDefaultAsync(x => x.Code == validation.Code && !x.IsDeleted, cancellationToken);
        if (referral is null)
            return;

        var normalizedEmail = RequireEmail(email);
        var already = await db.ReferralRedemptions.AnyAsync(
            x => !x.IsDeleted && x.ReferralCodeId == referral.Id && x.ReferredEmail == normalizedEmail,
            cancellationToken);
        if (already)
            return;

        db.ReferralRedemptions.Add(new ReferralRedemption
        {
            ReferralCodeId = referral.Id,
            ReferrerTenantId = referral.TenantId,
            ReferredTenantId = companyId,
            ReferredEmail = normalizedEmail,
            Status = companyId.HasValue ? ReferralRedemptionStatuses.Registered : ReferralRedemptionStatuses.Attributed
        });
        referral.RedemptionCount += 1;
        referral.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SaaSAnalyticsDto> GetSaaSAnalyticsAsync(CancellationToken cancellationToken = default)
    {
        RequirePlatform();
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var subscriptions = await db.TenantSubscriptions.IgnoreQueryFilters().AsNoTracking()
            .Include(x => x.Plan)
            .Where(x => !x.IsDeleted)
            .ToListAsync(cancellationToken);

        decimal mrr = 0;
        foreach (var subscription in subscriptions.Where(x => x.Plan is not null))
        {
            var monthly = SubscriptionRevenueMath.MonthlyRecurring(subscription.Plan, subscription);
            if (SubscriptionRevenueMath.CountsAsRecurring(subscription.Status))
                mrr += monthly;
        }

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var active = subscriptions.Count(x => x.Status == SubscriptionStatus.Active);
        var trials = subscriptions.Count(x => x.Status == SubscriptionStatus.Trial);
        var cancelled = subscriptions.Count(x => x.Status == SubscriptionStatus.Cancelled);
        var churnedThisMonth = subscriptions.Count(x => x.Status == SubscriptionStatus.Cancelled && x.EndDate >= monthStart);
        var newThisMonth = subscriptions.Count(x => x.Status == SubscriptionStatus.Active && x.StartDate >= monthStart);
        var activeAfterTrial = subscriptions.Count(x => x.Status == SubscriptionStatus.Active && x.TrialConsumed);
        var trialConvertedPool = subscriptions.Count(x => x.TrialConsumed);
        var failedPayments = await db.BillingTransactions.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.Status == BillingTransactionStatus.Failed, cancellationToken);
        var openTickets = await db.PlatformSupportTickets.AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.Status != PlatformSupportStatuses.Resolved && x.Status != PlatformSupportStatuses.Closed, cancellationToken);
        var newLeads = await db.MarketingLeads.AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.CreatedAt >= monthStart, cancellationToken);
        var estimatedAi = await db.AiUsageLedgers.IgnoreQueryFilters().AsNoTracking()
            .Where(x => !x.IsDeleted)
            .SumAsync(x => (decimal?)x.EstimatedProviderCostUsd, cancellationToken) ?? 0m;
        estimatedAi += await db.NalvaExecutions.IgnoreQueryFilters().AsNoTracking()
            .Where(x => !x.IsDeleted)
            .SumAsync(x => (decimal?)x.EstimatedCostUsd, cancellationToken) ?? 0m;

        var payingTenants = Math.Max(active, 1);
        decimal? arpt = active > 0 ? Math.Round(mrr / active, 2) : null;
        decimal? conversion = trialConvertedPool > 0
            ? Math.Round((decimal)activeAfterTrial / trialConvertedPool * 100m, 2)
            : null;
        var startOfMonthActiveBase = active + churnedThisMonth;
        decimal? churnRate = startOfMonthActiveBase > 0
            ? Math.Round((decimal)churnedThisMonth / startOfMonthActiveBase * 100m, 2)
            : null;

        return new SaaSAnalyticsDto
        {
            MonthlyRecurringRevenue = mrr,
            AnnualRecurringRevenue = mrr * 12m,
            ActiveSubscriptions = active,
            TrialSubscriptions = trials,
            CancelledSubscriptions = cancelled,
            FailedPayments = failedPayments,
            NewSubscriptionsThisMonth = newThisMonth,
            ChurnedThisMonth = churnedThisMonth,
            ActiveAfterTrial = activeAfterTrial,
            OpenSupportTickets = openTickets,
            NewMarketingLeadsThisMonth = newLeads,
            AverageRevenuePerTenant = arpt,
            TrialToPaidConversionRate = conversion,
            MonthlyChurnRate = churnRate,
            EstimatedAiCostPerTenantUsd = Math.Round(estimatedAi / payingTenants, 4),
            EstimatedGrossMarginUsd = Math.Round(mrr - estimatedAi, 2),
            CustomerLifetimeValue = null,
            CustomerAcquisitionCost = null,
            MeasuredMetrics =
            [
                "MonthlyRecurringRevenue",
                "AnnualRecurringRevenue",
                "ActiveSubscriptions",
                "TrialSubscriptions",
                "CancelledSubscriptions",
                "FailedPayments",
                "NewSubscriptionsThisMonth",
                "ChurnedThisMonth",
                "ActiveAfterTrial",
                "OpenSupportTickets",
                "NewMarketingLeadsThisMonth",
                "AverageRevenuePerTenant",
                "TrialToPaidConversionRate",
                "MonthlyChurnRate"
            ],
            EstimatedMetrics =
            [
                "EstimatedAiCostPerTenantUsd",
                "EstimatedGrossMarginUsd"
            ],
            UnavailableMetrics =
            [
                "CustomerLifetimeValue",
                "CustomerAcquisitionCost"
            ]
        };
    }

    private async Task<string> GenerateUniqueCodeAsync(BusinessOSDbContext db, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var bytes = RandomNumberGenerator.GetBytes(5);
            var code = Convert.ToHexString(bytes)[..8];
            var exists = await db.ReferralCodes.AnyAsync(x => x.Code == code && !x.IsDeleted, cancellationToken);
            if (!exists)
                return code;
        }

        throw new InvalidOperationException("Unable to allocate a referral code.");
    }

    private void RequirePlatform()
    {
        if (!_admin.IsSuperAdmin)
            throw new ForbiddenException("Platform administrator access is required.");
    }

    private static string NormalizeKind(string? kind)
    {
        var value = (kind ?? MarketingLeadKinds.Contact).Trim();
        return value switch
        {
            MarketingLeadKinds.Demo => MarketingLeadKinds.Demo,
            MarketingLeadKinds.Waitlist => MarketingLeadKinds.Waitlist,
            MarketingLeadKinds.Support => MarketingLeadKinds.Support,
            _ => MarketingLeadKinds.Contact
        };
    }

    private static string NormalizeCategory(string? category)
    {
        var value = (category ?? PlatformSupportCategories.General).Trim();
        return value switch
        {
            PlatformSupportCategories.Billing => PlatformSupportCategories.Billing,
            PlatformSupportCategories.Technical => PlatformSupportCategories.Technical,
            PlatformSupportCategories.Onboarding => PlatformSupportCategories.Onboarding,
            PlatformSupportCategories.Account => PlatformSupportCategories.Account,
            _ => PlatformSupportCategories.General
        };
    }

    private static string RequireEmail(string? email)
    {
        var value = Required(email, "Email", 256).ToLowerInvariant();
        if (!EmailPattern.IsMatch(value))
            throw new BadRequestException("Email is not valid.");
        return value;
    }

    private static string Required(string? value, string label, int max)
    {
        var text = value?.Trim() ?? "";
        if (text.Length == 0)
            throw new BadRequestException($"{label} is required.");
        if (text.Length > max)
            throw new BadRequestException($"{label} must be {max} characters or fewer.");
        return text;
    }

    private static string? Optional(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var text = value.Trim();
        if (text.Length > max)
            throw new BadRequestException($"Value must be {max} characters or fewer.");
        return text;
    }

    private static MarketingLeadDto MapLead(MarketingLead lead) => new()
    {
        Id = lead.Id,
        FullName = lead.FullName,
        Email = lead.Email,
        Company = lead.Company,
        Kind = lead.Kind,
        Topic = lead.Topic,
        Subject = lead.Subject,
        Message = lead.Message,
        Status = lead.Status,
        SourcePath = lead.SourcePath,
        ReferralCode = lead.ReferralCode,
        MarketingOptIn = lead.MarketingOptIn,
        CreatedAt = lead.CreatedAt,
        ConvertedCompanyId = lead.ConvertedCompanyId
    };

    private static PlatformSupportTicketDto MapTicket(PlatformSupportTicket ticket) => new()
    {
        Id = ticket.Id,
        TenantId = ticket.TenantId,
        RequesterName = ticket.RequesterName,
        RequesterEmail = ticket.RequesterEmail,
        Category = ticket.Category,
        Subject = ticket.Subject,
        Message = ticket.Message,
        Status = ticket.Status,
        Priority = ticket.Priority,
        AgentReply = ticket.AgentReply,
        EscalatedToHuman = ticket.EscalatedToHuman,
        CreatedAt = ticket.CreatedAt,
        ResolvedAt = ticket.ResolvedAt
    };

    private static ReferralCodeDto MapReferral(ReferralCode code) => new()
    {
        Id = code.Id,
        Code = code.Code,
        IsActive = code.IsActive,
        RedemptionCount = code.RedemptionCount,
        MaxRedemptions = code.MaxRedemptions,
        SharePath = $"/register-business?ref={code.Code}"
    };
}
