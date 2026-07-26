using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.Agents.Enums;
using BusinessOS.Application.Features.Agents.Services;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;

namespace BusinessOS.Infrastructure.AI.Agents;

public sealed class AgentPlanner : IAgentPlanner
{
    public bool RequiresWorkflow(
        AiCopilotIntent intent,
        string message,
        AiMemoryStateDto memory)
    {
        if (intent is AiCopilotIntent.ReportGeneration
            or AiCopilotIntent.Workflow
            or AiCopilotIntent.Onboarding)
        {
            return true;
        }

        var text = message.Trim().ToLowerInvariant();

        if (ContainsAny(text, "inventory report", "stock report", "warehouse report"))
            return true;

        if (ContainsAny(text, "purchase order", "create po", "draft po", "reorder")
            && ContainsAny(text, "create", "draft", "generate", "make", "prepare"))
            return true;

        if (ContainsAny(text, "multi-step", "workflow", "end to end", "full report"))
            return true;

        if (memory.OnboardingStep is > 0 && memory.OnboardingStep < 10)
            return true;

        return false;
    }

    public AgentWorkflowPlanDto Plan(
        string agentKey,
        AiCopilotIntent intent,
        string message,
        AiPageContextDto page,
        AiMemoryStateDto memory)
    {
        var key = AgentKeys.Normalize(agentKey);
        var text = message.Trim().ToLowerInvariant();

        if (intent is AiCopilotIntent.Onboarding
            || ContainsAny(text, "onboard", "setup company", "set up my business", "set up company"))
        {
            return PlanOnboarding(key, memory.PreferredLanguage);
        }

        if (intent is AiCopilotIntent.ReportGeneration
            || ContainsAny(text, "inventory report", "stock report", "warehouse report")
            || (ContainsAny(text, "inventory", "stock") && ContainsAny(text, "report", "pdf", "export")))
        {
            return new AgentWorkflowPlanDto
            {
                Title = "Inventory intelligence report",
                AgentKey = key,
                Intent = AiCopilotIntent.ReportGeneration,
                Steps =
                [
                    Step("read_inventory", "Read inventory levels", 0, AiToolName.GetInventorySummary),
                    Step("analyze_demand", "Analyze demand & low stock", 1, AiToolName.GetLowStock),
                    Step("generate_charts", "Build purchase recommendations", 2, AiToolName.GetPurchaseRecommendations),
                    Step("create_pdf", "Generate inventory report", 3, AiToolName.GenerateInventoryReport),
                    Step("summarize", "Summarize findings", 4, null)
                ]
            };
        }

        if (ContainsAny(text, "sales report", "revenue report")
            || (ContainsAny(text, "sales", "revenue") && ContainsAny(text, "report", "pdf", "export")))
        {
            return new AgentWorkflowPlanDto
            {
                Title = "Sales revenue report",
                AgentKey = key,
                Intent = AiCopilotIntent.ReportGeneration,
                Steps =
                [
                    Step("read_sales", "Read sales & revenue", 0, AiToolName.GetSalesSummary),
                    Step("analyze_trends", "Analyze trends", 1, AiToolName.GetSalesTrends),
                    Step("create_pdf", "Generate sales report", 2, AiToolName.GenerateSalesReport),
                    Step("summarize", "Summarize findings", 3, null)
                ]
            };
        }

        if (ContainsAny(text, "purchase order", "create po", "draft po")
            || (ContainsAny(text, "reorder") && ContainsAny(text, "create", "draft", "purchase")))
        {
            return new AgentWorkflowPlanDto
            {
                Title = "Purchase order draft",
                AgentKey = key,
                Intent = AiCopilotIntent.ActionCreate,
                Steps =
                [
                    Step("read_inventory", "Check low stock", 0, AiToolName.GetLowStock),
                    Step("analyze_demand", "Build purchase recommendations", 1, AiToolName.GetPurchaseRecommendations),
                    Step("create_po", "Create purchase order draft", 2, AiToolName.CreatePurchaseOrderDraft),
                    Step("summarize", "Confirm draft", 3, null)
                ]
            };
        }

        return new AgentWorkflowPlanDto
        {
            Title = "Agent workflow",
            AgentKey = key,
            Intent = intent is AiCopilotIntent.Unknown ? AiCopilotIntent.Workflow : intent,
            Steps =
            [
                Step("read_inventory", "Gather business context", 0, AiToolName.GetInventorySummary),
                Step("analyze_demand", "Analyze signals", 1, AiToolName.GetPurchaseRecommendations),
                Step("summarize", "Summarize next steps", 2, null)
            ]
        };
    }

    public AgentWorkflowPlanDto PlanOnboarding(string agentKey, string? language)
    {
        var key = AgentKeys.Normalize(agentKey);
        var isUrdu = AgentLanguages.Normalize(language) == AgentLanguages.Urdu;

        return new AgentWorkflowPlanDto
        {
            Title = isUrdu ? "کاروبار سیٹ اپ" : "Business onboarding",
            AgentKey = key,
            Intent = AiCopilotIntent.Onboarding,
            Steps =
            [
                Step("welcome", isUrdu ? "خوش آمدید" : "Welcome", 0, null),
                Step("business_name", isUrdu ? "کاروبار کا نام" : "Business name", 1, null),
                Step("industry", isUrdu ? "صنعت" : "Industry", 2, null),
                Step("size", isUrdu ? "کاروبار کا سائز" : "Business size", 3, null),
                Step("currency", isUrdu ? "کرنسی" : "Currency", 4, null),
                Step("country_timezone", isUrdu ? "ملک اور ٹائم زون" : "Country & timezone", 5, null),
                Step("tax", isUrdu ? "ٹیکس" : "Tax defaults", 6, null),
                Step("warehouse", isUrdu ? "گودام" : "Warehouse defaults", 7, null),
                Step("categories", isUrdu ? "زمرے" : "Product categories", 8, null),
                Step("confirm_apply", isUrdu ? "تصدیق اور اطلاق" : "Confirm & apply", 9, AiToolName.ApplyOnboardingProfile)
            ]
        };
    }

    private static AgentPlannedStepDto Step(
        string key,
        string title,
        int sortOrder,
        AiToolName? tool) =>
        new()
        {
            StepKey = key,
            Title = title,
            SortOrder = sortOrder,
            ToolName = tool
        };

    private static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
}
