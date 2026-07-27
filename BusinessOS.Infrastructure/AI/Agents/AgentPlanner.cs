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
            && ContainsAny(text, "create", "draft", "generate", "make", "prepare", "buy", "order"))
            return true;

        // Named product purchase: "create PO for Laptop", "order 5 laptops from supplier"
        if (ContainsAny(text, "purchase order", "create po", "draft po")
            || (ContainsAny(text, "buy stock", "reorder stock", "order stock")))
            return true;

        if (ContainsAny(text, "multi-step", "workflow", "end to end", "full report"))
            return true;

        if (LooksLikeCustomerThenInvoice(text))
            return true;

        if (ContainsAny(text, " then ", " and then ", "after that"))
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

        if (LooksLikeCustomerThenInvoice(text))
        {
            return new AgentWorkflowPlanDto
            {
                Title = "Create customer and invoice",
                AgentKey = key,
                Intent = AiCopilotIntent.Workflow,
                Steps =
                [
                    Step("create_customer", "Create customer", 0, AiToolName.CreateCustomer),
                    Step("create_sale", "Create sale for customer", 1, AiToolName.CreateSale),
                    Step("create_invoice", "Create invoice", 2, AiToolName.CreateInvoice),
                    Step("summarize", "Confirm completion", 3, null)
                ]
            };
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

        if (ContainsAny(text, "purchase order", "create po", "draft po", "buy stock", "order stock")
            || (ContainsAny(text, "reorder") && ContainsAny(text, "create", "draft", "purchase", "buy", "order")))
        {
            var namedProduct = MessageNamesProduct(text);
            if (namedProduct)
            {
                // User already named what to buy — skip recommendation workflow and create directly.
                return new AgentWorkflowPlanDto
                {
                    Title = "Create purchase order",
                    AgentKey = key,
                    Intent = AiCopilotIntent.ActionCreate,
                    Steps =
                    [
                        Step("create_po", "Create purchase order", 0, AiToolName.CreatePurchaseOrder),
                        Step("summarize", "Confirm draft", 1, null)
                    ]
                };
            }

            // Generic "create purchase order" → draft from low stock (works without line items).
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

        // Generic multi-step: use suggested create tools in sequence when "then" is present.
        if (ContainsAny(text, " then ", " and then ", "after that"))
        {
            var steps = new List<AgentPlannedStepDto>();
            var order = 0;
            if (ContainsAny(text, "customer", "client"))
                steps.Add(Step("create_customer", "Create customer", order++, AiToolName.CreateCustomer));
            if (ContainsAny(text, "product"))
                steps.Add(Step("create_product", "Create product", order++, AiToolName.CreateProduct));
            if (ContainsAny(text, "sale", "order") && !ContainsAny(text, "purchase"))
                steps.Add(Step("create_sale", "Create sale", order++, AiToolName.CreateSale));
            if (ContainsAny(text, "invoice"))
                steps.Add(Step("create_invoice", "Create invoice", order++, AiToolName.CreateInvoice));
            if (ContainsAny(text, "supplier"))
                steps.Add(Step("create_supplier", "Create supplier", order++, AiToolName.CreateSupplier));
            steps.Add(Step("summarize", "Confirm completion", order, null));

            if (steps.Count > 1)
            {
                return new AgentWorkflowPlanDto
                {
                    Title = "Multi-step business workflow",
                    AgentKey = key,
                    Intent = AiCopilotIntent.Workflow,
                    Steps = steps
                };
            }
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

        return new AgentWorkflowPlanDto
        {
            Title = "Business onboarding",
            AgentKey = key,
            Intent = AiCopilotIntent.Onboarding,
            Steps =
            [
                Step("welcome", "Welcome", 0, null),
                Step("business_name", "Business name", 1, null),
                Step("industry", "Industry", 2, null),
                Step("size", "Business size", 3, null),
                Step("currency", "Currency", 4, null),
                Step("country_timezone", "Country & timezone", 5, null),
                Step("tax", "Tax defaults", 6, null),
                Step("warehouse", "Warehouse defaults", 7, null),
                Step("categories", "Product categories", 8, null),
                Step("confirm_apply", "Confirm & apply", 9, AiToolName.ApplyOnboardingProfile)
            ]
        };
    }

    private static bool LooksLikeCustomerThenInvoice(string text) =>
        (ContainsAny(text, "customer", "client") && ContainsAny(text, "invoice"))
        || (ContainsAny(text, "create customer") && ContainsAny(text, "sale", "order", "invoice"));

    private static bool MessageNamesProduct(string text)
    {
        // Strip common PO phrasing; if meaningful tokens remain, treat as a named product.
        var stripped = text;
        foreach (var phrase in new[]
                 {
                     "create a purchase order", "create purchase order", "draft purchase order",
                     "create po", "draft po", "purchase order", "buy stock", "order stock",
                     "reorder items", "reorder", "low stock", "from low stock",
                     "create", "draft", "generate", "make", "prepare", "new", "please",
                     "for me", "for us"
                 })
        {
            stripped = stripped.Replace(phrase, " ", StringComparison.OrdinalIgnoreCase);
        }

        stripped = System.Text.RegularExpressions.Regex.Replace(stripped, @"\b(for|of|from|supplier|vendor|quantity|qty|units?|and|the|a|an|items?|products?|recommendations?|stock)\b", " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        stripped = System.Text.RegularExpressions.Regex.Replace(stripped, @"\d+(?:\.\d+)?", " ");
        stripped = System.Text.RegularExpressions.Regex.Replace(stripped, @"\s+", " ").Trim();
        return stripped.Length >= 2;
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
