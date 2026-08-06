using System.Text.RegularExpressions;
using BusinessOS.Application.Features.Agents.Services;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.AI.Services;

namespace BusinessOS.Infrastructure.AI.Agents.Runtime;

/// <summary>
/// Extends IAiIntentDetector with page-aware short commands for agent workflows.
/// </summary>
public sealed class AgentIntentParser : IAgentIntentParser
{
    private readonly IAiIntentDetector _detector;

    public AgentIntentParser(IAiIntentDetector detector) => _detector = detector;

    public AiIntentDetectionResult Parse(
        string message,
        AiPageContextDto page,
        AiMemoryStateDto memory,
        string? language = null)
    {
        var normalized = NormalizeBilingual(message);
        var pageAware = ExpandPageAwareShortCommand(normalized, page);

        // Clarification follow-ups (e.g. user replies "Laptop" after PO asked for a product).
        if (TryParseClarificationFollowUp(pageAware, memory) is { } followUp)
            return followUp;

        var result = _detector.Detect(pageAware, page, memory);

        // Enrich with employee write tools when intent is ActionCreate but detector only suggested legacy tools.
        if (result.Intent is AiCopilotIntent.ActionCreate)
            result = EnrichCreateTools(pageAware, result, page);

        if (result.Intent is AiCopilotIntent.ActionRead)
            result = EnrichReadTools(pageAware, result);

        return result;
    }

    private static AiIntentDetectionResult? TryParseClarificationFollowUp(string message, AiMemoryStateDto memory)
    {
        var lastAssistant = memory.RecentTurns
            .LastOrDefault(t => t.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
            ?.Content ?? "";
        if (string.IsNullOrWhiteSpace(lastAssistant))
            return null;

        var lowerAssistant = lastAssistant.ToLowerInvariant();
        var needsProduct = ContainsAny(lowerAssistant,
            "purchase line", "which product", "product should i order", "please provide: product",
            "at least one purchase", "how many", "example: \"create purchase order",
            "which product and how many");
        var needsSupplier = ContainsAny(lowerAssistant,
            "create a supplier", "no supplier", "supplier first", "what's the supplier")
            && ContainsAny(lowerAssistant, "supplier");
        var needsCustomer = ContainsAny(lowerAssistant,
            "please provide: name", "please provide: customer", "what's the customer's",
            "which customer");
        var needsCustomerForSale = ContainsAny(lowerAssistant,
            "which customer is this for", "customer is required");

        var trimmed = message.Trim();
        if (trimmed.Length is < 1 or > 120)
            return null;

        // Don't hijack clear new commands.
        if (ContainsAny(trimmed.ToLowerInvariant(),
                "create ", "add ", "delete ", "update ", "search ", "show ", "report", "hello", "hi ", "hey "))
            return null;

        var wordCount = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount > 8)
            return null;

        if (needsProduct)
        {
            var saleProduct = ContainsAny(lowerAssistant,
                "create order", "create a sale", "sale", "which product and how many");
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.ActionCreate,
                Confidence = 0.91,
                SuggestedTools = saleProduct ? [AiToolName.CreateSale] : [AiToolName.CreatePurchaseOrder]
            };
        }

        if (needsSupplier && memory.LastIntent?.Equals("ActionCreate", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.ActionCreate,
                Confidence = 0.9,
                SuggestedTools = [AiToolName.CreateSupplier]
            };
        }

        if (needsCustomerForSale)
        {
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.ActionCreate,
                Confidence = 0.9,
                SuggestedTools = [AiToolName.CreateSale]
            };
        }

        if (needsCustomer && memory.LastIntent?.Equals("ActionCreate", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new AiIntentDetectionResult
            {
                Intent = AiCopilotIntent.ActionCreate,
                Confidence = 0.9,
                SuggestedTools = [AiToolName.CreateCustomer]
            };
        }

        return null;
    }

    private static bool IsExplicitCreateCustomer(string text) =>
        Matches(text,
            "create customer", "add customer", "new customer", "register customer",
            "create client", "add client");

    private static bool IsCreateSaleOrOrder(string text) =>
        Matches(text, "create sale", "create order", "new order", "new sale");

    private static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(t => text.Contains(t, StringComparison.OrdinalIgnoreCase));


    private static AiIntentDetectionResult EnrichCreateTools(
        string text,
        AiIntentDetectionResult result,
        AiPageContextDto page)
    {
        var tools = result.SuggestedTools.ToList();

        if (Matches(text, "customer", "client")
            && IsExplicitCreateCustomer(text)
            && !tools.Contains(AiToolName.CreateCustomer))
            tools.Insert(0, AiToolName.CreateCustomer);

        if (Matches(text, "product", "item")
            && !IsCreateSaleOrOrder(text)
            && !tools.Contains(AiToolName.CreateProduct))
            tools.Insert(0, AiToolName.CreateProduct);

        if (Matches(text, "supplier", "vendor")
            && !tools.Contains(AiToolName.CreateSupplier))
            tools.Insert(0, AiToolName.CreateSupplier);

        if ((Matches(text, "sale", "order")
            || (Matches(text, "order") && !Matches(text, "purchase", "buy stock", "reorder")))
            && !tools.Contains(AiToolName.CreateSale))
            tools.Insert(0, AiToolName.CreateSale);

        if (Matches(text, "invoice", "bill")
            && !tools.Contains(AiToolName.CreateInvoice))
            tools.Insert(0, AiToolName.CreateInvoice);

        if (Matches(text, "purchase order", "create po", "buy stock", "order stock", "draft po"))
        {
            // Prefer draft (auto low-stock lines) unless the user already named a product.
            var namedProduct = HasNamedPurchaseProduct(text);
            if (namedProduct)
            {
                tools.Remove(AiToolName.CreatePurchaseOrderDraft);
                if (!tools.Contains(AiToolName.CreatePurchaseOrder))
                    tools.Insert(0, AiToolName.CreatePurchaseOrder);
            }
            else if (tools.Contains(AiToolName.CreatePurchaseOrderDraft))
            {
                tools.Remove(AiToolName.CreatePurchaseOrder);
                // Keep draft first.
                tools.Remove(AiToolName.CreatePurchaseOrderDraft);
                tools.Insert(0, AiToolName.CreatePurchaseOrderDraft);
            }
            else if (!tools.Contains(AiToolName.CreatePurchaseOrder))
            {
                tools.Insert(0, AiToolName.CreatePurchaseOrderDraft);
            }
        }

        if (Matches(text, "adjust", "adjust stock", "adjust inventory")
            && !tools.Contains(AiToolName.AdjustInventory))
            tools.Insert(0, AiToolName.AdjustInventory);

        if (Matches(text, "receive stock", "receive inventory")
            && !tools.Contains(AiToolName.ReceiveStock))
            tools.Insert(0, AiToolName.ReceiveStock);

        if (tools.Count == 0 && IsShortCreate(text))
        {
            var pageTool = InferCreateFromPage(page);
            if (pageTool is not null)
                tools.Add(pageTool.Value);
        }

        return new AiIntentDetectionResult
        {
            Intent = result.Intent,
            Confidence = result.Confidence,
            SuggestedTools = tools
        };
    }

    private static AiIntentDetectionResult EnrichReadTools(string text, AiIntentDetectionResult result)
    {
        var tools = result.SuggestedTools.ToList();

        if (Matches(text, "search customer", "find customer")
            && !tools.Contains(AiToolName.SearchCustomer))
            tools.Insert(0, AiToolName.SearchCustomer);

        if (Matches(text, "search product", "find product")
            && !tools.Contains(AiToolName.SearchProduct))
            tools.Insert(0, AiToolName.SearchProduct);

        if (Matches(text, "search supplier", "find supplier")
            && !tools.Contains(AiToolName.SearchSupplier))
            tools.Insert(0, AiToolName.SearchSupplier);

        if (Matches(text, "profit", "show profit")
            && !tools.Contains(AiToolName.ShowProfit))
            tools.Insert(0, AiToolName.ShowProfit);

        if (Matches(text, "search invoice", "find invoice")
            && !tools.Contains(AiToolName.SearchInvoice))
            tools.Insert(0, AiToolName.SearchInvoice);

        return new AiIntentDetectionResult
        {
            Intent = result.Intent,
            Confidence = result.Confidence,
            SuggestedTools = tools
        };
    }

    /// <summary>
    /// Normalizes user input (trim only; English-only backend).
    /// </summary>
    public static string NormalizeBilingual(string message) => message.Trim();

    private static string ExpandPageAwareShortCommand(string message, AiPageContextDto page)
    {
        var lower = message.Trim().ToLowerInvariant();
        if (!IsShortCreate(lower))
            return message;

        var entity = InferEntityFromPage(page);
        if (entity is null)
            return message;

        return $"create {entity}";
    }

    private static bool IsShortCreate(string text)
    {
        var t = text.Trim().ToLowerInvariant();
        return t is "create one" or "create it" or "add one" or "new one" or "make one"
            || t is "create" or "add new" or "new"
            || Regex.IsMatch(t, @"^(create|add|new)\s+(one|it)?$");
    }

    private static string? InferEntityFromPage(AiPageContextDto page)
    {
        var p = $"{page.Module} {page.Url}".ToLowerInvariant();
        if (p.Contains("customer")) return "customer";
        if (p.Contains("product")) return "product";
        if (p.Contains("supplier")) return "supplier";
        if (p.Contains("invoice")) return "invoice";
        if (p.Contains("order") || p.Contains("sale")) return "sale";
        if (p.Contains("purchase")) return "purchase order";
        if (p.Contains("inventor") || p.Contains("stock")) return "inventory adjustment";
        return null;
    }

    private static AiToolName? InferCreateFromPage(AiPageContextDto page) =>
        InferEntityFromPage(page) switch
        {
            "customer" => AiToolName.CreateCustomer,
            "product" => AiToolName.CreateProduct,
            "supplier" => AiToolName.CreateSupplier,
            "invoice" => AiToolName.CreateInvoice,
            "sale" => AiToolName.CreateSale,
            "purchase order" => AiToolName.CreatePurchaseOrderDraft,
            "inventory adjustment" => AiToolName.AdjustInventory,
            _ => null
        };

    private static bool HasNamedPurchaseProduct(string text)
    {
        var stripped = text;
        foreach (var phrase in new[]
                 {
                     "create a purchase order", "create purchase order", "draft purchase order",
                     "create po", "draft po", "purchase order", "buy stock", "order stock",
                     "reorder items", "reorder", "low stock", "from low stock",
                     "create", "draft", "generate", "make", "prepare", "new", "please"
                 })
        {
            stripped = stripped.Replace(phrase, " ", StringComparison.OrdinalIgnoreCase);
        }

        stripped = Regex.Replace(stripped, @"\b(for|of|from|supplier|vendor|quantity|qty|units?|and|the|a|an|items?|products?|recommendations?|stock)\b", " ", RegexOptions.IgnoreCase);
        stripped = Regex.Replace(stripped, @"\d+(?:\.\d+)?", " ");
        stripped = Regex.Replace(stripped, @"\s+", " ").Trim();
        return stripped.Length >= 2;
    }

    private static bool Matches(string text, params string[] terms) =>
        terms.Any(t => text.Contains(t, StringComparison.OrdinalIgnoreCase));
}
