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

        // Agent employee intents first (before advice/strategy classification).
        if (IsOnboardingRequest(content))
        {
            tools.Add(AiToolName.ApplyOnboardingProfile);
            tools.Add(AiToolName.GetBusinessSettings);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.Onboarding,
                Confidence = 0.94,
                SuggestedTools = tools
            };
        }

        if (IsInventoryReportRequest(content))
        {
            tools.Add(AiToolName.GenerateInventoryReport);
            tools.Add(AiToolName.GetInventorySummary);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.ReportGeneration,
                Confidence = 0.94,
                SuggestedTools = tools
            };
        }

        if (IsSalesReportRequest(content))
        {
            tools.Add(AiToolName.GenerateSalesReport);
            tools.Add(AiToolName.GetSalesSummary);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.ReportGeneration,
                Confidence = 0.93,
                SuggestedTools = tools
            };
        }

        if (IsPurchaseOrderCreateRequest(content))
        {
            tools.Add(AiToolName.CreatePurchaseOrderDraft);
            tools.Add(AiToolName.GetLowStock);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.ActionCreate,
                Confidence = 0.93,
                SuggestedTools = tools
            };
        }

        if (IsLowOrDeadStockRequest(content))
        {
            if (ContainsAny(content, "dead stock", "slow moving", "unsold", "stale stock"))
                tools.Add(AiToolName.GetDeadStock);
            if (ContainsAny(content, "low stock", "reorder", "running low", "stock alert", "out of stock"))
                tools.Add(AiToolName.GetLowStock);

            if (tools.Count == 0)
                tools.Add(AiToolName.GetLowStock);

            var intent = ContainsAny(content, "recommend", "should i buy", "what should i")
                ? AiCopilotIntent.Recommendation
                : AiCopilotIntent.ActionRead;

            return new AiIntentDetectionResult
            {
                Intent = intent,
                Confidence = 0.92,
                SuggestedTools = tools
            };
        }

        if (IsPurchaseRecommendationRequest(content))
        {
            tools.Add(AiToolName.GetPurchaseRecommendations);
            tools.Add(AiToolName.GetLowStock);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.Recommendation,
                Confidence = 0.92,
                SuggestedTools = tools
            };
        }

        // Advice / strategy questions ("how can I increase sales?") must NOT become Analytics lookups.
        // Orchestrator will still ground advice with live sales tools when relevant.
        if (IsAdviceRequest(content))
        {
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.Conversational,
                Confidence = 0.93,
                SuggestedTools = InferAdviceGroundingTools(content)
            };
        }

        // Sales analytics before Help — "show top selling…" contains the substring "how to".
        if (IsBestSellerQuery(content))
        {
            tools.Add(AiToolName.GetBestSellingProducts);
            tools.Add(AiToolName.GetSalesSummary);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.Analytics,
                Confidence = 0.94,
                SuggestedTools = tools
            };
        }

        if (IsTrendQuery(content))
        {
            tools.Add(AiToolName.GetSalesTrends);
            tools.Add(AiToolName.GetRevenue);
            tools.Add(AiToolName.GetSalesSummary);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.Analytics,
                Confidence = 0.93,
                SuggestedTools = tools
            };
        }

        if (IsHelpRequest(content))
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

        if (ContainsAny(content, "policy", "handbook", "contract", "faq", "terms", "documentation", "document", "uploaded", "knowledge base", "sop"))
        {
            tools.Add(AiToolName.SearchDocuments);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.DocumentSearch,
                Confidence = 0.88,
                SuggestedTools = tools
            };
        }

        if (ContainsAny(content, "create", "add", "new", "generate", "register"))
        {
            if (ContainsAny(content, "customer", "client")) tools.Add(AiToolName.CreateCustomer);
            if (ContainsAny(content, "product", "item")) tools.Add(AiToolName.CreateProduct);
            if (ContainsAny(content, "supplier", "vendor")) tools.Add(AiToolName.CreateSupplier);
            if (ContainsAny(content, "project")) tools.Add(AiToolName.CreateProject);
            if (ContainsAny(content, "sale") || (ContainsAny(content, "order") && !ContainsAny(content, "purchase")))
                tools.Add(AiToolName.CreateSale);
            if (ContainsAny(content, "task")) tools.Add(AiToolName.CreateTask);
            if (ContainsAny(content, "invoice")) tools.Add(AiToolName.CreateInvoice);
            if (ContainsAny(content, "purchase order", "po ")) tools.Add(AiToolName.CreatePurchaseOrder);

            if (tools.Count > 0)
            {
                return new AiIntentDetectionResult
                {
                    Intent = AiCopilotIntent.ActionCreate,
                    Confidence = 0.9,
                    SuggestedTools = tools
                };
            }
        }

        if (ContainsAny(content, "update", "edit", "change", "modify"))
        {
            if (ContainsAny(content, "customer", "client")) tools.Add(AiToolName.UpdateCustomer);
            if (ContainsAny(content, "product")) tools.Add(AiToolName.UpdateProduct);
            if (ContainsAny(content, "supplier")) tools.Add(AiToolName.UpdateSupplier);
            if (ContainsAny(content, "company", "business", "profile")) tools.Add(AiToolName.UpdateCompanyProfile);
            if (ContainsAny(content, "tax", "currency")) tools.Add(AiToolName.UpdateTaxDefaults);
            if (tools.Count > 0)
            {
                return new AiIntentDetectionResult
                {
                    Intent = AiCopilotIntent.ActionCreate,
                    Confidence = 0.88,
                    SuggestedTools = tools
                };
            }
        }

        if (ContainsAny(content, "delete", "remove"))
        {
            if (ContainsAny(content, "customer", "client")) tools.Add(AiToolName.DeleteCustomer);
            if (ContainsAny(content, "product")) tools.Add(AiToolName.DeleteProduct);
            if (ContainsAny(content, "supplier")) tools.Add(AiToolName.DeleteSupplier);
            if (tools.Count > 0)
            {
                return new AiIntentDetectionResult
                {
                    Intent = AiCopilotIntent.ActionCreate,
                    Confidence = 0.88,
                    SuggestedTools = tools
                };
            }
        }

        if (ContainsAny(content, "search", "find", "look up"))
        {
            if (ContainsAny(content, "customer", "client")) tools.Add(AiToolName.SearchCustomer);
            if (ContainsAny(content, "product")) tools.Add(AiToolName.SearchProduct);
            if (ContainsAny(content, "supplier")) tools.Add(AiToolName.SearchSupplier);
            if (ContainsAny(content, "invoice")) tools.Add(AiToolName.SearchInvoice);
            if (tools.Count > 0)
            {
                return new AiIntentDetectionResult
                {
                    Intent = AiCopilotIntent.ActionRead,
                    Confidence = 0.9,
                    SuggestedTools = tools
                };
            }
        }

        if (ContainsAny(content, "adjust stock", "adjust inventory"))
        {
            tools.Add(AiToolName.AdjustInventory);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.ActionCreate,
                Confidence = 0.9,
                SuggestedTools = tools
            };
        }

        if (ContainsAny(content, "receive stock", "receive inventory"))
        {
            tools.Add(AiToolName.ReceiveStock);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.ActionCreate,
                Confidence = 0.9,
                SuggestedTools = tools
            };
        }

        if (ContainsAny(content, "show profit", "net profit", "gross profit", "p&l", "profit and loss"))
        {
            tools.Add(AiToolName.ShowProfit);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.ActionRead,
                Confidence = 0.92,
                SuggestedTools = tools
            };
        }

        if (ContainsAny(content, "cancel invoice", "void invoice"))
        {
            tools.Add(AiToolName.CancelInvoice);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.ActionCreate,
                Confidence = 0.9,
                SuggestedTools = tools
            };
        }

        if (ContainsAny(content, "approve purchase", "approve po"))
        {
            tools.Add(AiToolName.ApprovePurchaseOrder);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.ActionCreate,
                Confidence = 0.9,
                SuggestedTools = tools
            };
        }

        if (ContainsAny(content, "receive purchase", "receive po"))
        {
            tools.Add(AiToolName.ReceivePurchase);
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.ActionCreate,
                Confidence = 0.9,
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

            if (ContainsAny(content, "product"))
                tools.Add(AiToolName.GetBestSellingProducts);

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

        if (ContainsAny(content, "product", "inventory", "catalog", "sku"))
        {
            // Prefer inventory summary when the ask is about stock levels broadly.
            if (ContainsAny(content, "inventory", "stock", "warehouse"))
                tools.Add(AiToolName.GetInventorySummary);
            else
                tools.Add(AiToolName.GetProducts);

            if (ContainsAny(content, "sell", "sold", "performance", "ranking"))
                tools.Add(AiToolName.GetBestSellingProducts);
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

    private static bool IsOnboardingRequest(string text) =>
        ContainsAny(text,
            "onboard", "onboarding",
            "setup company", "set up company", "set up my business", "set up our business",
            "configure my business", "start setup", "business setup wizard");

    private static bool IsInventoryReportRequest(string text) =>
        ContainsAny(text, "inventory report", "stock report", "warehouse report")
        || (ContainsAny(text, "inventory", "stock", "warehouse")
            && ContainsAny(text, "report", "pdf", "export"));

    private static bool IsSalesReportRequest(string text) =>
        ContainsAny(text, "sales report", "revenue report")
        || (ContainsAny(text, "sales", "revenue")
            && ContainsAny(text, "report", "pdf", "export")
            && !IsBestSellerQuery(text)
            && !IsTrendQuery(text));

    private static bool IsPurchaseOrderCreateRequest(string text) =>
        (ContainsAny(text, "purchase order", "create po", "draft po", "purchase-order")
         && ContainsAny(text, "create", "add", "new", "generate", "draft", "make", "prepare"))
        || ContainsAny(text, "create a purchase order", "create purchase order", "draft purchase order");

    private static bool IsLowOrDeadStockRequest(string text) =>
        ContainsAny(text,
            "low stock", "dead stock", "slow moving", "reorder level",
            "running low", "stock alert", "out of stock", "unsold", "stale stock");

    private static bool IsPurchaseRecommendationRequest(string text) =>
        ContainsAny(text,
            "what should i buy", "what should we buy", "purchase recommend",
            "reorder recommend", "buying recommend", "suggest purchases", "what to reorder");

    private static bool IsHelpRequest(string text)
    {
        // Avoid naive substring matches like "how to" inside "show top".
        if (ContainsPhrase(text, "getting started") || ContainsPhrase(text, "what can you"))
            return true;

        if (ContainsPhrase(text, "how do i") || ContainsPhrase(text, "how to") || ContainsPhrase(text, "how can i"))
        {
            // App how-tos, not sales analytics / strategy.
            return !IsBestSellerQuery(text) && !IsTrendQuery(text) && !IsAdviceRequest(text);
        }

        return ContainsPhrase(text, "help me") || text is "help" or "help?" or "help!";
    }

    private static bool ContainsPhrase(string text, string phrase)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(phrase))
            return false;

        var index = 0;
        while ((index = text.IndexOf(phrase, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var beforeOk = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            var afterIndex = index + phrase.Length;
            var afterOk = afterIndex >= text.Length || !char.IsLetterOrDigit(text[afterIndex]);
            if (beforeOk && afterOk)
                return true;
            index += phrase.Length;
        }

        return false;
    }

    private static bool IsBestSellerQuery(string text) =>
        ContainsAny(text,
            "best selling", "best-selling", "bestseller", "best seller",
            "top product", "top selling", "most sold", "highest selling",
            "which product is best", "popular product", "fastest selling");

    private static bool IsTrendQuery(string text) =>
        ContainsAny(text,
            "trend", "future trend", "sales trend", "growth trend",
            "are sales growing", "is revenue growing", "declining",
            "compare last month", "vs last month", "versus last",
            "forecast", "what does the future", "momentum");

    private static IReadOnlyList<AiToolName> InferAdviceGroundingTools(string text)
    {
        var tools = new List<AiToolName>();
        if (ContainsAny(text, "sales", "sell", "revenue", "product", "customer", "grow", "increase", "boost"))
        {
            tools.Add(AiToolName.GetSalesSummary);
            tools.Add(AiToolName.GetBestSellingProducts);
            tools.Add(AiToolName.GetSalesTrends);
        }

        return tools;
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
            "top customer", "best customer", "by revenue", "products sold",
            "best selling", "bestseller", "top product", "trend");

    private static bool IsFollowUp(string text, AiMemoryStateDto memory) =>
        memory.RecentTurns.Count > 0
        && (ContainsAny(text, "last year", "last month", "what about", "same for", "and for", "how about", "that customer", "them", "those")
            || (text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 6 && memory.SelectedCustomerId is not null));

    private static IReadOnlyList<AiToolName> InferToolsFromMemory(AiMemoryStateDto memory)
    {
        if (memory.LastAnalyticsQuery?.Contains("revenue", StringComparison.OrdinalIgnoreCase) == true)
            return [AiToolName.GetRevenue, AiToolName.GetCustomers];
        if (memory.LastAnalyticsQuery?.Contains("product", StringComparison.OrdinalIgnoreCase) == true
            || memory.LastAnalyticsQuery?.Contains("best", StringComparison.OrdinalIgnoreCase) == true)
            return [AiToolName.GetBestSellingProducts, AiToolName.GetSalesSummary];
        if (memory.SelectedCustomerId is not null)
            return [AiToolName.GetCustomers, AiToolName.GetInvoices];
        return [AiToolName.GetSalesSummary];
    }

    private static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
}
