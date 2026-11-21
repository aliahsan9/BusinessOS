namespace BusinessOS.Application.Features.Nalva;

public sealed record NalvaPlannedTool(string Name, string ArgsJson, int Priority = 1, bool RequiresApproval = false, string? DependsOn = null);

public static class NalvaPlanner
{
    private static readonly (string Phrase, string Tool)[] Phrases =
    [
        ("daily", "GenerateDailySummary"),
        ("overview", "GetCompanyOverview"),
        ("kpi", "GetBusinessKPIs"),
        ("sales summary", "GetSalesSummary"),
        ("financial", "GetFinancialSummary"),
        ("inventory", "GetInventorySummary"),
        ("low stock", "GetLowStockProducts"),
        ("pipeline", "GetSalesPipeline"),
        ("create lead", "CreateLead"),
        ("find customers", "ResearchExternalProspects"),
        ("find potential", "ResearchExternalProspects"),
        ("prospect", "ResearchExternalProspects"),
        ("qualify", "QualifyLead"),
        ("outreach", "GeneratePersonalizedOutreach"),
        ("follow up", "CreateFollowUp"),
        ("follow-up", "CreateFollowUp"),
        ("quotation", "CreateQuotationDraft"),
        ("proposal", "CreateProposalDraft"),
        ("campaign", "CreateOutreachCampaign"),
        ("content draft", "CreateContentDraft"),
        ("email draft", "CreateEmailDraft"),
        ("purchase order", "CreatePurchaseOrderDraft"),
        ("support ticket", "CreateSupportTicket"),
        ("customer support", "ReceiveSupportMessage"),
        ("support inquiry", "ReceiveSupportMessage"),
        ("escalate ticket", "EscalateTicket"),
        ("resolve ticket", "ResolveSupportTicket"),
        ("support follow", "ScheduleSupportFollowUp"),
        ("knowledge", "SearchKnowledgeBase"),
        ("churn", "GetChurnRisk"),
        ("duplicate", "FindDuplicateLeads"),
        ("cash flow", "ForecastCashFlow"),
        ("profit", "CalculateProfitability"),
        ("delegate", "DelegateAgentTask"),
        ("sales objective", "AssignSalesObjective"),
        ("competitor", "ResearchCompetitors"),
        ("attendance", "GetAttendanceSummary"),
        ("task", "GetUpcomingTasks")
    ];

    public static IReadOnlyList<NalvaPlannedTool> Plan(
        NalvaAgentDefinition agent,
        string objective,
        IReadOnlyList<string>? explicitTools,
        string argsJson)
    {
        var allowed = agent.Tools.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (explicitTools is { Count: > 0 })
        {
            return NalvaPlanPolicy.Accept(agent, explicitTools, argsJson);
        }

        var selected = new List<string>();
        foreach (var (phrase, tool) in Phrases)
        {
            if (objective.Contains(phrase, StringComparison.OrdinalIgnoreCase) && allowed.Contains(tool))
                selected.Add(NalvaToolCatalog.Get(tool).Name);
        }

        if (selected.Count == 0)
        {
            selected.AddRange(agent.Tools
                .Select(NalvaToolCatalog.Get)
                .Where(tool => !tool.IsWrite && !tool.RequiresApproval)
                .Take(3)
                .Select(tool => tool.Name));
        }

        return NalvaPlanPolicy.Accept(agent, selected, argsJson);
    }

    public static bool MatchesPhrase(NalvaAgentDefinition agent, string objective)
    {
        var allowed = agent.Tools.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Phrases.Any(pair =>
            objective.Contains(pair.Phrase, StringComparison.OrdinalIgnoreCase) && allowed.Contains(pair.Tool));
    }
}
