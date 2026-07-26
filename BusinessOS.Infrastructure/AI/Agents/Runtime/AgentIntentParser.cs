using System.Text.RegularExpressions;
using BusinessOS.Application.Features.Agents.Services;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.AI.Services;

namespace BusinessOS.Infrastructure.AI.Agents.Runtime;

/// <summary>
/// Extends <see cref="IAiIntentDetector"/> with Urdu synonyms and page-aware short commands.
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
        var result = _detector.Detect(pageAware, page, memory);

        // Enrich with employee write tools when intent is ActionCreate but detector only suggested legacy tools.
        if (result.Intent is AiCopilotIntent.ActionCreate)
            result = EnrichCreateTools(pageAware, result, page);

        if (result.Intent is AiCopilotIntent.ActionRead)
            result = EnrichReadTools(pageAware, result);

        return result;
    }

    private static AiIntentDetectionResult EnrichCreateTools(
        string text,
        AiIntentDetectionResult result,
        AiPageContextDto page)
    {
        var tools = result.SuggestedTools.ToList();

        if (Matches(text, "customer", "client", "گاہک", "کسٹمر", "کسٹمرز")
            && !tools.Contains(AiToolName.CreateCustomer))
            tools.Insert(0, AiToolName.CreateCustomer);

        if (Matches(text, "product", "item", "پروڈکٹ", "مصنوعات")
            && !tools.Contains(AiToolName.CreateProduct))
            tools.Insert(0, AiToolName.CreateProduct);

        if (Matches(text, "supplier", "vendor", "سپلائر", "سپلائیر")
            && !tools.Contains(AiToolName.CreateSupplier))
            tools.Insert(0, AiToolName.CreateSupplier);

        if (Matches(text, "sale", "order", "فروخت", "سیلز")
            && !Matches(text, "purchase order", "خرید")
            && !tools.Contains(AiToolName.CreateSale))
            tools.Insert(0, AiToolName.CreateSale);

        if (Matches(text, "invoice", "انوائس", "بل")
            && !tools.Contains(AiToolName.CreateInvoice))
            tools.Insert(0, AiToolName.CreateInvoice);

        if (Matches(text, "purchase order", "create po", "خرید آرڈر", "پرچیز آرڈر")
            && !tools.Contains(AiToolName.CreatePurchaseOrder))
            tools.Insert(0, AiToolName.CreatePurchaseOrder);

        if (Matches(text, "adjust", "adjust stock", "adjust inventory", "اسٹاک ایڈجسٹ")
            && !tools.Contains(AiToolName.AdjustInventory))
            tools.Insert(0, AiToolName.AdjustInventory);

        if (Matches(text, "receive stock", "receive inventory", "اسٹاک وصول")
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

        if (Matches(text, "search customer", "find customer", "گاہک تلاش", "کسٹمر تلاش")
            && !tools.Contains(AiToolName.SearchCustomer))
            tools.Insert(0, AiToolName.SearchCustomer);

        if (Matches(text, "search product", "find product", "پروڈکٹ تلاش")
            && !tools.Contains(AiToolName.SearchProduct))
            tools.Insert(0, AiToolName.SearchProduct);

        if (Matches(text, "search supplier", "find supplier", "سپلائر تلاش")
            && !tools.Contains(AiToolName.SearchSupplier))
            tools.Insert(0, AiToolName.SearchSupplier);

        if (Matches(text, "profit", "منافع", "show profit", "منافع دکھاؤ")
            && !tools.Contains(AiToolName.ShowProfit))
            tools.Insert(0, AiToolName.ShowProfit);

        if (Matches(text, "search invoice", "find invoice", "انوائس تلاش")
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
    /// Map common Urdu phrases to English keywords so the existing detector can classify them.
    /// </summary>
    public static string NormalizeBilingual(string message)
    {
        var text = message.Trim();
        if (string.IsNullOrEmpty(text))
            return text;

        // Phrase replacements (Urdu → English intent cues)
        var replacements = new (string Urdu, string English)[]
        {
            ("نیا کسٹمر بناؤ", "create customer"),
            ("نیا گاہک بناؤ", "create customer"),
            ("کسٹمر بناؤ", "create customer"),
            ("گاہک بناؤ", "create customer"),
            ("کسٹمر شامل کرو", "create customer"),
            ("گاہک شامل کرو", "create customer"),
            ("پروڈکٹ بناؤ", "create product"),
            ("نیا پروڈکٹ", "create product"),
            ("انوائس بناؤ", "create invoice"),
            ("سیلز بناؤ", "create sale"),
            ("آرڈر بناؤ", "create order"),
            ("سپلائر بناؤ", "create supplier"),
            ("پرچیز آرڈر", "purchase order"),
            ("خرید آرڈر", "purchase order"),
            ("کم اسٹاک", "low stock"),
            ("اسٹاک خلاصہ", "inventory summary"),
            ("انوینٹری خلاصہ", "inventory summary"),
            ("منافع دکھاؤ", "show profit"),
            ("منافع", "profit"),
            ("سیلز رپورٹ", "sales report"),
            ("انوینٹری رپورٹ", "inventory report"),
            ("تلاش کرو", "search"),
            ("حذف کرو", "delete"),
            ("اپڈیٹ کرو", "update"),
            ("ترمیم کرو", "update"),
            ("منظور کرو", "approve"),
            ("اسٹاک وصول", "receive stock"),
            ("ایک بناؤ", "create one"),
            ("نیا بناؤ", "create one"),
        };

        foreach (var (urdu, english) in replacements)
        {
            if (text.Contains(urdu, StringComparison.OrdinalIgnoreCase))
                text = text.Replace(urdu, english, StringComparison.OrdinalIgnoreCase);
        }

        // Token-level fallbacks
        text = text
            .Replace("کسٹمر", "customer", StringComparison.OrdinalIgnoreCase)
            .Replace("گاہک", "customer", StringComparison.OrdinalIgnoreCase)
            .Replace("پروڈکٹ", "product", StringComparison.OrdinalIgnoreCase)
            .Replace("انوائس", "invoice", StringComparison.OrdinalIgnoreCase)
            .Replace("سپلائر", "supplier", StringComparison.OrdinalIgnoreCase)
            .Replace("بناؤ", "create", StringComparison.OrdinalIgnoreCase)
            .Replace("شامل", "add", StringComparison.OrdinalIgnoreCase);

        return text;
    }

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
            "purchase order" => AiToolName.CreatePurchaseOrder,
            "inventory adjustment" => AiToolName.AdjustInventory,
            _ => null
        };

    private static bool Matches(string text, params string[] terms) =>
        terms.Any(t => text.Contains(t, StringComparison.OrdinalIgnoreCase));
}
