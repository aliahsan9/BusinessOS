using System.Text.Json;
using BusinessOS.Application.Features.Autopilot;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BusinessOS.Infrastructure.AI.Autopilot;

public sealed class AutonomySafetyEngine : IAutonomySafetyEngine
{
    private static readonly HashSet<string> AlwaysHigh = new(StringComparer.OrdinalIgnoreCase)
    {
        "SendExternalEmail", "PublishSocialPost", "SendSupportReply", "SubmitPreparedPurchaseOrder",
        "SubmitPaymentReminder", "IssueRefund", "ConvertLead", "ReceivePurchaseOrderVerified",
        "MakePayment", "RecordPayment", "DeleteRecord"
    };

    private static readonly HashSet<string> AlwaysMedium = new(StringComparer.OrdinalIgnoreCase)
    {
        "CreateTask", "CreateFollowUp", "CreateAgentTask", "CreateReminder", "CreateSupportTicket",
        "UpdateLead", "UpdateSupportTicket", "CreateOutreachCampaign", "QualifyLead",
        "PreparePurchaseOrder", "PreparePaymentReminder", "PrepareInvoiceDraft",
        "CreateQuotationDraft", "CreateProposalDraft", "CreateCampaignDraft", "CreateContentDraft",
        "CreateContentCalendar", "CreateEmailDraft", "CreateResponseDraft", "ScheduleSupportFollowUp"
    };

    private readonly BusinessOSDbContext _db;
    private readonly IMemoryCache _cache;

    public AutonomySafetyEngine(BusinessOSDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<bool> IsEmergencyStoppedAsync(CancellationToken cancellationToken = default)
    {
        var config = await GetConfigCachedAsync(cancellationToken);
        return config?.EmergencyStop == true;
    }

    public async Task<bool> IsAgentPausedAsync(string agentKey, CancellationToken cancellationToken = default)
    {
        var config = await GetConfigCachedAsync(cancellationToken);
        if (config is null)
            return false;
        var paused = ParseStrings(config.PausedAgentKeysJson);
        return paused.Contains(agentKey, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<AutonomyDecision> EvaluateToolAsync(
        string agentKey,
        string toolName,
        string? argsJson,
        bool approvalAlreadyGranted,
        CancellationToken cancellationToken = default)
    {
        var config = await GetConfigCachedAsync(cancellationToken);
        var mode = AutopilotModes.Normalize(config?.Mode);
        var risk = ClassifyRisk(toolName);

        if (config?.EmergencyStop == true)
        {
            var emergencyAllowsRead = risk == AutopilotRiskLevels.Low
                && NalvaToolCatalog.TryGet(toolName, out var t)
                && t is { IsWrite: false, ExternalEffect: false };
            if (!emergencyAllowsRead)
            {
                return new AutonomyDecision
                {
                    Blocked = true,
                    RequireApproval = false,
                    AllowAutonomous = false,
                    RiskLevel = risk,
                    Mode = mode,
                    Reason = "Emergency stop is active. Autonomous and external actions are blocked."
                };
            }
        }

        if (await IsAgentPausedAsync(agentKey, cancellationToken))
        {
            return new AutonomyDecision
            {
                Blocked = true,
                RequireApproval = false,
                AllowAutonomous = false,
                RiskLevel = risk,
                Mode = mode,
                Reason = $"Agent '{agentKey}' is paused by Autopilot."
            };
        }

        if (!IsWithinWorkingHours(config?.WorkingHoursJson, config?.TimeZoneId))
        {
            if (risk != AutopilotRiskLevels.Low)
            {
                return new AutonomyDecision
                {
                    Blocked = false,
                    RequireApproval = true,
                    AllowAutonomous = false,
                    RiskLevel = risk,
                    Mode = mode,
                    Reason = "Outside configured working hours. Approval required."
                };
            }
        }

        if (ExceedsSpendingLimit(config, argsJson))
        {
            return new AutonomyDecision
            {
                Blocked = false,
                RequireApproval = true,
                AllowAutonomous = false,
                RiskLevel = AutopilotRiskLevels.High,
                Mode = mode,
                Reason = "Action amount exceeds configured spending limits."
            };
        }

        if (config is null || !config.IsEnabled)
        {
            // Autopilot off: preserve existing Nalva approval catalog behavior via RequireApproval hint.
            var catalogRequires = NalvaToolCatalog.TryGet(toolName, out var tool) && tool!.RequiresApproval;
            return new AutonomyDecision
            {
                Blocked = false,
                RequireApproval = catalogRequires || risk == AutopilotRiskLevels.High,
                AllowAutonomous = risk == AutopilotRiskLevels.Low && !catalogRequires,
                RiskLevel = risk,
                Mode = mode,
                Reason = "Autopilot disabled; catalog and high-risk approval rules apply."
            };
        }

        if (approvalAlreadyGranted)
        {
            return new AutonomyDecision
            {
                Blocked = false,
                RequireApproval = false,
                AllowAutonomous = true,
                RiskLevel = risk,
                Mode = mode,
                Reason = "Human approval already granted and revalidated by caller."
            };
        }

        var allowedMedium = ParseStrings(config.AllowedMediumToolsJson);
        var allowlistHigh = ParseStrings(config.ControlledAllowlistToolsJson);

        return mode switch
        {
            AutopilotModes.Copilot => EvaluateCopilot(risk, toolName),
            AutopilotModes.Supervised => EvaluateSupervised(risk, toolName, allowedMedium),
            AutopilotModes.Controlled => EvaluateControlled(risk, toolName, allowedMedium, allowlistHigh),
            _ => EvaluateCopilot(risk, toolName)
        };
    }

    private static AutonomyDecision EvaluateCopilot(string risk, string toolName)
    {
        var allow = risk == AutopilotRiskLevels.Low
            && NalvaToolCatalog.TryGet(toolName, out var tool)
            && tool is { IsWrite: false };
        return new AutonomyDecision
        {
            Blocked = false,
            AllowAutonomous = allow,
            RequireApproval = !allow,
            RiskLevel = risk,
            Mode = AutopilotModes.Copilot,
            Reason = allow
                ? "Copilot allows read-only analysis."
                : "Copilot mode: AI prepares drafts/recommendations only; humans execute writes and external actions."
        };
    }

    private static AutonomyDecision EvaluateSupervised(string risk, string toolName, IReadOnlyList<string> allowedMedium)
    {
        if (risk == AutopilotRiskLevels.Low)
        {
            return new AutonomyDecision
            {
                Blocked = false,
                AllowAutonomous = true,
                RequireApproval = false,
                RiskLevel = risk,
                Mode = AutopilotModes.Supervised,
                Reason = "Supervised Autopilot allows low-risk actions."
            };
        }

        if (risk == AutopilotRiskLevels.Medium && allowedMedium.Contains(toolName, StringComparer.OrdinalIgnoreCase))
        {
            return new AutonomyDecision
            {
                Blocked = false,
                AllowAutonomous = true,
                RequireApproval = false,
                RiskLevel = risk,
                Mode = AutopilotModes.Supervised,
                Reason = "Supervised Autopilot: medium-risk tool explicitly allowed by policy."
            };
        }

        return new AutonomyDecision
        {
            Blocked = false,
            AllowAutonomous = false,
            RequireApproval = true,
            RiskLevel = risk,
            Mode = AutopilotModes.Supervised,
            Reason = "Supervised Autopilot requires human approval for this action."
        };
    }

    private static AutonomyDecision EvaluateControlled(
        string risk,
        string toolName,
        IReadOnlyList<string> allowedMedium,
        IReadOnlyList<string> allowlistHigh)
    {
        if (risk == AutopilotRiskLevels.Low)
        {
            return new AutonomyDecision
            {
                Blocked = false,
                AllowAutonomous = true,
                RequireApproval = false,
                RiskLevel = risk,
                Mode = AutopilotModes.Controlled,
                Reason = "Controlled Autopilot allows low-risk actions."
            };
        }

        if (risk == AutopilotRiskLevels.Medium
            && (allowedMedium.Count == 0 || allowedMedium.Contains(toolName, StringComparer.OrdinalIgnoreCase)))
        {
            return new AutonomyDecision
            {
                Blocked = false,
                AllowAutonomous = true,
                RequireApproval = false,
                RiskLevel = risk,
                Mode = AutopilotModes.Controlled,
                Reason = "Controlled Autopilot allows authorized medium-risk internal actions."
            };
        }

        // Payments/refunds never auto-execute even on allowlist.
        if (toolName is "IssueRefund" or "MakePayment" or "RecordPayment")
        {
            return new AutonomyDecision
            {
                Blocked = false,
                AllowAutonomous = false,
                RequireApproval = true,
                RiskLevel = AutopilotRiskLevels.High,
                Mode = AutopilotModes.Controlled,
                Reason = "Financial settlement actions always require human approval."
            };
        }

        if (risk == AutopilotRiskLevels.High && allowlistHigh.Contains(toolName, StringComparer.OrdinalIgnoreCase))
        {
            return new AutonomyDecision
            {
                Blocked = false,
                AllowAutonomous = true,
                RequireApproval = false,
                RiskLevel = risk,
                Mode = AutopilotModes.Controlled,
                Reason = "Controlled Autopilot: high-risk tool explicitly allowlisted under spending limits."
            };
        }

        return new AutonomyDecision
        {
            Blocked = false,
            AllowAutonomous = false,
            RequireApproval = true,
            RiskLevel = risk,
            Mode = AutopilotModes.Controlled,
            Reason = "Controlled Autopilot requires approval; tool is not on the high-risk allowlist."
        };
    }

    public static string ClassifyRisk(string toolName)
    {
        if (AlwaysHigh.Contains(toolName))
            return AutopilotRiskLevels.High;

        if (NalvaToolCatalog.TryGet(toolName, out var tool) && tool is not null)
        {
            if (tool.ExternalEffect || tool.RequiresApproval || string.Equals(tool.Risk, "High", StringComparison.OrdinalIgnoreCase))
                return AutopilotRiskLevels.High;
            if (tool.IsWrite || AlwaysMedium.Contains(toolName) || string.Equals(tool.Risk, "Medium", StringComparison.OrdinalIgnoreCase))
                return AutopilotRiskLevels.Medium;
            return AutopilotRiskLevels.Low;
        }

        if (AlwaysMedium.Contains(toolName))
            return AutopilotRiskLevels.Medium;
        return AutopilotRiskLevels.High;
    }

    private async Task<BusinessAutopilotConfig?> GetConfigCachedAsync(CancellationToken cancellationToken)
    {
        // Short TTL so emergency stop propagates quickly without hammering DB on every tool call.
        const string cacheKeyPrefix = "autopilot-config:";
        // Tenant id is applied by EF filters; cache per scoped request via MemoryCache with short sliding.
        if (_cache.TryGetValue(cacheKeyPrefix + "current", out BusinessAutopilotConfig? cached))
            return cached;

        var row = await _db.BusinessAutopilotConfigs.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        _cache.Set(cacheKeyPrefix + "current", row, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(3)
        });
        return row;
    }

    internal void InvalidateCache()
    {
        _cache.Remove("autopilot-config:current");
    }

    private static IReadOnlyList<string> ParseStrings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool ExceedsSpendingLimit(BusinessAutopilotConfig? config, string? argsJson)
    {
        if (config is null || string.IsNullOrWhiteSpace(argsJson))
            return false;
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            decimal? amount = null;
            foreach (var name in new[] { "amount", "total", "totalAmount", "spend", "budget" })
            {
                if (doc.RootElement.TryGetProperty(name, out var el) && el.TryGetDecimal(out var value))
                {
                    amount = value;
                    break;
                }
            }
            if (amount is null)
                return false;
            if (config.SpendingLimitPerAction is decimal per && amount > per)
                return true;
        }
        catch (JsonException)
        {
            return false;
        }
        return false;
    }

    private static bool IsWithinWorkingHours(string? workingHoursJson, string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(workingHoursJson) || workingHoursJson is "{}")
            return true;
        try
        {
            using var doc = JsonDocument.Parse(workingHoursJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("enabled", out var enabledEl) || enabledEl.ValueKind != JsonValueKind.True)
                return true;
            var start = root.TryGetProperty("start", out var s) ? s.GetString() : "09:00";
            var end = root.TryGetProperty("end", out var e) ? e.GetString() : "18:00";
            if (!TimeSpan.TryParse(start, out var startTs) || !TimeSpan.TryParse(end, out var endTs))
                return true;

            DateTime localNow;
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(timeZoneId) ? "UTC" : timeZoneId!);
                localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                localNow = DateTime.UtcNow;
            }

            var tod = localNow.TimeOfDay;
            if (endTs >= startTs)
                return tod >= startTs && tod <= endTs;
            return tod >= startTs || tod <= endTs;
        }
        catch (JsonException)
        {
            return true;
        }
    }
}
