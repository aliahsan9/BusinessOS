using System.Text.RegularExpressions;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Nalva;

namespace BusinessOS.Infrastructure.AI.Nalva;

/// <summary>
/// Bridges Navla chat / Command Center requests into the Nalva workforce orchestrator.
/// Never invents completed tool outcomes — only plans/starts persisted workflows.
/// </summary>
public sealed partial class NavlaWorkforceManagerService : INavlaWorkforceManager
{
    private readonly INalvaCollaborationService _collaboration;
    private readonly INalvaWorkforceService _workforce;
    private readonly ICurrentUserService _user;

    public NavlaWorkforceManagerService(
        INalvaCollaborationService collaboration,
        INalvaWorkforceService workforce,
        ICurrentUserService user)
    {
        _collaboration = collaboration;
        _workforce = workforce;
        _user = user;
    }

    public bool LooksLikeWorkforceDelegation(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        var text = ExpandWorkforceHints(message);
        if (WorkforceVerbRegex().IsMatch(text) && WorkforceDomainRegex().IsMatch(text))
            return true;

        // Explicit Navla manager phrasing.
        if (NavlaManagerRegex().IsMatch(text) && WorkforceDomainRegex().IsMatch(text))
            return true;

        // Multi-agent coordination phrases.
        return CoordinateRegex().IsMatch(text);
    }

    public async Task<NavlaWorkforceDelegationResult> DelegateAsync(
        NavlaWorkforceDelegationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_user.HasPermission(PermissionCodes.NalvaView)
            && !_user.HasPermission(PermissionCodes.NalvaManage))
        {
            return new NavlaWorkforceDelegationResult(
                false,
                "You do not have permission to use the AI workforce.",
                null, null, 0, [], [], [], false, null, null,
                "Missing Nalva permission.", false);
        }

        var objective = (request.Message ?? "").Trim();
        if (string.IsNullOrWhiteSpace(objective))
        {
            return new NavlaWorkforceDelegationResult(
                true,
                "Tell Navla what business outcome to coordinate.",
                null, null, 0, [], [],
                ["What should the AI workforce do?"],
                false, null, null, "Empty objective.", false);
        }

        var planRequest = new NalvaObjectivePlanRequest
        {
            Objective = objective,
            ArgumentsJson = request.ArgumentsJson,
            ActivateMissingAgents = request.ActivateMissingAgents,
            ActivateWorkflow = true,
            IdempotencyKey = request.IdempotencyKey
        };

        var plan = request.StartImmediately && _user.HasPermission(PermissionCodes.NalvaManage)
            ? await _collaboration.StartObjectiveAsync(planRequest, cancellationToken)
            : await _collaboration.PlanObjectiveAsync(planRequest, cancellationToken);

        var started = plan.WorkflowRunId is not null;
        var summary = started
            ? $"Navla started workflow '{plan.WorkflowName}' ({plan.WorkflowKey}). Status: {plan.RunStatus}. {plan.ReportSummary}".Trim()
            : plan.CanStart
                ? $"Navla planned '{plan.WorkflowName}'. Say start or approve to run it with agents: {string.Join(", ", plan.RequiredAgents)}."
                : $"Navla could not start yet: {plan.BlockReason ?? "Clarify the objective."}";

        return new NavlaWorkforceDelegationResult(
            true,
            summary,
            plan.WorkflowKey,
            plan.WorkflowName,
            plan.Confidence,
            plan.RequiredAgents,
            plan.MissingAgents,
            plan.ClarifyingQuestions,
            started,
            plan.WorkflowRunId,
            plan.RunStatus,
            plan.BlockReason,
            SimulatedLabel: false);
    }

    public async Task<NalvaApplyWorkforcePackResult> ActivateCoreEmployeesAsync(
        CancellationToken cancellationToken = default)
    {
        var activated = new List<string>();
        var skipped = new List<string>();
        foreach (var key in NalvaAgentCatalog.Phase8BEmployeeKeys)
        {
            if (!NalvaAgentCatalog.TryGet(key, out _))
            {
                skipped.Add(key);
                continue;
            }

            await _workforce.ActivateAsync(key, cancellationToken);
            activated.Add(key);
        }

        var workflows = await _workforce.ApplyWorkforcePackAsync(new NalvaApplyWorkforcePackRequest
        {
            PackKey = "phase8b-core",
            ActivateRecommendedAgents = false,
            ActivateRecommendedWorkflows = true
        }, cancellationToken);

        return new NalvaApplyWorkforcePackResult(
            "phase8b-core",
            activated,
            workflows.ActivatedWorkflows,
            skipped);
    }

    private static string ExpandWorkforceHints(string message)
    {
        var text = message.Trim().ToLowerInvariant();
        // Roman Urdu / Urdu domain hints normalized for phrase matching.
        text = text
            .Replace("stock check", "inventory check")
            .Replace("stock dekho", "inventory check")
            .Replace("stock dekhen", "inventory check")
            .Replace("kam stock", "low stock")
            .Replace("stock kam", "low stock")
            .Replace("purchase order", "purchase order")
            .Replace("po tayyar", "prepare purchase order")
            .Replace("order tayyar", "prepare purchase order")
            .Replace("sales report", "sales report")
            .Replace("marketing campaign", "marketing campaign")
            .Replace("payment reminder", "payment reminder")
            .Replace("baqi payment", "overdue invoice")
            .Replace("pending payment", "overdue invoice");
        return text;
    }

    [GeneratedRegex(
        @"\b(check|prepare|create|analyze|identify|generate|coordinate|delegate|assign|run|monitor|review|find|restock|reorder)\b|tayyar|dekho|dekhen|banao|banayen",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WorkforceVerbRegex();

    [GeneratedRegex(
        @"\b(inventory|stock|purchase order|procurement|supplier|sales|lead|outreach|marketing|campaign|support|ticket|finance|revenue|expense|payment|receivable|workforce|ai employee|agent)\b|انوینٹری|خریداری|فروخت|مارکیٹنگ",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WorkforceDomainRegex();

    [GeneratedRegex(
        @"\b(navla[,:]?\s+|hey navla|navla\s+(please|check|prepare|coordinate|assign))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NavlaManagerRegex();

    [GeneratedRegex(
        @"\b(coordinate (the )?(workforce|agents|ai employees)|multi[- ]agent|ask (the )?(inventory|sales|marketing|finance|procurement|support) (manager|agent))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CoordinateRegex();
}
