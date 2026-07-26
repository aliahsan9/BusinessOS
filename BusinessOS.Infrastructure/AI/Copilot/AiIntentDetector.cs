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

        // Strip leading "hi," / "hello" so compound messages can be classified correctly.
        var content = StripLeadingGreeting(text);

        // Advice / strategy questions ("how can I increase sales?") must NOT become Analytics lookups.
        if (IsAdviceRequest(content))
        {
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.Conversational,
                Confidence = 0.93
            };
        }

        if (ContainsAny(content, "help", "how do i", "how to", "how can i", "getting started", "what can you"))
        {
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.Help,
                Confidence = 0.9
            };
        }

        if (IsFollowUp(content, memory))
        {
            tools.AddRange(InferToolsFromMemory(memory));
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.FollowUp,
                Confidence = 0.85,
                SuggestedTools = tools
            };
        }

        if (ContainsAny(content, "focus today", "what should i focus", "priorities", "dashboard insight", "actionable"))
        {
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.DashboardInsight,
                Confidence = 0.9
            };
        }

        if (ContainsAny(content, "policy", "handbook", "contract", "faq", "terms", "documentation", "document", "uploaded"))
        {
            tools.Add(AiToolName.SearchDocuments);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.DocumentSearch,
                Confidence = 0.88,
                SuggestedTools = tools
            };
        }

        if (ContainsAny(content, "create", "add", "new", "generate"))
        {
            if (ContainsAny(content, "customer")) tools.Add(AiToolName.CreateCustomer);
            if (ContainsAny(content, "project", "order")) tools.Add(AiToolName.CreateProject);
            if (ContainsAny(content, "task")) tools.Add(AiToolName.CreateTask);
            if (ContainsAny(content, "invoice")) tools.Add(AiToolName.CreateInvoice);

            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.ActionCreate,
                Confidence = 0.9,
                SuggestedTools = tools
            };
        }

        if (ContainsAny(content, "revenue", "sales", "sold", "profit", "growth", "trend", "compare", "monthly", "quarterly", "yearly", "analytics")
            && IsFactualDataLookup(content))
        {
            if (ContainsAny(content, "sold", "sales", "products sold", "orders"))
                tools.Add(AiToolName.GetSalesSummary);
            else
                tools.Add(AiToolName.GetRevenue);

            if (ContainsAny(content, "top customer", "best customer", "highest revenue"))
                tools.Add(AiToolName.GetCustomers);

            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.Analytics,
                Confidence = 0.92,
                SuggestedTools = tools
            };
        }

        if (ContainsAny(content, "overdue", "unpaid", "outstanding", "invoice"))
        {
            tools.Add(AiToolName.GetInvoices);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.BusinessIntelligence,
                Confidence = 0.9,
                SuggestedTools = tools
            };
        }

        if (ContainsAny(content, "expense", "spending", "cost"))
        {
            tools.Add(AiToolName.GetExpenses);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.BusinessIntelligence,
                Confidence = 0.88,
                SuggestedTools = tools
            };
        }

        if (ContainsAny(content, "project", "behind schedule", "delayed", "progress"))
        {
            tools.Add(AiToolName.GetProjects);
            if (ContainsAny(content, "task", "delayed", "pending"))
                tools.Add(AiToolName.GetTasks);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.BusinessIntelligence,
                Confidence = 0.88,
                SuggestedTools = tools
            };
        }

        if (ContainsAny(content, "customer", "summarize", "activity"))
        {
            tools.Add(AiToolName.GetCustomers);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.BusinessIntelligence,
                Confidence = 0.85,
                SuggestedTools = tools
            };
        }

        if (ContainsAny(content, "product", "inventory", "catalog"))
        {
            tools.Add(AiToolName.GetProducts);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.ActionRead,
                Confidence = 0.85,
                SuggestedTools = tools
            };
        }

        if (ContainsAny(content, "task", "todo", "pending"))
        {
            tools.Add(AiToolName.GetTasks);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.BusinessIntelligence,
                Confidence = 0.85,
                SuggestedTools = tools
            };
        }

        // Unknown / open chat — do not force sales tools.
        return new AiIntentDetectionResult
        {
            Intent = AiCopilotIntent.Conversational,
            Confidence = 0.5
        };
    }

    private static bool IsGreetingOrSmallTalk(string text)
    {
        if (ContainsAny(text, "thanks", "thank you", "bye", "goodbye"))
            return text.Length < 60;

        if (!ContainsAny(text, "hi", "hello", "hey", "good morning", "good afternoon", "good evening"))
            return false;

        // Pure greeting, or greeting + tiny filler ("hi there", "hello!").
        var withoutGreeting = StripLeadingGreeting(text);
        return string.IsNullOrWhiteSpace(withoutGreeting) || withoutGreeting.Length <= 12;
    }

    private static string StripLeadingGreeting(string text)
    {
        string[] prefixes =
        [
            "good morning", "good afternoon", "good evening",
            "hello there", "hey there", "hi there",
            "hello,", "hello!", "hello ",
            "hey,", "hey!", "hey ",
            "hi,", "hi!", "hi "
        ];

        foreach (var prefix in prefixes.OrderByDescending(p => p.Length))
        {
            if (text.StartsWith(prefix, StringComparison.Ordinal))
                return text[prefix.Length..].TrimStart(' ', ',', '!', '?', '.', ':');
        }

        return text;
    }

    /// <summary>
    /// Business advice / strategy questions should get a chatbot-style answer, not a data dump.
    /// </summary>
    private static bool IsAdviceRequest(string text)
    {
        // App/product how-tos belong to Help, not strategy advice.
        if (ContainsAny(text, "create", "add a", "add new", "set up", "setup", "configure", "in businessos", "in the app"))
            return false;

        var hasStrategyCue = ContainsAny(text,
            "increase", "improve", "boost", "grow my", "grow our",
            "tips", "advice", "strateg", "recommend", "suggestions",
            "ways to", "best way to", "what should i do to",
            "how to increase", "how to improve", "how to boost", "how to grow",
            "how do i increase", "how do i improve", "help me increase", "help me improve");

        if (!hasStrategyCue)
            return false;

        // Pure strategy questions always win; mixed "how can I increase … this month?" stays advice.
        var hasAdviceFraming = ContainsAny(text,
            "how can i", "how should i", "how do we", "how can we", "ways to", "tips", "advice", "strateg", "recommend");

        return !IsFactualDataLookup(text) || hasAdviceFraming;
    }

    private static bool IsFactualDataLookup(string text) =>
        ContainsAny(text,
            "how many", "what is my", "what's my", "what is our", "what's our", "what are my", "what are our",
            "show me", "show ", "list ", "report", "breakdown", "total",
            "this month", "last month", "this week", "last week", "this year", "last year", "ytd",
            "today", "yesterday", "quarter", "so far", "to date",
            "top customer", "best customer", "by revenue", "products sold");

    private static bool IsFollowUp(string text, AiMemoryStateDto memory) =>
        memory.RecentTurns.Count > 0
        && (ContainsAny(text, "last year", "last month", "what about", "same for", "and for", "how about", "that customer", "them", "those")
            || (text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 6 && memory.SelectedCustomerId is not null));

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
