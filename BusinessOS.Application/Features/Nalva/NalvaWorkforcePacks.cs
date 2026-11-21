namespace BusinessOS.Application.Features.Nalva;

/// <summary>
/// Business-type workforce recommendations. Packs suggest agents and workflows;
/// owners still activate individually and can disable irrelevant agents.
/// </summary>
public static class NalvaWorkforcePackCatalog
{
    public static readonly IReadOnlyList<NalvaWorkforcePack> All =
    [
        Pack(
            "software-agency",
            "Software house",
            "Sales, delivery, support, and finance for a software development company.",
            ["sales", "marketing", "support", "software", "finance", "analyst", "assistant", "proposal", "qa", "ceo"],
            ["customer-acquisition", "autonomous-lead-generation", "lead-acquisition", "proposal-generation", "project-operations", "customer-support-automation", "executive-reporting", "financial-monitoring"]),
        Pack(
            "it-consultancy",
            "IT consultancy",
            "Client delivery and advisory operations with sales and finance support.",
            ["sales", "support", "software", "finance", "analyst", "assistant", "proposal", "ceo"],
            ["lead-acquisition", "proposal-generation", "project-operations", "executive-reporting", "financial-monitoring"]),
        Pack(
            "marketing-agency",
            "Digital marketing agency",
            "Lead generation, content, social, email, and campaign analysis.",
            ["sales", "marketing", "content", "social", "email", "seo", "support", "analyst", "ceo"],
            ["customer-acquisition", "content-generation", "social-campaign", "email-marketing", "seo-research", "marketing-performance", "lead-acquisition"]),
        Pack(
            "ecommerce",
            "E-commerce",
            "Catalog, inventory, orders, support, and marketing for online sellers.",
            ["sales", "procurement", "ecommerce", "support", "marketing", "finance", "analyst", "content"],
            ["inventory-automation", "customer-support-automation", "content-generation", "social-campaign", "overdue-payment-followup", "financial-monitoring"]),
        Pack(
            "retail",
            "Retail",
            "Inventory, procurement, sales, support, and finance for storefronts.",
            ["sales", "procurement", "support", "finance", "analyst", "ecommerce", "assistant"],
            ["inventory-automation", "customer-support-automation", "overdue-payment-followup", "financial-monitoring", "executive-reporting"]),
        Pack(
            "service",
            "Service business",
            "Clients, support, sales follow-up, and financial monitoring.",
            ["sales", "support", "success", "finance", "assistant", "analyst", "ceo"],
            ["lead-acquisition", "follow-up", "customer-support-automation", "customer-onboarding", "overdue-payment-followup", "executive-reporting"]),
        Pack(
            "distributor",
            "Distributor / wholesaler",
            "Order flow, procurement, inventory, and receivables for distribution.",
            ["sales", "procurement", "finance", "analyst", "support", "assistant", "ecommerce"],
            ["inventory-automation", "overdue-payment-followup", "financial-monitoring", "lead-qualification", "executive-reporting"]),
        Pack(
            "custom",
            "General / custom",
            "A balanced starter workforce. Disable agents that do not match the business.",
            ["ceo", "sales", "marketing", "support", "finance", "procurement", "analyst", "assistant", "hr"],
            ["customer-acquisition", "inventory-automation", "customer-support-automation", "financial-monitoring", "overdue-payment-followup", "executive-reporting"])
    ];

    private static readonly Dictionary<string, NalvaWorkforcePack> ByKey =
        All.ToDictionary(x => x.BusinessTypeKey, StringComparer.OrdinalIgnoreCase);

    public static NalvaWorkforcePack Resolve(string? businessType)
    {
        if (string.IsNullOrWhiteSpace(businessType))
            return ByKey["custom"];

        var key = businessType.Trim();
        if (ByKey.TryGetValue(key, out var pack))
            return pack;

        if (Contains(key, "market"))
            return ByKey["marketing-agency"];
        if (Contains(key, "software") || Contains(key, "dev"))
            return ByKey["software-agency"];
        if (Contains(key, "retail") || Contains(key, "store"))
            return ByKey["retail"];
        if (Contains(key, "ecom") || Contains(key, "online"))
            return ByKey["ecommerce"];
        if (Contains(key, "distribut") || Contains(key, "wholesale"))
            return ByKey["distributor"];
        if (Contains(key, "service") || Contains(key, "consult"))
            return Contains(key, "it") || Contains(key, "tech") ? ByKey["it-consultancy"] : ByKey["service"];

        return ByKey["custom"];
    }

    private static bool Contains(string value, string fragment) =>
        value.Contains(fragment, StringComparison.OrdinalIgnoreCase);

    private static NalvaWorkforcePack Pack(
        string key,
        string name,
        string description,
        string[] agents,
        string[] workflows) =>
        new(key, name, description, agents, workflows);
}

public sealed record NalvaWorkforcePack(
    string BusinessTypeKey,
    string Name,
    string Description,
    IReadOnlyList<string> RecommendedAgentKeys,
    IReadOnlyList<string> RecommendedWorkflowKeys);

public sealed record NalvaWorkforceRecommendationDto(
    string BusinessType,
    string PackKey,
    string PackName,
    string Description,
    IReadOnlyList<NalvaRecommendedAgentDto> Agents,
    IReadOnlyList<NalvaRecommendedWorkflowDto> Workflows,
    int AlreadyActiveAgents,
    int RecommendedAgentCount);

public sealed record NalvaRecommendedAgentDto(
    string Key,
    string Name,
    string Role,
    string Category,
    bool IsActive,
    bool IsRecommended);

public sealed record NalvaRecommendedWorkflowDto(
    string Key,
    string Name,
    string TriggerKind,
    bool IsActive,
    bool IsRecommended);

public sealed record NalvaApplyWorkforcePackRequest
{
    public bool ActivateRecommendedAgents { get; set; } = true;
    public bool ActivateRecommendedWorkflows { get; set; }
    public List<string>? OnlyAgentKeys { get; set; }
}

public sealed record NalvaApplyWorkforcePackResult(
    string PackKey,
    IReadOnlyList<string> ActivatedAgents,
    IReadOnlyList<string> ActivatedWorkflows,
    IReadOnlyList<string> Skipped);

public sealed record NalvaAgentOutcomeDto(
    string AgentKey,
    string AgentName,
    int Executions,
    int Succeeded,
    int Failed,
    int PendingApprovals,
    int ApprovedActions,
    int RejectedActions,
    int LeadsCreated,
    int DealsCreated,
    int FollowUpsCreated,
    int ContentDrafts,
    int EmailDrafts,
    int CampaignDrafts,
    int SupportTickets,
    int PurchaseDrafts,
    int Reports,
    int WorkItems,
    decimal EstimatedCostUsd,
    bool CostIsEstimate);
