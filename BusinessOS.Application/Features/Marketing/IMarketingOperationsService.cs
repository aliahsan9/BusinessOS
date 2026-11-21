namespace BusinessOS.Application.Features.Marketing;

public interface IMarketingOperationsService
{
    Task<MarketingBrandProfileDto> GetOrSeedBrandProfileAsync(CancellationToken cancellationToken = default);
    Task<MarketingBrandProfileDto> UpsertBrandProfileAsync(UpsertBrandProfileRequest request, CancellationToken cancellationToken = default);

    Task<MarketingCampaignDto> GenerateStrategyAsync(GenerateStrategyRequest request, CancellationToken cancellationToken = default);
    Task<MarketingCampaignDto> UpdateStrategyAsync(Guid campaignId, UpdateStrategyRequest request, CancellationToken cancellationToken = default);
    Task<MarketingCampaignDto> ApproveStrategyAsync(Guid campaignId, CancellationToken cancellationToken = default);

    Task<MarketingCampaignDto> CreateCampaignAsync(CreateCampaignRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MarketingCampaignDto>> ListCampaignsAsync(string? status = null, CancellationToken cancellationToken = default);
    Task<MarketingCampaignDto> GetCampaignAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MarketingCampaignDto> PauseResumeCampaignAsync(Guid id, bool pause, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MarketingContentItemDto>> GenerateContentCalendarAsync(GenerateContentCalendarRequest request, CancellationToken cancellationToken = default);
    Task<MarketingContentItemDto> GenerateContentItemAsync(GenerateContentItemRequest request, CancellationToken cancellationToken = default);
    Task<MarketingContentItemDto> UpdateContentItemAsync(Guid id, UpdateContentItemRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MarketingContentItemDto>> ListContentAsync(Guid? campaignId = null, string? status = null, CancellationToken cancellationToken = default);
    Task<MarketingContentItemDto> GetContentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MarketingCalendarDto> GetCalendarAsync(DateTime from, DateTime to, string? view = null, CancellationToken cancellationToken = default);

    Task<MarketingContentItemDto> SubmitForReviewAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MarketingContentItemDto> ApproveContentAsync(Guid id, string? expectedFingerprint = null, CancellationToken cancellationToken = default);
    Task<MarketingContentItemDto> RejectContentAsync(Guid id, RejectContentRequest request, CancellationToken cancellationToken = default);
    Task<MarketingPublishJobDto> ScheduleApprovedContentAsync(Guid id, ScheduleContentRequest request, CancellationToken cancellationToken = default);

    Task<MarketingContentAssetDto> GenerateGraphicAsync(GenerateGraphicRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MarketingContentAssetDto>> ListAssetsAsync(Guid? contentItemId = null, Guid? campaignId = null, CancellationToken cancellationToken = default);

    Task<MarketingResearchReportDto> CreateResearchReportAsync(CreateResearchReportRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MarketingResearchReportDto>> ListResearchAsync(string? kind = null, CancellationToken cancellationToken = default);

    Task<MarketingOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MarketingConnectedAccountDto>> GetConnectedMarketingAccountsAsync(CancellationToken cancellationToken = default);
    Task<MarketingSyncAnalyticsResultDto> SyncAnalyticsAsync(SyncAnalyticsRequest request, CancellationToken cancellationToken = default);
    Task<MarketingPerformanceReportDto> GeneratePerformanceReportAsync(Guid? campaignId = null, CancellationToken cancellationToken = default);

    Task<MarketingAgentPolicyDto> GetPolicyAsync(CancellationToken cancellationToken = default);
    Task<MarketingAgentPolicyDto> SavePolicyAsync(MarketingPolicyWriteRequest request, CancellationToken cancellationToken = default);

    Task<MarketingPublishProcessResultDto> ProcessDuePublishJobsAsync(CancellationToken cancellationToken = default);
    Task RunRecurringMaintenanceAsync(CancellationToken cancellationToken = default);

    Task<MarketingContentItemDto?> ShareInsightsWithSalesAsync(Guid? campaignId = null, string? agentKey = null, CancellationToken cancellationToken = default);
}

public interface IMarketingPublishTransport
{
    string ProviderKey { get; }
    bool IsSandbox { get; }
    Task<MarketingPublishTransportResult> PublishAsync(MarketingPublishTransportRequest request, CancellationToken cancellationToken = default);
}

public sealed record MarketingPublishTransportRequest(
    Guid TenantId,
    Guid ContentItemId,
    Guid JobId,
    string IdempotencyKey,
    string Message,
    string? Caption,
    string AccountExternalId,
    string? Link);

public sealed record MarketingPublishTransportResult(
    bool Succeeded,
    string? ExternalPostId,
    string? Error,
    string DataSource);

public interface IMarketingImageClient
{
    bool IsConfigured { get; }
    Task<MarketingImageResult?> GenerateAsync(string prompt, int width, int height, CancellationToken cancellationToken = default);
}

public sealed record MarketingImageResult(
    byte[] Bytes,
    string MimeType,
    string Model);
