using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.AI.Services;

namespace BusinessOS.Infrastructure.AI.Copilot;

public sealed class AiIntentDetector : IAiIntentDetector
{
    public AiIntentDetectionResult Detect(string message, AiPageContextDto page, AiMemoryStateDto memory)
    {
        var text = message.Trim().ToLowerInvariant();
        var tools = new List<AiToolName>();

        if (IsGreetingOrSmallTalk(text))
        {
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.Conversational,
                Confidence = 0.95
            };
        }

        if (ContainsAny(text, "help", "how do i", "how to", "getting started", "what can you"))
        {
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.Help,
                Confidence = 0.9
            };
        }

        if (IsFollowUp(text, memory))
        {
            tools.AddRange(InferToolsFromMemory(memory));
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.FollowUp,
                Confidence = 0.85,
                SuggestedTools = tools
            };
        }

        if (ContainsAny(text, "focus today", "what should i", "priorities", "dashboard", "actionable"))
        {
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.DashboardInsight,
                Confidence = 0.9
            };
        }

        if (ContainsAny(text, "policy", "handbook", "contract", "faq", "terms", "documentation", "document", "uploaded"))
        {
            tools.Add(AiToolName.SearchDocuments);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.DocumentSearch,
                Confidence = 0.88,
                SuggestedTools = tools
            };
        }

        if (ContainsAny(text, "create", "add", "new", "generate"))
        {
            if (ContainsAny(text, "customer")) tools.Add(AiToolName.CreateCustomer);
            if (ContainsAny(text, "project", "order")) tools.Add(AiToolName.CreateProject);
            if (ContainsAny(text, "task")) tools.Add(AiToolName.CreateTask);
            if (ContainsAny(text, "invoice")) tools.Add(AiToolName.CreateInvoice);

            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.ActionCreate,
                Confidence = 0.9,
                SuggestedTools = tools
            };
        }

        if (ContainsAny(text, "revenue", "sales", "sold", "profit", "growth", "trend", "compare", "monthly", "quarterly", "yearly", "analytics"))
        {
            if (ContainsAny(text, "sold", "sales", "products sold", "orders"))
                tools.Add(AiToolName.GetSalesSummary);
            else
                tools.Add(AiToolName.GetRevenue);

            if (ContainsAny(text, "top customer", "best customer", "highest revenue"))
                tools.Add(AiToolName.GetCustomers);

            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.Analytics,
                Confidence = 0.92,
                SuggestedTools = tools
            };
        }

        if (ContainsAny(text, "overdue", "unpaid", "outstanding", "invoice"))
        {
            tools.Add(AiToolName.GetInvoices);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.BusinessIntelligence,
                Confidence = 0.9,
                SuggestedTools = tools
            };
        }

        if (ContainsAny(text, "expense", "spending", "cost"))
        {
            tools.Add(AiToolName.GetExpenses);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.BusinessIntelligence,
                Confidence = 0.88,
                SuggestedTools = tools
            };
        }

        if (ContainsAny(text, "project", "behind schedule", "delayed", "progress"))
        {
            tools.Add(AiToolName.GetProjects);
            if (ContainsAny(text, "task", "delayed", "pending"))
                tools.Add(AiToolName.GetTasks);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.BusinessIntelligence,
                Confidence = 0.88,
                SuggestedTools = tools
            };
        }

        if (ContainsAny(text, "customer", "summarize", "activity"))
        {
            tools.Add(AiToolName.GetCustomers);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.BusinessIntelligence,
                Confidence = 0.85,
                SuggestedTools = tools
            };
        }

        if (ContainsAny(text, "product", "inventory", "catalog"))
        {
            tools.Add(AiToolName.GetProducts);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.ActionRead,
                Confidence = 0.85,
                SuggestedTools = tools
            };
        }

        if (ContainsAny(text, "task", "todo", "pending"))
        {
            tools.Add(AiToolName.GetTasks);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.BusinessIntelligence,
                Confidence = 0.85,
                SuggestedTools = tools
            };
        }

        tools.Add(AiToolName.GetSalesSummary);
        return new AiIntentDetectionResult
        {
            Intent = AiCopilotIntent.BusinessIntelligence,
            Confidence = 0.5,
            SuggestedTools = tools
        };
    }

    private static bool IsGreetingOrSmallTalk(string text) =>
        text.Length < 40 && ContainsAny(text, "hi", "hello", "hey", "thanks", "thank you", "good morning", "good afternoon");

    private static bool IsFollowUp(string text, AiMemoryStateDto memory) =>
        memory.RecentTurns.Count > 0
        && (ContainsAny(text, "last year", "last month", "what about", "same for", "and for", "how about", "that customer", "them", "those")
            || (text.Split(' ').Length <= 6 && memory.SelectedCustomerId is not null));

    private static IReadOnlyList<AiToolName> InferToolsFromMemory(AiMemoryStateDto memory)
    {
        if (memory.LastAnalyticsQuery?.Contains("revenue", StringComparison.OrdinalIgnoreCase) == true)
            return [AiToolName.GetRevenue, AiToolName.GetCustomers];
        if (memory.SelectedCustomerId is not null)
            return [AiToolName.GetCustomers, AiToolName.GetInvoices];
        return [AiToolName.GetSalesSummary];
    }

    private static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
}
