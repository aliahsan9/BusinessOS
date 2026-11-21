using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Application.Features.Integrations;
using BusinessOS.Application.Features.Marketing;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Services.Marketing;

public sealed class MarketingOperationsService : IMarketingOperationsService
{
    private static readonly string[] DefaultPlatforms = ["LinkedIn", "Facebook", "Instagram"];
    private static readonly HashSet<string> Objectives = new(StringComparer.OrdinalIgnoreCase)
    {
        "BrandAwareness", "LeadGeneration", "ProductPromotion", "CustomerEngagement",
        "WebsiteTraffic", "EventPromotion", "CustomerRetention", "ProductLaunch"
    };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly ITenantProvider _tenant;
    private readonly ILlmChatClient? _llm;
    private readonly IMarketingImageClient? _images;
    private readonly IIntegrationPlatform? _integrations;
    private readonly IEnumerable<IMarketingPublishTransport> _transports;
    private readonly ILogger<MarketingOperationsService> _logger;

    public MarketingOperationsService(
        IApplicationDbContext db,
        ICurrentUserService user,
        ITenantProvider tenant,
        IEnumerable<IMarketingPublishTransport> transports,
        ILogger<MarketingOperationsService> logger,
        ILlmChatClient? llm = null,
        IMarketingImageClient? images = null,
        IIntegrationPlatform? integrations = null)
    {
        _db = db;
        _user = user;
        _tenant = tenant;
        _transports = transports;
        _logger = logger;
        _llm = llm;
        _images = images;
        _integrations = integrations;
    }

    private Guid TenantId => _tenant.HasTenant() ? _tenant.TenantId : Ops.Tenant(_user);

    public async Task<MarketingBrandProfileDto> GetOrSeedBrandProfileAsync(CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaView, PermissionCodes.NalvaManage, PermissionCodes.OrganizationView);
        var existing = await _db.MarketingBrandProfiles.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
            return MapBrand(existing);

        var seeded = await SeedBrandAsync(cancellationToken);
        return MapBrand(seeded);
    }

    public async Task<MarketingBrandProfileDto> UpsertBrandProfileAsync(UpsertBrandProfileRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaManage);
        var row = await _db.MarketingBrandProfiles.FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            row = await SeedBrandAsync(cancellationToken);
            row = await _db.MarketingBrandProfiles.FirstAsync(x => x.Id == row.Id, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.BusinessName)) row.BusinessName = request.BusinessName.Trim();
        if (request.Industry is not null) row.Industry = TrimOrNull(request.Industry, 120);
        if (request.ProductsServicesJson is not null) row.ProductsServicesJson = request.ProductsServicesJson;
        if (request.TargetAudience is not null) row.TargetAudience = TrimOrNull(request.TargetAudience, 2000);
        if (request.BrandDescription is not null) row.BrandDescription = TrimOrNull(request.BrandDescription, 4000);
        if (request.BrandVoice is not null) row.BrandVoice = TrimOrNull(request.BrandVoice, 1000);
        if (request.BrandColorsJson is not null) row.BrandColorsJson = request.BrandColorsJson;
        if (request.LogoUrl is not null) row.LogoUrl = TrimOrNull(request.LogoUrl, 1000);
        if (request.Website is not null) row.Website = TrimOrNull(request.Website, 500);
        if (request.SocialAccountsJson is not null) row.SocialAccountsJson = request.SocialAccountsJson;
        if (request.MarketingObjectivesJson is not null) row.MarketingObjectivesJson = request.MarketingObjectivesJson;
        if (request.PreferredLanguagesJson is not null) row.PreferredLanguagesJson = request.PreferredLanguagesJson;
        if (request.ContentRestrictions is not null) row.ContentRestrictions = TrimOrNull(request.ContentRestrictions, 2000);
        if (request.CompetitorsJson is not null) row.CompetitorsJson = request.CompetitorsJson;
        if (request.MarketingPreferencesJson is not null) row.MarketingPreferencesJson = request.MarketingPreferencesJson;
        if (request.SourceNotes is not null) row.SourceNotes = TrimOrNull(request.SourceNotes, 2000);
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapBrand(row);
    }

    public async Task<MarketingCampaignDto> GenerateStrategyAsync(GenerateStrategyRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaManage);
        var brand = await EnsureBrandTrackedAsync(cancellationToken);
        var segments = await _db.CustomerSegments.AsNoTracking().CountAsync(cancellationToken);
        var customers = await _db.Customers.AsNoTracking().CountAsync(cancellationToken);
        var objective = NormalizeObjective(request.Objective);
        var title = string.IsNullOrWhiteSpace(request.Title) ? $"{brand.BusinessName} {objective} strategy" : request.Title.Trim();

        MarketingCampaign campaign;
        if (request.CampaignId is Guid id)
        {
            campaign = await _db.MarketingCampaigns.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? throw new NotFoundException("Campaign not found.");
        }
        else
        {
            campaign = new MarketingCampaign
            {
                TenantId = TenantId,
                Title = title,
                Objective = objective,
                TargetAudience = request.TargetAudience ?? brand.TargetAudience,
                Status = "Draft",
                AgentKey = request.AgentKey ?? "marketing"
            };
            _db.MarketingCampaigns.Add(campaign);
        }

        var body = await BuildStrategyBodyAsync(brand, objective, segments, customers, cancellationToken);
        campaign.Title = title;
        campaign.Objective = objective;
        campaign.TargetAudience = request.TargetAudience ?? brand.TargetAudience ?? campaign.TargetAudience;
        campaign.StrategyBody = body;
        campaign.ContentThemesJson = JsonSerializer.Serialize(new[] { objective, brand.Industry ?? "general", "company-voice" });
        campaign.RecommendedPlatformsJson = JsonSerializer.Serialize(DefaultPlatforms);
        campaign.PublishingFrequency = "3x weekly";
        campaign.ContentFormatsJson = JsonSerializer.Serialize(new[] { "Post", "Carousel", "ShortVideoBrief" });
        campaign.SuccessMetricsJson = JsonSerializer.Serialize(new[] { "approved drafts", "scheduled posts", "verified platform metrics when connected" });
        campaign.EstimatedAiCostUsd = 0.05m;
        campaign.StrategyApproved = false;
        campaign.StrategyApprovedAt = null;
        campaign.StrategyApprovedByUserId = null;
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapCampaign(campaign);
    }

    public async Task<MarketingCampaignDto> UpdateStrategyAsync(Guid campaignId, UpdateStrategyRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaManage);
        var campaign = await RequireCampaignAsync(campaignId, cancellationToken);
        if (request.StrategyBody is not null) campaign.StrategyBody = TrimOrNull(request.StrategyBody, 16000);
        if (request.ContentThemesJson is not null) campaign.ContentThemesJson = request.ContentThemesJson;
        if (request.RecommendedPlatformsJson is not null) campaign.RecommendedPlatformsJson = request.RecommendedPlatformsJson;
        if (request.PublishingFrequency is not null) campaign.PublishingFrequency = TrimOrNull(request.PublishingFrequency, 80);
        if (request.ContentFormatsJson is not null) campaign.ContentFormatsJson = request.ContentFormatsJson;
        if (request.SuccessMetricsJson is not null) campaign.SuccessMetricsJson = request.SuccessMetricsJson;
        campaign.StrategyApproved = false;
        campaign.StrategyApprovedAt = null;
        campaign.StrategyApprovedByUserId = null;
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapCampaign(campaign);
    }

    public async Task<MarketingCampaignDto> ApproveStrategyAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaApprove, PermissionCodes.NalvaManage);
        var campaign = await RequireCampaignAsync(campaignId, cancellationToken);
        if (string.IsNullOrWhiteSpace(campaign.StrategyBody))
            throw new BadRequestException("Strategy body is empty.");
        campaign.StrategyApproved = true;
        campaign.StrategyApprovedAt = DateTime.UtcNow;
        campaign.StrategyApprovedByUserId = Ops.User(_user);
        if (campaign.Status == "Draft")
            campaign.Status = "Active";
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapCampaign(campaign);
    }

    public async Task<MarketingCampaignDto> CreateCampaignAsync(CreateCampaignRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaManage);
        var brand = await EnsureBrandTrackedAsync(cancellationToken);
        var campaign = new MarketingCampaign
        {
            TenantId = TenantId,
            Title = Ops.Required(request.Title, "Title"),
            Objective = NormalizeObjective(request.Objective),
            TargetAudience = request.TargetAudience ?? brand.TargetAudience,
            ContentThemesJson = request.ContentThemesJson ?? "[]",
            RecommendedPlatformsJson = request.RecommendedPlatformsJson ?? JsonSerializer.Serialize(DefaultPlatforms),
            PublishingFrequency = request.PublishingFrequency,
            ContentFormatsJson = request.ContentFormatsJson ?? "[\"Post\"]",
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            SuccessMetricsJson = request.SuccessMetricsJson ?? "[]",
            StrategyBody = request.StrategyBody,
            Status = "Draft",
            AgentKey = request.AgentKey ?? "marketing"
        };
        _db.MarketingCampaigns.Add(campaign);
        await _db.SaveChangesAsync(cancellationToken);
        return MapCampaign(campaign);
    }

    public async Task<IReadOnlyList<MarketingCampaignDto>> ListCampaignsAsync(string? status = null, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaView, PermissionCodes.NalvaManage);
        var query = _db.MarketingCampaigns.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);
        var rows = await query.OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(cancellationToken);
        return rows.Select(MapCampaign).ToList();
    }

    public async Task<MarketingCampaignDto> GetCampaignAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaView, PermissionCodes.NalvaManage);
        return MapCampaign(await RequireCampaignAsync(id, cancellationToken));
    }

    public async Task<MarketingCampaignDto> PauseResumeCampaignAsync(Guid id, bool pause, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaManage);
        var campaign = await RequireCampaignAsync(id, cancellationToken);
        campaign.Status = pause ? "Paused" : "Active";
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapCampaign(campaign);
    }

    public async Task<IReadOnlyList<MarketingContentItemDto>> GenerateContentCalendarAsync(GenerateContentCalendarRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaManage);
        var brand = await EnsureBrandTrackedAsync(cancellationToken);
        var days = Math.Clamp(request.Days <= 0 ? 7 : request.Days, 1, 31);
        var platforms = (request.Platforms is { Count: > 0 } ? request.Platforms : DefaultPlatforms).ToList();
        var start = (request.StartDate ?? DateTime.UtcNow.Date).ToUniversalTime();
        var campaignId = request.CampaignId;
        if (campaignId is null)
        {
            var campaign = await CreateCampaignAsync(new CreateCampaignRequest
            {
                Title = $"{brand.BusinessName} {days}-day calendar",
                Objective = "BrandAwareness",
                AgentKey = request.AgentKey ?? "marketing"
            }, cancellationToken);
            campaignId = campaign.Id;
        }

        var items = new List<MarketingContentItem>();
        for (var day = 0; day < days; day++)
        {
            var platform = platforms[day % platforms.Count];
            var when = start.AddDays(day).AddHours(10);
            var item = await BuildContentItemAsync(brand, campaignId, platform, "Post", null, when, request.AgentKey, null, cancellationToken, save: false);
            items.Add(item);
            _db.MarketingContentItems.Add(item);
        }
        await _db.SaveChangesAsync(cancellationToken);
        return items.Select(MapContent).ToList();
    }

    public async Task<MarketingContentItemDto> GenerateContentItemAsync(GenerateContentItemRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaManage);
        var brand = await EnsureBrandTrackedAsync(cancellationToken);
        var item = await BuildContentItemAsync(
            brand,
            request.CampaignId,
            request.Platform ?? "LinkedIn",
            request.ContentType ?? "Post",
            request.Brief,
            request.SuggestedPublishAt,
            request.AgentKey,
            request.ExecutionId,
            cancellationToken,
            save: true,
            title: request.Title);
        return MapContent(item);
    }

    public async Task<MarketingContentItemDto> UpdateContentItemAsync(Guid id, UpdateContentItemRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaManage);
        var item = await RequireContentAsync(id, cancellationToken);
        var before = Fingerprint(item);
        if (request.Title is not null) item.Title = Ops.Required(request.Title, "Title");
        if (request.Body is not null) item.Body = request.Body;
        if (request.Caption is not null) item.Caption = TrimOrNull(request.Caption, 4000);
        if (request.HashtagsJson is not null) item.HashtagsJson = request.HashtagsJson;
        if (request.CallToAction is not null) item.CallToAction = TrimOrNull(request.CallToAction, 500);
        if (request.SuggestedPublishAt is not null) item.SuggestedPublishAt = request.SuggestedPublishAt;
        if (request.ScheduledPublishAt is not null) item.ScheduledPublishAt = request.ScheduledPublishAt;
        if (request.ImageAssetIdsJson is not null) item.ImageAssetIdsJson = request.ImageAssetIdsJson;
        if (request.ConnectedAccountProvider is not null) item.ConnectedAccountProvider = TrimOrNull(request.ConnectedAccountProvider, 40);
        if (request.ConnectedAccountExternalId is not null) item.ConnectedAccountExternalId = TrimOrNull(request.ConnectedAccountExternalId, 200);
        item.ContentHash = HashText($"{item.Title}|{item.Body}|{item.Caption}");
        var after = Fingerprint(item);
        if (!string.Equals(before, after, StringComparison.Ordinal))
        {
            item.ApprovalFingerprint = null;
            item.ApprovedAt = null;
            item.ApprovedByUserId = null;
            if (item.ApprovalStatus is "Approved" or "Scheduled" or "Review")
                item.ApprovalStatus = "Draft";
        }
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapContent(item);
    }

    public async Task<IReadOnlyList<MarketingContentItemDto>> ListContentAsync(Guid? campaignId = null, string? status = null, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaView, PermissionCodes.NalvaManage);
        var query = _db.MarketingContentItems.AsNoTracking().AsQueryable();
        if (campaignId is not null) query = query.Where(x => x.CampaignId == campaignId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.ApprovalStatus == status);
        var rows = await query.OrderByDescending(x => x.SuggestedPublishAt ?? x.CreatedAt).Take(200).ToListAsync(cancellationToken);
        return rows.Select(MapContent).ToList();
    }

    public async Task<MarketingContentItemDto> GetContentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaView, PermissionCodes.NalvaManage);
        return MapContent(await RequireContentAsync(id, cancellationToken));
    }

    public async Task<MarketingCalendarDto> GetCalendarAsync(DateTime from, DateTime to, string? view = null, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaView, PermissionCodes.NalvaManage);
        if (to < from) (from, to) = (to, from);
        var rows = await _db.MarketingContentItems.AsNoTracking()
            .Where(x => (x.SuggestedPublishAt ?? x.ScheduledPublishAt ?? x.CreatedAt) >= from
                && (x.SuggestedPublishAt ?? x.ScheduledPublishAt ?? x.CreatedAt) <= to)
            .OrderBy(x => x.SuggestedPublishAt ?? x.ScheduledPublishAt ?? x.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);
        return new MarketingCalendarDto(from, to, string.IsNullOrWhiteSpace(view) ? "week" : view, rows.Select(MapContent).ToList());
    }

    public async Task<MarketingContentItemDto> SubmitForReviewAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaManage);
        var item = await RequireContentAsync(id, cancellationToken);
        item.ApprovalStatus = "Review";
        item.ApprovalFingerprint = Fingerprint(item);
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapContent(item);
    }

    public async Task<MarketingContentItemDto> ApproveContentAsync(Guid id, string? expectedFingerprint = null, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaApprove, PermissionCodes.NalvaManage);
        var item = await RequireContentAsync(id, cancellationToken);
        var current = Fingerprint(item);
        if (!string.IsNullOrWhiteSpace(expectedFingerprint) && !string.Equals(expectedFingerprint, current, StringComparison.Ordinal))
            throw new BadRequestException("Content changed since review. Re-submit for approval.");
        if (!string.IsNullOrWhiteSpace(item.ApprovalFingerprint) && !string.Equals(item.ApprovalFingerprint, current, StringComparison.Ordinal))
            throw new BadRequestException("Material edits cleared approval. Submit for review again.");
        item.ApprovalFingerprint = current;
        item.ApprovalStatus = "Approved";
        item.ApprovedAt = DateTime.UtcNow;
        item.ApprovedByUserId = Ops.User(_user);
        item.RejectedReason = null;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapContent(item);
    }

    public async Task<MarketingContentItemDto> RejectContentAsync(Guid id, RejectContentRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaApprove, PermissionCodes.NalvaManage);
        var item = await RequireContentAsync(id, cancellationToken);
        item.ApprovalStatus = "Rejected";
        item.RejectedReason = TrimOrNull(request.Reason, 2000) ?? "Rejected";
        item.ApprovedAt = null;
        item.ApprovedByUserId = null;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapContent(item);
    }

    public async Task<MarketingPublishJobDto> ScheduleApprovedContentAsync(Guid id, ScheduleContentRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaManage);
        var policy = await EnsurePolicyAsync(cancellationToken);
        if (policy.IsPaused)
            throw new BadRequestException(policy.PausedReason ?? "Marketing agent is paused.");

        var item = await RequireContentAsync(id, cancellationToken);
        if (item.ApprovalStatus != "Approved" && item.ApprovalStatus != "Scheduled")
            throw new BadRequestException("Only approved content can be scheduled.");
        var fingerprint = Fingerprint(item);
        if (!string.Equals(item.ApprovalFingerprint, fingerprint, StringComparison.Ordinal))
            throw new BadRequestException("Content changed after approval. Approve again before scheduling.");

        var runAt = request.RunAtUtc ?? item.ScheduledPublishAt ?? DateTime.UtcNow;
        // Account/schedule binding may be completed at schedule time when blank at approval.
        if (!string.IsNullOrWhiteSpace(request.ConnectedAccountProvider))
            item.ConnectedAccountProvider = request.ConnectedAccountProvider.Trim();
        if (!string.IsNullOrWhiteSpace(request.ConnectedAccountExternalId))
            item.ConnectedAccountExternalId = request.ConnectedAccountExternalId.Trim();
        item.ScheduledPublishAt = runAt;
        item.ApprovalFingerprint = Fingerprint(item);

        var provider = string.IsNullOrWhiteSpace(item.ConnectedAccountProvider) ? "sandbox" : item.ConnectedAccountProvider!;
        var account = item.ConnectedAccountExternalId ?? "sandbox";
        var idempotency = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"publish:{item.Id:N}:{runAt:yyyyMMddHHmm}"
            : request.IdempotencyKey.Trim();

        var existing = await _db.MarketingPublishJobs.FirstOrDefaultAsync(x => x.IdempotencyKey == idempotency, cancellationToken);
        if (existing is not null)
            return MapJob(existing);

        var today = DateTime.UtcNow.Date;
        var publishedToday = await _db.MarketingPublishJobs.CountAsync(x =>
            x.CreatedAt >= today && (x.Status == "Pending" || x.Status == "Running" || x.Status == "Succeeded"), cancellationToken);
        if (publishedToday >= policy.DailyPublishLimit)
            throw new BadRequestException("Daily publish limit reached.");

        var job = new MarketingPublishJob
        {
            TenantId = TenantId,
            ContentItemId = item.Id,
            Provider = provider,
            AccountExternalId = account,
            RunAtUtc = runAt,
            Status = "Pending",
            IdempotencyKey = idempotency,
            MaxAttempts = 3
        };
        _db.MarketingPublishJobs.Add(job);
        item.ApprovalStatus = "Scheduled";
        item.ScheduledPublishAt = runAt;
        item.IdempotencyKey = idempotency;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapJob(job);
    }

    public async Task<MarketingContentAssetDto> GenerateGraphicAsync(GenerateGraphicRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaManage);
        var brand = await EnsureBrandTrackedAsync(cancellationToken);
        var (width, height) = PlatformSize(request.Platform);
        var colors = brand.BrandColorsJson;
        var prompt = request.Prompt
            ?? $"Brand-safe graphic for {brand.BusinessName}. Use colors {colors}. Industry: {brand.Industry}. Do not invent logos. {request.Brief}".Trim();
        var brief = request.Brief
            ?? $"Visual brief for {brand.BusinessName}: emphasize brand colors {colors}, audience {brand.TargetAudience}, avoid fabricated product claims or fake logos.";

        byte[]? bytes = null;
        string source = "BriefOnly";
        string? mime = null;
        string? path = null;
        if (_images?.IsConfigured == true)
        {
            var generated = await _images.GenerateAsync(prompt, width, height, cancellationToken);
            if (generated is not null)
            {
                bytes = generated.Bytes;
                mime = generated.MimeType;
                source = "OpenAiImage";
            }
        }

        if (bytes is not null)
        {
            var root = Path.Combine(Path.GetTempPath(), "businessos-marketing", TenantId.ToString("N"));
            Directory.CreateDirectory(root);
            var fileName = $"{Guid.NewGuid():N}.png";
            path = Path.Combine(root, fileName);
            await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        }

        var asset = new MarketingContentAsset
        {
            TenantId = TenantId,
            ContentItemId = request.ContentItemId,
            CampaignId = request.CampaignId,
            Kind = source == "BriefOnly" ? "Brief" : "Graphic",
            Platform = request.Platform,
            Width = width,
            Height = height,
            Prompt = prompt,
            Brief = brief,
            StoragePath = path,
            StorageUrl = path is null ? null : $"file://{path}",
            MimeType = mime,
            Source = source,
            BrandCompliant = true,
            Status = "Ready"
        };
        _db.MarketingContentAssets.Add(asset);

        if (request.ContentItemId is Guid contentId)
        {
            var content = await _db.MarketingContentItems.FirstOrDefaultAsync(x => x.Id == contentId, cancellationToken);
            if (content is not null)
            {
                var ids = ParseGuidArray(content.ImageAssetIdsJson);
                ids.Add(asset.Id);
                content.ImageAssetIdsJson = JsonSerializer.Serialize(ids);
                content.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return MapAsset(asset);
    }

    public async Task<IReadOnlyList<MarketingContentAssetDto>> ListAssetsAsync(Guid? contentItemId = null, Guid? campaignId = null, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaView, PermissionCodes.NalvaManage);
        var query = _db.MarketingContentAssets.AsNoTracking().AsQueryable();
        if (contentItemId is not null) query = query.Where(x => x.ContentItemId == contentItemId);
        if (campaignId is not null) query = query.Where(x => x.CampaignId == campaignId);
        var rows = await query.OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(cancellationToken);
        return rows.Select(MapAsset).ToList();
    }

    public async Task<MarketingResearchReportDto> CreateResearchReportAsync(CreateResearchReportRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaManage);
        var brand = await EnsureBrandTrackedAsync(cancellationToken);
        var kind = string.IsNullOrWhiteSpace(request.Kind) ? "Competitor" : request.Kind.Trim();
        var now = DateTime.UtcNow;
        var sources = new List<object>();
        var competitorNames = ParseStringArray(brand.CompetitorsJson);
        var themes = new List<string>();
        var keywords = new List<string>();
        var opportunities = new List<string>();

        var knowledge = await _db.NalvaKnowledgeSources.AsNoTracking()
            .Where(x => x.Title.Contains("competitor") || x.Title.Contains("market") || (request.Notes != null && x.Title.Contains(request.Notes)))
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var k in knowledge)
        {
            sources.Add(new { url = (string?)null, title = k.Title, retrievedAt = k.CreatedAt });
            themes.Add(k.Title);
        }

        var segmentCount = await _db.CustomerSegments.CountAsync(cancellationToken);
        var customerCount = await _db.Customers.CountAsync(cancellationToken);
        sources.Add(new { url = (string?)null, title = "Company CRM segments and customers", retrievedAt = now });
        if (!string.IsNullOrWhiteSpace(request.Notes))
            sources.Add(new { url = (string?)null, title = "Owner notes", retrievedAt = now });

        string body;
        if (string.Equals(kind, "Competitor", StringComparison.OrdinalIgnoreCase))
        {
            body = competitorNames.Count == 0
                ? "No competitor names are stored on the brand profile. Add competitors in the brand profile or knowledge base. No competitor analytics were invented."
                : $"Competitor notes for {string.Join(", ", competitorNames)} based only on brand profile and authorized knowledge. No open-web scrape and no fabricated market share or engagement metrics.";
            if (knowledge.Count > 0)
                body += " Knowledge titles: " + string.Join("; ", knowledge.Select(x => x.Title));
        }
        else if (string.Equals(kind, "Market", StringComparison.OrdinalIgnoreCase))
        {
            body = $"Market note from company records only: {customerCount} customers and {segmentCount} segments. External market feeds are not connected. Industry on profile: {brand.Industry ?? "unspecified"}.";
            keywords.AddRange(new[] { brand.Industry ?? "business", brand.BusinessName }.Where(x => !string.IsNullOrWhiteSpace(x))!);
        }
        else if (string.Equals(kind, "Keywords", StringComparison.OrdinalIgnoreCase))
        {
            keywords.AddRange(ParseStringArray(brand.ProductsServicesJson).Take(10));
            if (!string.IsNullOrWhiteSpace(brand.Industry)) keywords.Add(brand.Industry);
            body = keywords.Count == 0
                ? "No product/service keywords are stored yet. Search volumes were not invented."
                : "Keyword candidates from stored products/services and industry. Search volumes and rankings are unavailable without Search Console.";
        }
        else
        {
            opportunities.Add("Approve and schedule content already drafted in Marketing.");
            if (segmentCount > 0) opportunities.Add("Reuse saved customer segments for targeting drafts.");
            body = "Opportunity list derived from stored company data only. No invented competitor gaps or paid-media forecasts.";
        }

        if (!string.IsNullOrWhiteSpace(request.Notes))
            body += " Owner notes: " + request.Notes.Trim();

        var report = new MarketingResearchReport
        {
            TenantId = TenantId,
            Kind = kind,
            Title = string.IsNullOrWhiteSpace(request.Title) ? $"{kind} research" : request.Title.Trim(),
            Body = body,
            SourcesJson = JsonSerializer.Serialize(sources),
            CompetitorNamesJson = JsonSerializer.Serialize(competitorNames),
            ThemesJson = JsonSerializer.Serialize(themes),
            KeywordsJson = JsonSerializer.Serialize(keywords),
            OpportunitiesJson = JsonSerializer.Serialize(opportunities),
            GeneratedAt = now,
            AgentKey = request.AgentKey ?? "marketing",
            Fabricated = false
        };
        _db.MarketingResearchReports.Add(report);
        await _db.SaveChangesAsync(cancellationToken);
        return MapResearch(report);
    }

    public async Task<IReadOnlyList<MarketingResearchReportDto>> ListResearchAsync(string? kind = null, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaView, PermissionCodes.NalvaManage);
        var query = _db.MarketingResearchReports.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(kind)) query = query.Where(x => x.Kind == kind);
        var rows = await query.OrderByDescending(x => x.GeneratedAt).Take(50).ToListAsync(cancellationToken);
        return rows.Select(MapResearch).ToList();
    }

    public async Task<MarketingOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaView, PermissionCodes.NalvaManage);
        var campaigns = await _db.MarketingCampaigns.CountAsync(cancellationToken);
        var active = await _db.MarketingCampaigns.CountAsync(x => x.Status == "Active", cancellationToken);
        var drafts = await _db.MarketingContentItems.CountAsync(x => x.ApprovalStatus == "Draft", cancellationToken);
        var review = await _db.MarketingContentItems.CountAsync(x => x.ApprovalStatus == "Review", cancellationToken);
        var approved = await _db.MarketingContentItems.CountAsync(x => x.ApprovalStatus == "Approved", cancellationToken);
        var scheduled = await _db.MarketingContentItems.CountAsync(x => x.ApprovalStatus == "Scheduled", cancellationToken);
        var published = await _db.MarketingContentItems.CountAsync(x => x.ApprovalStatus == "Published", cancellationToken);
        var research = await _db.MarketingResearchReports.CountAsync(cancellationToken);
        var pending = await _db.MarketingPublishJobs.CountAsync(x => x.Status == "Pending", cancellationToken);
        var snaps = await _db.MarketingAnalyticsSnapshots.CountAsync(cancellationToken);
        var hasBrand = await _db.MarketingBrandProfiles.AnyAsync(cancellationToken);
        return new MarketingOverviewDto(campaigns, active, drafts, review, approved, scheduled, published, research, pending, snaps, hasBrand, "company-records");
    }

    public async Task<IReadOnlyList<MarketingConnectedAccountDto>> GetConnectedMarketingAccountsAsync(CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaView, PermissionCodes.NalvaManage);
        var result = new List<MarketingConnectedAccountDto>
        {
            new("instagram", null, "Partial", null, null, false, "Instagram Content Publishing requires Meta connection metadata instagramBusinessAccountId plus instagram_content_publish. Image posts only."),
            new("tiktok", null, "Unavailable", null, null, false, "TikTok provider is not present."),
            new("youtube", null, "Unavailable", null, null, false, "YouTube provider is not present.")
        };

        if (_integrations is null)
        {
            result.Insert(0, new("meta", null, "NotConnected", null, null, false, "Integration platform unavailable in this context."));
            result.Insert(1, new("linkedin", null, "NotConnected", null, null, false, "LinkedIn write publish is blocked until write scopes are verified."));
            return result;
        }

        var connections = await _integrations.GetConnectionsAsync(cancellationToken);
        foreach (var provider in new[] { "meta", "linkedin" })
        {
            var connection = connections.FirstOrDefault(x => string.Equals(x.ProviderKey, provider, StringComparison.OrdinalIgnoreCase) && x.Status == "Connected");
            var publish = provider == "meta" && connection is not null;
            var notes = provider == "meta"
                ? (publish ? "Facebook Page feed publish available when pageId metadata is set." : "Connect Meta and set pageId metadata to publish.")
                : "LinkedIn organization read may be available; page publish write ops are not enabled.";
            result.Insert(provider == "meta" ? 0 : 1, new(
                provider,
                connection?.Id.ToString(),
                connection?.Status ?? "NotConnected",
                null,
                connection?.ExternalAccountLabel,
                publish,
                notes));
        }

        return result;
    }

    public async Task<MarketingSyncAnalyticsResultDto> SyncAnalyticsAsync(SyncAnalyticsRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaManage, PermissionCodes.NalvaView);
        var snapshots = new List<MarketingAnalyticsSnapshot>();
        var items = await _db.MarketingContentItems
            .Where(x => x.ApprovalStatus == "Published" && (request.ContentItemId == null || x.Id == request.ContentItemId)
                && (request.CampaignId == null || x.CampaignId == request.CampaignId))
            .Take(50)
            .ToListAsync(cancellationToken);

        if (items.Count == 0 && request.AllowSandbox)
        {
            var sandbox = new MarketingAnalyticsSnapshot
            {
                TenantId = TenantId,
                ContentItemId = request.ContentItemId,
                CampaignId = request.CampaignId,
                Provider = "sandbox",
                MetricsJson = null,
                SyncedAt = DateTime.UtcNow,
                DataSource = "Sandbox",
                UnavailableMetricsJson = JsonSerializer.Serialize(new[] { "impressions", "clicks", "engagement", "reach" })
            };
            _db.MarketingAnalyticsSnapshots.Add(sandbox);
            snapshots.Add(sandbox);
            await _db.SaveChangesAsync(cancellationToken);
            return new MarketingSyncAnalyticsResultDto(1, "Sandbox", "No published posts with platform metrics. Sandbox unavailable-metrics snapshot recorded.", snapshots.Select(MapSnap).ToList());
        }

        foreach (var item in items)
        {
            if (string.Equals(item.ConnectedAccountProvider, "meta", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(item.ExternalPostId)
                && _integrations is not null)
            {
                var args = new JsonObject { ["externalPostId"] = item.ExternalPostId }.ToJsonString();
                var call = await _integrations.ExecuteAsync("meta", "insights", args, false, cancellationToken);
                if (call.Performed)
                {
                    var snap = new MarketingAnalyticsSnapshot
                    {
                        TenantId = TenantId,
                        ContentItemId = item.Id,
                        CampaignId = item.CampaignId,
                        Provider = "meta",
                        ExternalPostId = item.ExternalPostId,
                        MetricsJson = call.Data?.ToJsonString(),
                        SyncedAt = DateTime.UtcNow,
                        DataSource = "Meta",
                        UnavailableMetricsJson = "[]"
                    };
                    _db.MarketingAnalyticsSnapshots.Add(snap);
                    snapshots.Add(snap);
                    continue;
                }
            }

            if (string.Equals(item.ConnectedAccountProvider, "sandbox", StringComparison.OrdinalIgnoreCase) || request.AllowSandbox)
            {
                var snap = new MarketingAnalyticsSnapshot
                {
                    TenantId = TenantId,
                    ContentItemId = item.Id,
                    CampaignId = item.CampaignId,
                    Provider = item.ConnectedAccountProvider ?? "sandbox",
                    ExternalPostId = item.ExternalPostId,
                    MetricsJson = null,
                    SyncedAt = DateTime.UtcNow,
                    DataSource = "Sandbox",
                    UnavailableMetricsJson = JsonSerializer.Serialize(new[] { "impressions", "clicks", "engagement" })
                };
                _db.MarketingAnalyticsSnapshots.Add(snap);
                snapshots.Add(snap);
            }
            else
            {
                var snap = new MarketingAnalyticsSnapshot
                {
                    TenantId = TenantId,
                    ContentItemId = item.Id,
                    CampaignId = item.CampaignId,
                    Provider = item.ConnectedAccountProvider ?? "unknown",
                    ExternalPostId = item.ExternalPostId,
                    MetricsJson = null,
                    SyncedAt = DateTime.UtcNow,
                    DataSource = "Unavailable",
                    UnavailableMetricsJson = JsonSerializer.Serialize(new[] { "impressions", "clicks", "engagement", "reach" })
                };
                _db.MarketingAnalyticsSnapshots.Add(snap);
                snapshots.Add(snap);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        var source = snapshots.Select(x => x.DataSource).Distinct().FirstOrDefault() ?? "Unavailable";
        return new MarketingSyncAnalyticsResultDto(snapshots.Count, source, "Synced verified or explicitly unavailable metrics only. No invented engagement numbers.", snapshots.Select(MapSnap).ToList());
    }

    public async Task<MarketingPerformanceReportDto> GeneratePerformanceReportAsync(Guid? campaignId = null, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaView, PermissionCodes.NalvaManage);
        var contentQuery = _db.MarketingContentItems.AsNoTracking().AsQueryable();
        if (campaignId is not null) contentQuery = contentQuery.Where(x => x.CampaignId == campaignId);
        var published = await contentQuery.CountAsync(x => x.ApprovalStatus == "Published", cancellationToken);
        var scheduled = await contentQuery.CountAsync(x => x.ApprovalStatus == "Scheduled", cancellationToken);
        var drafts = await contentQuery.CountAsync(x => x.ApprovalStatus == "Draft" || x.ApprovalStatus == "Review", cancellationToken);
        var snapQuery = _db.MarketingAnalyticsSnapshots.AsNoTracking().AsQueryable();
        if (campaignId is not null) snapQuery = snapQuery.Where(x => x.CampaignId == campaignId);
        var snaps = await snapQuery.OrderByDescending(x => x.SyncedAt).Take(20).ToListAsync(cancellationToken);
        var verified = snaps.Where(x => x.MetricsJson is not null).Select(x => new { x.Id, x.DataSource, x.ExternalPostId, x.SyncedAt }).ToList();
        var unavailable = snaps.SelectMany(x => ParseStringArray(x.UnavailableMetricsJson)).Distinct().ToList();
        var sources = snaps.Select(x => x.DataSource).Distinct().ToList();
        if (sources.Count == 0) sources.Add("company-records");

        var body = $"Verified performance from company marketing records: {published} published, {scheduled} scheduled, {drafts} drafts/in-review. "
            + $"{verified.Count} analytics snapshots include provider metrics. "
            + "Missing metrics are listed as unavailable — values were not invented.";

        return new MarketingPerformanceReportDto(
            campaignId is null ? "Marketing performance" : $"Campaign {campaignId} performance",
            body,
            JsonSerializer.Serialize(new { published, scheduled, drafts, snapshotsWithMetrics = verified }),
            JsonSerializer.Serialize(unavailable),
            JsonSerializer.Serialize(sources),
            DateTime.UtcNow,
            false);
    }

    public async Task<MarketingAgentPolicyDto> GetPolicyAsync(CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaView, PermissionCodes.NalvaManage);
        return MapPolicy(await EnsurePolicyAsync(cancellationToken));
    }

    public async Task<MarketingAgentPolicyDto> SavePolicyAsync(MarketingPolicyWriteRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaManage);
        var policy = await EnsurePolicyAsync(cancellationToken);
        if (request.RequireApprovalBeforePublish is not null) policy.RequireApprovalBeforePublish = request.RequireApprovalBeforePublish.Value;
        if (request.DailyPublishLimit is not null) policy.DailyPublishLimit = Math.Clamp(request.DailyPublishLimit.Value, 0, 100);
        if (request.WeeklyAiBudgetUsd is not null) policy.WeeklyAiBudgetUsd = Math.Max(0, request.WeeklyAiBudgetUsd.Value);
        if (request.MonthlyAiBudgetUsd is not null) policy.MonthlyAiBudgetUsd = Math.Max(0, request.MonthlyAiBudgetUsd.Value);
        if (!string.IsNullOrWhiteSpace(request.TimeZoneId)) policy.TimeZoneId = request.TimeZoneId.Trim();
        if (request.DailyCronEnabled is not null) policy.DailyCronEnabled = request.DailyCronEnabled.Value;
        if (request.WeeklyCronEnabled is not null) policy.WeeklyCronEnabled = request.WeeklyCronEnabled.Value;
        if (request.MonthlyCronEnabled is not null) policy.MonthlyCronEnabled = request.MonthlyCronEnabled.Value;
        if (request.IsPaused is not null) policy.IsPaused = request.IsPaused.Value;
        if (request.PausedReason is not null) policy.PausedReason = TrimOrNull(request.PausedReason, 1000);
        policy.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapPolicy(policy);
    }

    public async Task<MarketingPublishProcessResultDto> ProcessDuePublishJobsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var due = await _db.MarketingPublishJobs
            .Where(x => x.Status == "Pending" && x.RunAtUtc <= now && x.Attempt < x.MaxAttempts)
            .OrderBy(x => x.RunAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        var claimed = 0;
        var succeeded = 0;
        var failed = 0;
        foreach (var job in due)
        {
            var token = Guid.NewGuid().ToString("N");
            job.Status = "Running";
            job.ClaimToken = token;
            job.StartedAt = now;
            job.Attempt += 1;
            claimed++;
            await _db.SaveChangesAsync(cancellationToken);

            var content = await _db.MarketingContentItems.FirstOrDefaultAsync(x => x.Id == job.ContentItemId, cancellationToken);
            if (content is null)
            {
                job.Status = "Failed";
                job.LastError = "Content item missing.";
                job.CompletedAt = DateTime.UtcNow;
                failed++;
                await _db.SaveChangesAsync(cancellationToken);
                continue;
            }

            if (!string.Equals(content.ApprovalFingerprint, Fingerprint(content), StringComparison.Ordinal))
            {
                job.Status = "Failed";
                job.LastError = "Approval fingerprint mismatch.";
                content.ApprovalStatus = "Failed";
                content.LastError = job.LastError;
                job.CompletedAt = DateTime.UtcNow;
                failed++;
                await _db.SaveChangesAsync(cancellationToken);
                continue;
            }

            var transport = ResolveTransport(job.Provider);
            var message = content.Caption ?? content.Body;
            var result = await transport.PublishAsync(new MarketingPublishTransportRequest(
                TenantId, content.Id, job.Id, job.IdempotencyKey, message, content.Caption, job.AccountExternalId, null), cancellationToken);

            if (result.Succeeded)
            {
                job.Status = "Succeeded";
                job.ExternalPostId = result.ExternalPostId;
                job.CompletedAt = DateTime.UtcNow;
                job.LastError = null;
                content.ApprovalStatus = "Published";
                content.ExternalPostId = result.ExternalPostId;
                content.ConnectedAccountProvider = job.Provider;
                content.LastError = null;
                succeeded++;
            }
            else
            {
                job.Status = job.Attempt >= job.MaxAttempts ? "Failed" : "Pending";
                job.LastError = result.Error ?? "Publish failed.";
                job.ClaimToken = null;
                if (job.Status == "Failed")
                {
                    job.CompletedAt = DateTime.UtcNow;
                    content.ApprovalStatus = "Failed";
                    content.LastError = job.LastError;
                    failed++;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        return new MarketingPublishProcessResultDto(claimed, succeeded, failed, 0);
    }

    public async Task RunRecurringMaintenanceAsync(CancellationToken cancellationToken = default)
    {
        var policy = await EnsurePolicyAsync(cancellationToken);
        if (policy.IsPaused)
            return;

        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var publishedToday = await _db.MarketingPublishJobs.CountAsync(x =>
            x.CreatedAt >= today && (x.Status == "Pending" || x.Status == "Running" || x.Status == "Succeeded"), cancellationToken);
        if (publishedToday >= policy.DailyPublishLimit)
        {
            policy.IsPaused = true;
            policy.PausedReason = $"Daily publish limit ({policy.DailyPublishLimit}) reached. Maintenance paused; nothing was auto-published.";
            policy.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var monthAiSpend = await _db.MarketingCampaigns
            .Where(x => x.CreatedAt >= monthStart)
            .SumAsync(x => (decimal?)x.EstimatedAiCostUsd, cancellationToken) ?? 0m;
        if (policy.MonthlyAiBudgetUsd > 0 && monthAiSpend >= policy.MonthlyAiBudgetUsd)
        {
            policy.IsPaused = true;
            policy.PausedReason = $"Monthly AI budget (${policy.MonthlyAiBudgetUsd}) reached (recorded estimate ${monthAiSpend}). Maintenance paused; nothing was auto-published.";
            policy.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (policy.DailyCronEnabled)
        {
            var failedJobs = await _db.MarketingPublishJobs.CountAsync(x => x.Status == "Failed", cancellationToken);
            var upcoming = await _db.MarketingContentItems.CountAsync(x =>
                x.ApprovalStatus == "Scheduled"
                && x.ScheduledPublishAt != null
                && x.ScheduledPublishAt >= DateTime.UtcNow, cancellationToken);
            _db.NalvaWorkItems.Add(new NalvaWorkItem
            {
                TenantId = TenantId,
                AgentKey = "marketing",
                Kind = "MarketingMaintenance",
                Title = "Daily marketing maintenance counts",
                Body = JsonSerializer.Serialize(new
                {
                    failedPublishJobs = failedJobs,
                    upcomingScheduledContent = upcoming,
                    publishedOrQueuedToday = publishedToday,
                    dailyPublishLimit = policy.DailyPublishLimit,
                    fabricated = false,
                    autoPublished = false
                }),
                Status = "Open"
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        if (policy.WeeklyCronEnabled)
        {
            var brand = await EnsureBrandTrackedAsync(cancellationToken);
            var activeCampaign = await _db.MarketingCampaigns
                .Where(x => x.Status == "Active" || x.Status == "Draft")
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (activeCampaign is null)
            {
                activeCampaign = new MarketingCampaign
                {
                    TenantId = TenantId,
                    Title = $"{brand.BusinessName} weekly draft",
                    Objective = "BrandAwareness",
                    ContentThemesJson = "[]",
                    RecommendedPlatformsJson = JsonSerializer.Serialize(DefaultPlatforms),
                    ContentFormatsJson = "[\"Post\"]",
                    SuccessMetricsJson = "[]",
                    Status = "Draft",
                    StrategyBody = "Weekly maintenance draft. Strategy not auto-approved. Nothing was published.",
                    AgentKey = "marketing"
                };
                _db.MarketingCampaigns.Add(activeCampaign);
                await _db.SaveChangesAsync(cancellationToken);
            }

            var weekStart = today;
            var weekEnd = today.AddDays(7);
            var calendarCount = await _db.MarketingContentItems.CountAsync(x =>
                x.CampaignId == activeCampaign.Id
                && x.SuggestedPublishAt != null
                && x.SuggestedPublishAt >= weekStart
                && x.SuggestedPublishAt < weekEnd, cancellationToken);
            if (calendarCount == 0)
            {
                await GenerateContentCalendarAsync(new GenerateContentCalendarRequest
                {
                    CampaignId = activeCampaign.Id,
                    Days = 7,
                    StartDate = weekStart,
                    AgentKey = "marketing"
                }, cancellationToken);
            }
        }

        if (policy.MonthlyCronEnabled)
        {
            var activeIds = await _db.MarketingCampaigns.AsNoTracking()
                .Where(x => x.Status == "Active" || x.Status == "Draft")
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => x.Id)
                .Take(10)
                .ToListAsync(cancellationToken);
            foreach (var campaignId in activeIds)
            {
                var report = await GeneratePerformanceReportAsync(campaignId, cancellationToken);
                _db.NalvaWorkItems.Add(new NalvaWorkItem
                {
                    TenantId = TenantId,
                    AgentKey = "marketing",
                    Kind = "MarketingPerformance",
                    Title = report.Title,
                    Body = report.Body + "\n" + report.VerifiedMetricsJson,
                    Status = "Open",
                    RelatedEntityType = "MarketingCampaign",
                    RelatedEntityId = campaignId
                });
            }
            if (activeIds.Count > 0)
                await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<MarketingContentItemDto?> ShareInsightsWithSalesAsync(Guid? campaignId = null, string? agentKey = null, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.NalvaManage);
        var overview = await GetOverviewAsync(cancellationToken);
        var report = await GeneratePerformanceReportAsync(campaignId, cancellationToken);
        var payload = new JsonObject
        {
            ["kind"] = "MarketingInsight",
            ["campaignId"] = campaignId?.ToString(),
            ["overview"] = JsonNode.Parse(JsonSerializer.Serialize(overview)),
            ["performance"] = JsonNode.Parse(JsonSerializer.Serialize(report)),
            ["fabricated"] = false
        };

        var work = new NalvaWorkItem
        {
            TenantId = TenantId,
            Kind = "MarketingInsight",
            Title = "Marketing insights for sales",
            Body = payload.ToJsonString(),
            Status = "Open",
            AgentKey = agentKey ?? "marketing"
        };
        _db.NalvaWorkItems.Add(work);
        await _db.SaveChangesAsync(cancellationToken);
        return null;
    }

    private IMarketingPublishTransport ResolveTransport(string provider)
    {
        var match = _transports.FirstOrDefault(x => string.Equals(x.ProviderKey, provider, StringComparison.OrdinalIgnoreCase));
        if (match is not null) return match;
        var sandbox = _transports.FirstOrDefault(x => x.IsSandbox);
        if (sandbox is not null) return sandbox;
        throw new InvalidOperationException($"No publish transport registered for '{provider}'.");
    }

    private async Task<MarketingBrandProfile> SeedBrandAsync(CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Tenant not found.");
        var storefront = await _db.CompanyStorefronts.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var offerings = await _db.StorefrontOfferings.AsNoTracking()
            .Where(x => x.IsPublished)
            .OrderBy(x => x.SortOrder)
            .Take(20)
            .Select(x => new { x.Name, x.Summary, x.PricingModel, x.Price })
            .ToListAsync(cancellationToken);

        var products = offerings.Select(o => new
        {
            name = o.Name,
            summary = o.Summary,
            pricingModel = o.PricingModel,
            price = o.Price
        }).ToList();

        var colors = new List<string>();
        if (!string.IsNullOrWhiteSpace(storefront?.BrandColor))
            colors.Add(storefront!.BrandColor);

        var sources = new List<string> { "Tenant" };
        if (storefront is not null) sources.Add("CompanyStorefront");
        if (products.Count > 0) sources.Add("StorefrontOfferings");

        var profile = new MarketingBrandProfile
        {
            TenantId = TenantId,
            BusinessName = tenant.Name,
            Industry = storefront?.Industry ?? tenant.BusinessType,
            ProductsServicesJson = JsonSerializer.Serialize(products),
            TargetAudience = null,
            BrandDescription = storefront?.PublicDescription ?? tenant.Description,
            BrandVoice = null,
            BrandColorsJson = JsonSerializer.Serialize(colors),
            LogoUrl = tenant.LogoUrl,
            Website = storefront?.Website ?? tenant.Website,
            SocialAccountsJson = storefront?.SocialLinksJson ?? tenant.SocialLinks ?? "{}",
            MarketingObjectivesJson = "[]",
            PreferredLanguagesJson = "[\"en\"]",
            ContentRestrictions = null,
            CompetitorsJson = "[]",
            MarketingPreferencesJson = "{}",
            SourceNotes = "Seeded from " + string.Join(", ", sources) + ". Prices/features only copied when present on offerings; nothing invented."
        };
        _db.MarketingBrandProfiles.Add(profile);
        await _db.SaveChangesAsync(cancellationToken);
        return profile;
    }

    private async Task<MarketingBrandProfile> EnsureBrandTrackedAsync(CancellationToken cancellationToken)
    {
        var row = await _db.MarketingBrandProfiles.FirstOrDefaultAsync(cancellationToken);
        if (row is not null) return row;
        await SeedBrandAsync(cancellationToken);
        return await _db.MarketingBrandProfiles.FirstAsync(cancellationToken);
    }

    private async Task<MarketingAgentPolicy> EnsurePolicyAsync(CancellationToken cancellationToken)
    {
        var policy = await _db.MarketingAgentPolicies.FirstOrDefaultAsync(cancellationToken);
        if (policy is not null) return policy;
        policy = new MarketingAgentPolicy { TenantId = TenantId };
        _db.MarketingAgentPolicies.Add(policy);
        await _db.SaveChangesAsync(cancellationToken);
        return policy;
    }

    private async Task<MarketingCampaign> RequireCampaignAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.MarketingCampaigns.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Campaign not found.");

    private async Task<MarketingContentItem> RequireContentAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.MarketingContentItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Content item not found.");

    private async Task<string> BuildStrategyBodyAsync(MarketingBrandProfile brand, string objective, int segments, int customers, CancellationToken cancellationToken)
    {
        var facts = $"Business: {brand.BusinessName}. Industry: {brand.Industry}. Audience: {brand.TargetAudience}. "
            + $"Products/services JSON: {brand.ProductsServicesJson}. Objectives JSON: {brand.MarketingObjectivesJson}. "
            + $"Stored segments: {segments}. Stored customers: {customers}. Restrictions: {brand.ContentRestrictions}. "
            + "Use only these facts. Do not invent prices, competitor metrics, or market sizes.";

        if (_llm?.IsConfigured == true)
        {
            try
            {
                var reply = await _llm.GenerateReplyAsync(
                    TenantId,
                    Ops.User(_user),
                    "You write marketing strategy drafts for BusinessOS. Use only supplied brand facts. Never invent metrics, competitor data, or claims.",
                    $"Objective: {objective}. Facts: {facts}",
                    cancellationToken);
                if (!string.IsNullOrWhiteSpace(reply))
                    return reply.Trim();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "LLM strategy generation failed; using template.");
            }
        }

        return $"Strategy draft for {brand.BusinessName} focused on {objective}. "
            + $"Grounded in {customers} stored customers and {segments} segments. "
            + $"Industry: {brand.Industry ?? "unspecified"}. Voice: {brand.BrandVoice ?? "company default"}. "
            + "Recommended next steps: approve strategy, generate a content calendar, draft platform posts, require human approval before publish. "
            + "No external market numbers were invented.";
    }

    private async Task<MarketingContentItem> BuildContentItemAsync(
        MarketingBrandProfile brand,
        Guid? campaignId,
        string platform,
        string contentType,
        string? brief,
        DateTime? suggested,
        string? agentKey,
        Guid? executionId,
        CancellationToken cancellationToken,
        bool save,
        string? title = null)
    {
        var body = await BuildCopyAsync(brand, platform, brief, cancellationToken);
        var caption = body.Length > 500 ? body[..500] : body;
        var item = new MarketingContentItem
        {
            TenantId = TenantId,
            CampaignId = campaignId,
            AssociatedCampaignId = campaignId,
            Title = string.IsNullOrWhiteSpace(title) ? $"{platform} draft for {brand.BusinessName}" : title.Trim(),
            Body = body,
            Platform = NormalizePlatform(platform),
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "Post" : contentType.Trim(),
            Caption = caption,
            HashtagsJson = JsonSerializer.Serialize(BuildHashtags(brand, platform)),
            CallToAction = string.IsNullOrWhiteSpace(brand.Website) ? null : $"Learn more: {brand.Website}",
            SuggestedPublishAt = suggested,
            ApprovalStatus = "Draft",
            AgentKey = agentKey ?? "marketing",
            ExecutionId = executionId,
            ContentHash = HashText(body)
        };
        if (save)
        {
            _db.MarketingContentItems.Add(item);
            await _db.SaveChangesAsync(cancellationToken);
        }
        return item;
    }

    private async Task<string> BuildCopyAsync(MarketingBrandProfile brand, string platform, string? brief, CancellationToken cancellationToken)
    {
        var facts = $"Company {brand.BusinessName}, industry {brand.Industry}, description {brand.BrandDescription}, voice {brand.BrandVoice}, offerings {brand.ProductsServicesJson}, restrictions {brand.ContentRestrictions}.";
        if (_llm?.IsConfigured == true)
        {
            try
            {
                var reply = await _llm.GenerateReplyAsync(
                    TenantId,
                    _user.UserId ?? "system",
                    "Write a short social post using only supplied brand facts. No invented claims, prices, testimonials, or competitor comparisons.",
                    $"Platform: {platform}. Brief: {brief}. Facts: {facts}",
                    cancellationToken);
                if (!string.IsNullOrWhiteSpace(reply))
                    return reply.Trim();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "LLM content generation failed; using template.");
            }
        }

        var templates = new[]
        {
            $"{brand.BusinessName} shares an update for {platform}: {brief ?? brand.BrandDescription ?? "our latest work"}.",
            $"From {brand.BusinessName} ({brand.Industry ?? "our industry"}): {brief ?? "a note for our audience"}.",
            $"{brand.BusinessName} — {brief ?? brand.BrandDescription ?? "built from your brand profile"}. Voice: {brand.BrandVoice ?? "professional"}."
        };
        var pick = templates[Math.Abs(HashCode.Combine(brand.BusinessName, platform, brief)) % templates.Length];
        if (!string.IsNullOrWhiteSpace(brand.ContentRestrictions))
            pick += " Respect content restrictions on the brand profile.";
        return pick;
    }

    private static string Fingerprint(MarketingContentItem item) =>
        HashText($"{item.Caption}|{item.Body}|{item.ImageAssetIdsJson}|{item.ConnectedAccountProvider}|{item.ConnectedAccountExternalId}|{item.ScheduledPublishAt:O}");

    private static string HashText(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private static string NormalizeObjective(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "BrandAwareness";
        var trimmed = value.Trim();
        return Objectives.Contains(trimmed) ? Objectives.First(x => x.Equals(trimmed, StringComparison.OrdinalIgnoreCase)) : "BrandAwareness";
    }

    private static string NormalizePlatform(string platform) =>
        platform.Trim() switch
        {
            var p when p.Equals("linkedin", StringComparison.OrdinalIgnoreCase) => "LinkedIn",
            var p when p.Equals("facebook", StringComparison.OrdinalIgnoreCase) => "Facebook",
            var p when p.Equals("instagram", StringComparison.OrdinalIgnoreCase) => "Instagram",
            var p when p.Equals("youtube", StringComparison.OrdinalIgnoreCase) => "YouTube",
            var p when p.Equals("tiktok", StringComparison.OrdinalIgnoreCase) => "TikTok",
            _ => string.IsNullOrWhiteSpace(platform) ? "Other" : platform.Trim()
        };

    private static (int Width, int Height) PlatformSize(string? platform) =>
        NormalizePlatform(platform ?? "Other") switch
        {
            "Instagram" => (1080, 1080),
            "LinkedIn" => (1200, 627),
            "Facebook" => (1200, 630),
            "YouTube" => (1280, 720),
            "TikTok" => (1080, 1920),
            _ => (1080, 1080)
        };

    private static List<string> BuildHashtags(MarketingBrandProfile brand, string platform)
    {
        var tags = new List<string>();
        if (!string.IsNullOrWhiteSpace(brand.Industry))
            tags.Add("#" + brand.Industry.Replace(" ", "", StringComparison.Ordinal));
        tags.Add("#" + brand.BusinessName.Replace(" ", "", StringComparison.Ordinal));
        tags.Add("#" + platform.Replace(" ", "", StringComparison.Ordinal));
        return tags.Take(5).ToList();
    }

    private static string? TrimOrNull(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var t = value.Trim();
        return t.Length <= max ? t : t[..max];
    }

    private static List<string> ParseStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];
            return doc.RootElement.EnumerateArray()
                .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.ToString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<Guid> ParseGuidArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static MarketingBrandProfileDto MapBrand(MarketingBrandProfile x) => new(
        x.Id, x.BusinessName, x.Industry, x.ProductsServicesJson, x.TargetAudience, x.BrandDescription, x.BrandVoice,
        x.BrandColorsJson, x.LogoUrl, x.Website, x.SocialAccountsJson, x.MarketingObjectivesJson, x.PreferredLanguagesJson,
        x.ContentRestrictions, x.CompetitorsJson, x.MarketingPreferencesJson, x.SourceNotes, x.CreatedAt, x.UpdatedAt);

    private static MarketingCampaignDto MapCampaign(MarketingCampaign x) => new(
        x.Id, x.Title, x.Objective, x.TargetAudience, x.ContentThemesJson, x.RecommendedPlatformsJson, x.PublishingFrequency,
        x.ContentFormatsJson, x.StartDate, x.EndDate, x.SuccessMetricsJson, x.EstimatedAiCostUsd, x.Status, x.StrategyBody,
        x.StrategyApproved, x.StrategyApprovedAt, x.StrategyApprovedByUserId, x.AgentKey, x.CreatedAt);

    private static MarketingContentItemDto MapContent(MarketingContentItem x) => new(
        x.Id, x.CampaignId, x.Title, x.Body, x.Platform, x.ContentType, x.Caption, x.HashtagsJson, x.CallToAction,
        x.SuggestedPublishAt, x.ScheduledPublishAt, x.ImageAssetIdsJson, x.ApprovalStatus, x.ApprovalFingerprint,
        x.ApprovedAt, x.ApprovedByUserId, x.RejectedReason, x.ConnectedAccountProvider, x.ConnectedAccountExternalId,
        x.ExternalPostId, x.LastError, x.IdempotencyKey, x.AgentKey, x.CreatedAt);

    private static MarketingContentAssetDto MapAsset(MarketingContentAsset x) => new(
        x.Id, x.ContentItemId, x.CampaignId, x.Kind, x.Platform, x.Width, x.Height, x.Prompt, x.Brief, x.StorageUrl,
        x.StoragePath, x.MimeType, x.Source, x.BrandCompliant, x.Status, x.CreatedAt);

    private static MarketingResearchReportDto MapResearch(MarketingResearchReport x) => new(
        x.Id, x.Kind, x.Title, x.Body, x.SourcesJson, x.CompetitorNamesJson, x.ThemesJson, x.KeywordsJson,
        x.OpportunitiesJson, x.GeneratedAt, x.AgentKey, x.Fabricated);

    private static MarketingPublishJobDto MapJob(MarketingPublishJob x) => new(
        x.Id, x.ContentItemId, x.Provider, x.AccountExternalId, x.RunAtUtc, x.Status, x.Attempt, x.IdempotencyKey,
        x.LastError, x.ExternalPostId, x.StartedAt, x.CompletedAt);

    private static MarketingAnalyticsSnapshotDto MapSnap(MarketingAnalyticsSnapshot x) => new(
        x.Id, x.ContentItemId, x.CampaignId, x.Provider, x.ExternalPostId, x.MetricsJson, x.SyncedAt, x.DataSource, x.UnavailableMetricsJson);

    private static MarketingAgentPolicyDto MapPolicy(MarketingAgentPolicy x) => new(
        x.Id, x.RequireApprovalBeforePublish, x.DailyPublishLimit, x.WeeklyAiBudgetUsd, x.MonthlyAiBudgetUsd, x.TimeZoneId,
        x.DailyCronEnabled, x.WeeklyCronEnabled, x.MonthlyCronEnabled, x.PausedReason, x.IsPaused);
}
