namespace BusinessOS.Application.Features.Growth;

public sealed class MarketingLeadWrite
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Company { get; set; }
    public string Kind { get; set; } = "Contact";
    public string Topic { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Message { get; set; } = "";
    public string? SourcePath { get; set; }
    public string? ReferralCode { get; set; }
    public bool MarketingOptIn { get; set; }
}

public sealed class MarketingLeadDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Company { get; set; }
    public string Kind { get; set; } = "";
    public string Topic { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Message { get; set; } = "";
    public string Status { get; set; } = "";
    public string? SourcePath { get; set; }
    public string? ReferralCode { get; set; }
    public bool MarketingOptIn { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? ConvertedCompanyId { get; set; }
}

public sealed class MarketingLeadStatusWrite
{
    public string Status { get; set; } = "";
    public string? Notes { get; set; }
}

public sealed class PlatformSupportTicketWrite
{
    public string? RequesterName { get; set; }
    public string? RequesterEmail { get; set; }
    public string Category { get; set; } = "General";
    public string Subject { get; set; } = "";
    public string Message { get; set; } = "";
    public string Priority { get; set; } = "Normal";
}

public sealed class PlatformSupportTicketDto
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string RequesterName { get; set; } = "";
    public string RequesterEmail { get; set; } = "";
    public string Category { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Message { get; set; } = "";
    public string Status { get; set; } = "";
    public string Priority { get; set; } = "";
    public string? AgentReply { get; set; }
    public bool EscalatedToHuman { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public sealed class PlatformSupportUpdateWrite
{
    public string Status { get; set; } = "";
    public string? AgentReply { get; set; }
    public bool EscalatedToHuman { get; set; }
}

public sealed class ReferralCodeDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public bool IsActive { get; set; }
    public int RedemptionCount { get; set; }
    public int MaxRedemptions { get; set; }
    public string SharePath { get; set; } = "";
}

public sealed class ReferralValidateDto
{
    public bool Valid { get; set; }
    public string? Code { get; set; }
    public string? Message { get; set; }
}

public sealed class SaaSAnalyticsDto
{
    public decimal MonthlyRecurringRevenue { get; set; }
    public decimal AnnualRecurringRevenue { get; set; }
    public int ActiveSubscriptions { get; set; }
    public int TrialSubscriptions { get; set; }
    public int CancelledSubscriptions { get; set; }
    public int FailedPayments { get; set; }
    public int NewSubscriptionsThisMonth { get; set; }
    public int ChurnedThisMonth { get; set; }
    public int ActiveAfterTrial { get; set; }
    public int OpenSupportTickets { get; set; }
    public int NewMarketingLeadsThisMonth { get; set; }
    public decimal? AverageRevenuePerTenant { get; set; }
    public decimal? TrialToPaidConversionRate { get; set; }
    public decimal? MonthlyChurnRate { get; set; }
    public decimal? EstimatedAiCostPerTenantUsd { get; set; }
    public decimal? EstimatedGrossMarginUsd { get; set; }
    public decimal? CustomerLifetimeValue { get; set; }
    public decimal? CustomerAcquisitionCost { get; set; }
    public IReadOnlyList<string> MeasuredMetrics { get; set; } = [];
    public IReadOnlyList<string> EstimatedMetrics { get; set; } = [];
    public IReadOnlyList<string> UnavailableMetrics { get; set; } = [];
    public string Note { get; set; } =
        "Measured metrics come from subscription and ledger rows. Estimated metrics use catalog AI prices, not provider invoices. CAC and LTV stay unavailable until acquisition spend and cohort retention are recorded.";
}

public interface IGrowthService
{
    Task<MarketingLeadDto> SubmitLeadAsync(MarketingLeadWrite request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MarketingLeadDto>> ListLeadsAsync(string? status, string? kind, CancellationToken cancellationToken = default);
    Task<MarketingLeadDto> UpdateLeadStatusAsync(Guid id, MarketingLeadStatusWrite request, CancellationToken cancellationToken = default);
    Task<PlatformSupportTicketDto> CreateSupportTicketAsync(PlatformSupportTicketWrite request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlatformSupportTicketDto>> ListMySupportTicketsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlatformSupportTicketDto>> ListPlatformSupportTicketsAsync(string? status, CancellationToken cancellationToken = default);
    Task<PlatformSupportTicketDto> UpdateSupportTicketAsync(Guid id, PlatformSupportUpdateWrite request, CancellationToken cancellationToken = default);
    Task<ReferralCodeDto> GetOrCreateReferralCodeAsync(CancellationToken cancellationToken = default);
    Task<ReferralValidateDto> ValidateReferralCodeAsync(string code, CancellationToken cancellationToken = default);
    Task AttributeReferralAsync(string code, string email, Guid? companyId, CancellationToken cancellationToken = default);
    Task<SaaSAnalyticsDto> GetSaaSAnalyticsAsync(CancellationToken cancellationToken = default);
}
