using BusinessOS.Application.Common.Models;

namespace BusinessOS.Application.Features.Platform;

public static class PlatformSettingKeys
{
    public const string DefaultModel = "ai.defaultModel";
    public const string EmbeddingModel = "ai.embeddingModel";
    public const string Providers = "ai.providers";
    public const string Features = "ai.features";
    public const string RequestLimit = "ai.globalRequestLimit";

    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        DefaultModel,
        EmbeddingModel,
        Providers,
        Features,
        RequestLimit
    };
}

public class PlatformSessionDto
{
    public bool IsSuperAdmin { get; set; }
}

public class CompanySummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string BusinessType { get; set; } = default!;
    public string Status { get; set; } = default!;
    public bool IsActive { get; set; }
    public string Email { get; set; } = default!;
    public string? Country { get; set; }
    public string SubscriptionPlan { get; set; } = default!;
    public string? SubscriptionStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public int MemberCount { get; set; }
}

public class CompanyProfileDto : CompanySummaryDto
{
    public string Slug { get; set; } = default!;
    public string? Description { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string? Address { get; set; }
    public string Timezone { get; set; } = "UTC";
    public string Currency { get; set; } = "USD";
    public string? RegistrationNumber { get; set; }
    public string? EnabledModulesJson { get; set; }
    public int AiRequestsUsed { get; set; }
    public long StorageUsedMb { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class PlatformSubscriptionRowDto
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = default!;
    public string PlanName { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string BillingInterval { get; set; } = default!;
    public decimal MonthlyPrice { get; set; }
    public decimal AnnualPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime StartDate { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
}

public class RevenueTrendPointDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Currency { get; set; } = default!;
    public decimal CompletedAmount { get; set; }
    public int PaymentCount { get; set; }
}

public class PlanDistributionDto
{
    public string PlanName { get; set; } = default!;
    public int Subscriptions { get; set; }
}

public class PlatformRevenueDto
{
    public decimal MonthlyRecurringRevenue { get; set; }
    public decimal AnnualRecurringRevenue { get; set; }
    public decimal CompletedPaymentTotal { get; set; }
    public int ActiveSubscriptions { get; set; }
    public int TrialSubscriptions { get; set; }
    public int CancelledSubscriptions { get; set; }
    public int FailedPayments { get; set; }
    public decimal CollectedRevenue { get; set; }
    public decimal BookedRevenue { get; set; }
    public decimal TrialPipelineMonthly { get; set; }
    public decimal AtRiskMonthly { get; set; }
    public decimal EstimatedCopilotCostUsd { get; set; }
    public decimal EstimatedAgentCostUsd { get; set; }
    public int NewSubscriptionsThisMonth { get; set; }
    public int ChurnedThisMonth { get; set; }
    public int ActiveAfterTrial { get; set; }
    public IReadOnlyList<PlanDistributionDto> Distribution { get; set; } = [];
    public IReadOnlyList<RevenueTrendPointDto> Trends { get; set; } = [];
    public string Note { get; set; } =
        "MRR is active subscriptions at the contracted price. Trials are pipeline. Past-due amounts are at risk, not collected. Collected revenue is completed transactions. Booked revenue is invoices that are not void. Mixed currencies are not converted. AI figures are estimates, not provider invoices.";
}

public class AiUsageTotalsDto
{
    public int Requests { get; set; }
    public int FailedRequests { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public string ModelNote { get; set; } =
        "Copilot ledger rows store tokens and an estimated cost. The model is stored when the caller supplies it. Agent executions store the model on the execution. Estimated cost is not a provider invoice.";
}

public class AiUsageCompanyDto
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = default!;
    public string OperationKey { get; set; } = default!;
    public int Requests { get; set; }
    public int FailedRequests { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
}

public class AiUsageReportDto
{
    public AiUsageTotalsDto Totals { get; set; } = new();
    public IReadOnlyList<AiUsageCompanyDto> Rows { get; set; } = [];
}

public class AgentExecutionDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = default!;
    public string AgentKey { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public double? DurationSeconds { get; set; }
    public string? ErrorMessage { get; set; }
    public int? RetryAttempts { get; set; }
    public int? TotalTokens { get; set; }
}

public class AgentExecutionDetailDto : AgentExecutionDto
{
    public IReadOnlyList<AgentExecutionStepDto> Steps { get; set; } = [];
}

public class AgentExecutionStepDto
{
    public string StepKey { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string? Message { get; set; }
}

public class PlatformHealthDto
{
    public string Status { get; set; } = default!;
    public bool DatabaseConnected { get; set; }
    public bool QdrantConfigured { get; set; }
    public bool? QdrantReachable { get; set; }
    public bool AiProviderConfigured { get; set; }
    public string? ChatModel { get; set; }
    public string? EmbeddingModel { get; set; }
    public int FailedJobs { get; set; }
    public int DeadJobs { get; set; }
    public DateTime CheckedAt { get; set; }
}

public class PlatformSettingDto
{
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
    public DateTime UpdatedAt { get; set; }
}

public class AnnouncementDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string Body { get; set; } = default!;
    public bool IsPublished { get; set; }
    public bool IsActive { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public bool TargetAllCompanies { get; set; }
    public IReadOnlyList<Guid> CompanyIds { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

public class BusinessTypeTemplateDto
{
    public string Key { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public IReadOnlyList<string> Modules { get; set; } = [];
}

public class CompanyMembershipDto
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = default!;
    public string Role { get; set; } = default!;
    public string Status { get; set; } = default!;
    public bool IsCurrent { get; set; }
}

public class StoredFileDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class KnowledgeDocumentDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string DocumentType { get; set; } = default!;
    public bool IsIndexed { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CompanyResourceDto
{
    public int MemberCount { get; set; }
    public int MaxUsers { get; set; }
    public int AiRequestsUsed { get; set; }
    public int MaxAiRequests { get; set; }
    public long StorageUsedMb { get; set; }
    public long MaxStorageMb { get; set; }
    public int KnowledgeDocuments { get; set; }
    public int StoredFiles { get; set; }
    public IReadOnlyList<string> EnabledModules { get; set; } = [];
}

public record SwitchCompanyRequest(Guid CompanyId);
public record TransferOwnershipRequest(string NewOwnerUserId);
public record SetCompanyStatusRequest(string Status);
public record UpsertPlatformSettingRequest(string Key, string Value);
public record SaveAnnouncementRequest(
    string Title,
    string Body,
    bool Publish,
    bool TargetAllCompanies,
    DateTime? StartsAt,
    DateTime? EndsAt,
    IReadOnlyList<Guid>? CompanyIds);

public class PlatformPage<T>
{
    public PagedResult<T> Page { get; set; } = new();
}
