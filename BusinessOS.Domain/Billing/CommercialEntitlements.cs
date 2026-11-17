using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Billing;

/// <summary>
/// Feature keys the entitlement service understands. Plan rows and company overrides
/// store these keys. Call sites ask for a key; they do not embed plan names.
/// </summary>
public static class CommercialFeatureKeys
{
    public const string AiAssistant = "ai_assistant";
    public const string AdvancedAnalytics = "advanced_analytics";
    public const string PdfReports = "pdf_reports";
    public const string AdvancedReports = "advanced_reports";
    public const string PrioritySupport = "priority_support";
    public const string ApiAccess = "api_access";
    public const string MultiBranch = "multi_branch";
    public const string Automations = "automations";
    public const string Integrations = "integrations";
    public const string ClientPortal = "client_portal";
    public const string Storefront = "storefront";
    public const string MultiAgent = "multi_agent";
    public const string Finance = "finance";
    public const string AdvancedSecurity = "advanced_security";

    public static readonly IReadOnlyList<string> All =
    [
        AiAssistant,
        AdvancedAnalytics,
        PdfReports,
        AdvancedReports,
        PrioritySupport,
        ApiAccess,
        MultiBranch,
        Automations,
        Integrations,
        ClientPortal,
        Storefront,
        MultiAgent,
        Finance,
        AdvancedSecurity
    ];

    public static bool IsKnown(string? key) =>
        !string.IsNullOrWhiteSpace(key) &&
        All.Contains(key.Trim(), StringComparer.OrdinalIgnoreCase);
}

public readonly record struct CommercialQuotas(int AgentExecutions, int AutomationRuns, int GraceDays);

/// <summary>
/// Default feature matrix for the four public plans. A database grant or company override replaces these.
/// </summary>
public static class CommercialFeatureCatalog
{
    public static bool DefaultEnabled(string? slug, string key)
    {
        var plan = (slug ?? "free").Trim().ToLowerInvariant();
        var feature = key.Trim().ToLowerInvariant();
        if (plan == "enterprise")
            return true;
        if (plan == "professional")
            return feature != CommercialFeatureKeys.AdvancedSecurity;

        if (plan == "starter")
        {
            return feature is CommercialFeatureKeys.AiAssistant
                or CommercialFeatureKeys.PdfReports
                or CommercialFeatureKeys.Finance
                or CommercialFeatureKeys.Automations;
        }

        return feature is CommercialFeatureKeys.AiAssistant or CommercialFeatureKeys.Automations;
    }

    public static CommercialQuotas Quotas(string? slug) => (slug ?? "free").Trim().ToLowerInvariant() switch
    {
        "starter" => new CommercialQuotas(40, 25, 3),
        "professional" => new CommercialQuotas(400, 200, 7),
        "enterprise" => new CommercialQuotas(SubscriptionPlan.Unlimited, SubscriptionPlan.Unlimited, 14),
        _ => new CommercialQuotas(10, 5, 0)
    };
}

public enum CommercialAccessMode
{
    Full = 0,
    Grace = 1,
    Restricted = 2
}

public enum QuotaOutcome
{
    Allowed = 0,
    Warning = 1,
    Blocked = 2
}

public sealed record FeatureGrantState(
    string Key,
    bool Enabled,
    long? Limit,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo);

public sealed class CommercialEntitlementInput
{
    public required SubscriptionPlan Plan { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Free;
    public bool CancelAtPeriodEnd { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime? GraceEndsAt { get; set; }
    public bool PreserveContract { get; set; }
    public ContractedEntitlement? Snapshot { get; set; }
    public DateTime UtcNow { get; set; } = DateTime.UtcNow;
    public IReadOnlyList<FeatureGrantState> PlanGrants { get; set; } = [];
    public IReadOnlyList<FeatureGrantState> CompanyOverrides { get; set; } = [];
}

public sealed class EffectiveCommercialAccess
{
    public CommercialAccessMode Mode { get; init; }
    public required IReadOnlyDictionary<string, bool> Features { get; init; }
    public int AgentExecutionLimit { get; init; }
    public int AutomationRunLimit { get; init; }
    public DateTime? GraceEndsAt { get; init; }

    public bool IsEnabled(string key) =>
        Features.TryGetValue(key, out var enabled) && enabled;
}

public static class CommercialEntitlementEvaluator
{
    public static CommercialAccessMode ResolveMode(
        SubscriptionStatus status,
        bool cancelAtPeriodEnd,
        DateTime? currentPeriodEnd,
        DateTime? graceEndsAt,
        DateTime utcNow)
    {
        if (status is SubscriptionStatus.Active
            or SubscriptionStatus.Trial
            or SubscriptionStatus.Free
            or SubscriptionStatus.PendingPayment
            or SubscriptionStatus.Incomplete)
        {
            return CommercialAccessMode.Full;
        }

        if (status == SubscriptionStatus.PastDue)
            return graceEndsAt is null || graceEndsAt >= utcNow
                ? CommercialAccessMode.Grace
                : CommercialAccessMode.Restricted;

        if (status == SubscriptionStatus.Cancelled &&
            cancelAtPeriodEnd &&
            currentPeriodEnd is DateTime end &&
            end >= utcNow)
        {
            return CommercialAccessMode.Full;
        }

        return CommercialAccessMode.Restricted;
    }

    public static EffectiveCommercialAccess Evaluate(CommercialEntitlementInput input)
    {
        var mode = ResolveMode(
            input.Status,
            input.CancelAtPeriodEnd,
            input.CurrentPeriodEnd,
            input.GraceEndsAt,
            input.UtcNow);
        var restricted = mode == CommercialAccessMode.Restricted;
        var features = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in CommercialFeatureKeys.All)
        {
            var enabled = CommercialFeatureCatalog.DefaultEnabled(input.Plan.Slug, key);
            if (Column(input.Plan, key) is bool column)
                enabled = column;
            if (input.PreserveContract && input.Snapshot is not null && SnapshotFlag(input.Snapshot, key) is bool snapped)
                enabled = snapped;

            var grant = Pick(input.PlanGrants, key, input.UtcNow);
            if (grant is not null)
                enabled = grant.Enabled;

            if (restricted)
                enabled = CommercialFeatureCatalog.DefaultEnabled("free", key);

            var company = Pick(input.CompanyOverrides, key, input.UtcNow);
            if (company is not null)
                enabled = company.Enabled;

            features[key] = enabled;
        }

        return new EffectiveCommercialAccess
        {
            Mode = mode,
            Features = features,
            AgentExecutionLimit = Limit(
                input,
                restricted,
                CommercialFeatureCatalog.Quotas("free").AgentExecutions,
                input.Plan.MaxAgentExecutions,
                input.Snapshot?.MaxAgentExecutions,
                "agent_executions"),
            AutomationRunLimit = Limit(
                input,
                restricted,
                CommercialFeatureCatalog.Quotas("free").AutomationRuns,
                input.Plan.MaxAutomationRuns,
                input.Snapshot?.MaxAutomationRuns,
                "automation_runs"),
            GraceEndsAt = input.GraceEndsAt
        };
    }

    private static int Limit(
        CommercialEntitlementInput input,
        bool restricted,
        int freeLimit,
        int planLimit,
        int? snapshotLimit,
        string key)
    {
        var limit = planLimit;
        if (input.PreserveContract && snapshotLimit is int snapped)
            limit = snapped;

        var grant = Pick(input.PlanGrants, key, input.UtcNow);
        if (grant?.Limit is long granted)
            limit = Clamp(granted);

        if (restricted)
            limit = freeLimit;

        var company = Pick(input.CompanyOverrides, key, input.UtcNow);
        if (company?.Limit is long overridden)
            limit = Clamp(overridden);

        return limit;
    }

    private static int Clamp(long value)
    {
        if (value < -1)
            return -1;
        if (value > int.MaxValue)
            return int.MaxValue;
        return (int)value;
    }

    private static FeatureGrantState? Pick(IReadOnlyList<FeatureGrantState> grants, string key, DateTime utcNow) =>
        grants
            .Where(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))
            .Where(x => x.EffectiveFrom <= utcNow && (x.EffectiveTo is null || x.EffectiveTo > utcNow))
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefault();

    private static bool? Column(SubscriptionPlan plan, string key) => key switch
    {
        CommercialFeatureKeys.AiAssistant => plan.HasAiAssistant,
        CommercialFeatureKeys.AdvancedAnalytics => plan.HasAdvancedAnalytics,
        CommercialFeatureKeys.PdfReports => plan.HasPdfReports,
        CommercialFeatureKeys.AdvancedReports => plan.HasAdvancedReports,
        CommercialFeatureKeys.PrioritySupport => plan.HasPrioritySupport,
        CommercialFeatureKeys.ApiAccess => plan.HasApiAccess,
        CommercialFeatureKeys.MultiBranch => plan.HasMultiBranch,
        _ => null
    };

    private static bool? SnapshotFlag(ContractedEntitlement snapshot, string key) => key switch
    {
        CommercialFeatureKeys.AiAssistant => snapshot.HasAiAssistant,
        CommercialFeatureKeys.AdvancedAnalytics => snapshot.HasAdvancedAnalytics,
        CommercialFeatureKeys.PdfReports => snapshot.HasPdfReports,
        CommercialFeatureKeys.AdvancedReports => snapshot.HasAdvancedReports,
        CommercialFeatureKeys.PrioritySupport => snapshot.HasPrioritySupport,
        CommercialFeatureKeys.ApiAccess => snapshot.HasApiAccess,
        CommercialFeatureKeys.MultiBranch => snapshot.HasMultiBranch,
        _ => null
    };
}

public static class QuotaDecision
{
    public const decimal WarningRatio = 0.8m;

    public static QuotaOutcome Decide(long used, long additional, int maximum)
    {
        if (maximum < 0)
            return QuotaOutcome.Allowed;

        var next = Math.Max(0, used) + Math.Max(0, additional);
        if (next > maximum)
            return QuotaOutcome.Blocked;

        if (maximum > 0 && (decimal)next / maximum >= WarningRatio)
            return QuotaOutcome.Warning;

        return QuotaOutcome.Allowed;
    }
}

/// <summary>
/// Turns token counts into a cost using a price card. The result is an estimate unless a provider charge is supplied.
/// </summary>
public static class AiUsageCost
{
    public static decimal Estimate(int inputTokens, int outputTokens, decimal inputUsdPerMillion, decimal outputUsdPerMillion)
    {
        var input = Math.Max(0, inputTokens) / 1_000_000m * inputUsdPerMillion;
        var output = Math.Max(0, outputTokens) / 1_000_000m * outputUsdPerMillion;
        return decimal.Round(input + output, 6);
    }

    public static AiCostQuote Quote(
        int inputTokens,
        int outputTokens,
        decimal inputUsdPerMillion,
        decimal outputUsdPerMillion,
        decimal? actualProviderChargeUsd,
        string source)
    {
        var estimated = Estimate(inputTokens, outputTokens, inputUsdPerMillion, outputUsdPerMillion);
        return new AiCostQuote(estimated, actualProviderChargeUsd, source);
    }
}

public readonly record struct AiCostQuote(decimal EstimatedUsd, decimal? ActualProviderChargeUsd, string Source)
{
    public bool IsEstimate => ActualProviderChargeUsd is null;
}
