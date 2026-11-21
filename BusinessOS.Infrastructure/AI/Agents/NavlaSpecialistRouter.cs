using BusinessOS.Application.Features.Agents;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Infrastructure.AI;

namespace BusinessOS.Infrastructure.AI.Agents;

/// <summary>
/// Routes a Navla chat turn to one or more domain specialists and intersects tools.
/// Does not invent tools; only scopes the existing registry.
/// </summary>
public static class NavlaSpecialistRouter
{
    private const int MaxSpecialists = 3;

    public static NavlaSpecialistRoute Route(
        string message,
        AiIntentDetectionResult intent)
    {
        var text = ConversationLanguage.ExpandForRouting(message).Trim().ToLowerInvariant();
        var matched = new List<string>();

        if (LooksLikeKnowledge(text, intent))
            matched.Add(NavlaSpecialistKeys.Knowledge);
        if (LooksLikeInventory(text, intent))
            matched.Add(NavlaSpecialistKeys.Inventory);
        if (LooksLikeSales(text, intent))
            matched.Add(NavlaSpecialistKeys.Sales);
        if (LooksLikeOrders(text, intent))
            matched.Add(NavlaSpecialistKeys.Orders);
        if (LooksLikeCustomers(text, intent))
            matched.Add(NavlaSpecialistKeys.Customers);
        if (LooksLikeFinance(text, intent))
            matched.Add(NavlaSpecialistKeys.Finance);

        if (matched.Count == 0)
            matched.Add(NavlaSpecialistKeys.BusinessAssistant);

        if (matched.Count > MaxSpecialists)
            matched = matched.Take(MaxSpecialists).ToList();

        var allowed = UnionTools(matched);
        if (intent.SuggestedTools.Count > 0)
        {
            var intersection = intent.SuggestedTools.Where(allowed.Contains).ToList();
            // Keep detector tools when they fall outside the specialist map (e.g. report tools).
            if (intersection.Count == 0)
                allowed = intent.SuggestedTools.ToHashSet();
            else
                allowed = intersection.Concat(intent.SuggestedTools.Where(t => !allowed.Contains(t))).ToHashSet();
        }

        var label = matched.Count == 1
            ? NavlaSpecialistCatalog.DisplayName(matched[0])
            : string.Join(" + ", matched.Select(NavlaSpecialistCatalog.DisplayName));

        return new NavlaSpecialistRoute(
            matched,
            allowed.ToList(),
            label,
            matched.Count > 1);
    }

    public static AiIntentDetectionResult Apply(
        AiIntentDetectionResult intent,
        NavlaSpecialistRoute route)
    {
        if (intent.SuggestedTools.Count == 0 || route.AllowedTools.Count == 0)
            return intent;

        var scoped = intent.SuggestedTools
            .Where(t => route.AllowedTools.Contains(t))
            .ToList();

        if (scoped.Count == 0)
            scoped = intent.SuggestedTools.ToList();

        // Multi-module: ensure each specialist contributes at least one suggested read when present.
        if (route.IsMultiModule)
        {
            foreach (var key in route.SpecialistKeys)
            {
                if (!NavlaSpecialistCatalog.ToolsBySpecialist.TryGetValue(key, out var tools))
                    continue;
                var extra = tools.FirstOrDefault(t =>
                    route.AllowedTools.Contains(t)
                    && !AiToolSafetyGate.IsMutating(t)
                    && !scoped.Contains(t));
                if (scoped.Count < 6
                    && route.AllowedTools.Contains(extra)
                    && !AiToolSafetyGate.IsMutating(extra)
                    && !scoped.Contains(extra))
                    scoped.Add(extra);
            }
        }

        return new AiIntentDetectionResult
        {
            Intent = intent.Intent,
            Confidence = intent.Confidence,
            SuggestedTools = scoped
        };
    }

    private static HashSet<AiToolName> UnionTools(IEnumerable<string> keys)
    {
        var set = new HashSet<AiToolName>();
        foreach (var key in keys)
        {
            if (NavlaSpecialistCatalog.ToolsBySpecialist.TryGetValue(key, out var tools))
            {
                foreach (var tool in tools)
                    set.Add(tool);
            }
        }

        return set;
    }

    private static bool LooksLikeKnowledge(string text, AiIntentDetectionResult intent) =>
        intent.Intent is AiCopilotIntent.DocumentSearch
        || intent.SuggestedTools.Contains(AiToolName.SearchDocuments)
        || ContainsAny(text, "policy", "document", "knowledge base", "uploaded", "handbook", "sop", "manual");

    private static bool LooksLikeInventory(string text, AiIntentDetectionResult intent) =>
        intent.SuggestedTools.Any(t => t is AiToolName.GetLowStock or AiToolName.GetInventorySummary
            or AiToolName.GetProducts or AiToolName.GetOutOfStock or AiToolName.GetDeadStock
            or AiToolName.GetInventoryHealth or AiToolName.GetStockHistory or AiToolName.AdjustInventory
            or AiToolName.CreateProduct or AiToolName.SearchProduct)
        || ContainsAny(text, "stock", "inventory", "warehouse", "sku", "reorder", "out of stock", "low stock",
            "baqi", "stock baqi", "product catalog");

    private static bool LooksLikeSales(string text, AiIntentDetectionResult intent) =>
        intent.SuggestedTools.Any(t => t is AiToolName.GetSalesSummary or AiToolName.GetRevenue
            or AiToolName.GetBestSellingProducts or AiToolName.GetSalesTrends or AiToolName.GetSaleDetails
            or AiToolName.GenerateSalesReport)
        || (ContainsAny(text, "sales", "sale", "revenue", "earn", "earning", "earned", "top selling",
                "best selling", "highest-selling", "highest selling", "kamaya", "hui hain")
            && !ContainsAny(text, "purchase order"));

    private static bool LooksLikeOrders(string text, AiIntentDetectionResult intent) =>
        intent.SuggestedTools.Any(t => t is AiToolName.GetOrders or AiToolName.GetOrderDetails
            or AiToolName.GetPendingOrders or AiToolName.UpdateOrderStatus or AiToolName.CreateSale)
        || (ContainsAny(text, "order", "orders", "pending order", "order status")
            && !ContainsAny(text, "purchase order"));

    private static bool LooksLikeCustomers(string text, AiIntentDetectionResult intent) =>
        intent.SuggestedTools.Any(t => t is AiToolName.GetCustomers or AiToolName.SearchCustomer
            or AiToolName.GetCustomerInsights or AiToolName.GetCustomerPurchaseHistory
            or AiToolName.CreateCustomer)
        || ContainsAny(text, "customer", "clients", "buyer", "repeat customer", "purchase history");

    private static bool LooksLikeFinance(string text, AiIntentDetectionResult intent) =>
        intent.SuggestedTools.Any(t => t is AiToolName.GetExpenses or AiToolName.ShowProfit
            or AiToolName.GetInvoices or AiToolName.GetFinancialSummary or AiToolName.GetOutstandingPayments
            or AiToolName.CreateExpense or AiToolName.GetRevenue)
        || ContainsAny(text, "expense", "profit", "invoice", "outstanding", "payment", "finance",
            "receivable", "payable", "kharcha", "munafa", "earn", "earned", "earning", "how much did i");

    private static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
}
