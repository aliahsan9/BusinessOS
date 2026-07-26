using System.Text.Json;
using System.Text.RegularExpressions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.Agents.Services;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.AI.Services;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Agents.Runtime;

public sealed class AgentArgumentExtractor : IAgentArgumentExtractor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILlmChatClient _llm;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AgentArgumentExtractor> _logger;

    public AgentArgumentExtractor(
        ILlmChatClient llm,
        ICurrentUserService currentUser,
        ILogger<AgentArgumentExtractor> logger)
    {
        _llm = llm;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<JsonElement> ExtractAsync(
        AiToolName toolName,
        string parameterSchemaJson,
        string message,
        AgentExecutionState state,
        AiPageContextDto page,
        string language,
        CancellationToken cancellationToken = default)
    {
        var heuristic = ExtractHeuristic(toolName, message, state, page);
        if (heuristic is not null && HasRequiredShape(toolName, heuristic.Value))
            return heuristic.Value;

        if (_llm.IsConfigured
            && _currentUser.TenantId is Guid tenantId
            && !string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            try
            {
                var system = """
                    You extract structured JSON arguments for a business ERP tool.
                    Return ONLY valid JSON matching the schema. No markdown. No commentary.
                    Use null for unknown optional fields. Prefer values from the user message.
                    Reuse known entity IDs from state when present.
                    """;

                var user = $"""
                    Tool: {toolName}
                    Language: {language}
                    Schema: {parameterSchemaJson}
                    Known state: {JsonSerializer.Serialize(state, JsonOptions)}
                    Page customerId: {page.CustomerId}
                    Page orderId: {page.OrderId}
                    Page invoiceId: {page.InvoiceId}
                    User message: {message}
                    """;

                var reply = await _llm.GenerateReplyAsync(
                    tenantId,
                    _currentUser.UserId!,
                    system,
                    user,
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(reply))
                {
                    var json = ExtractJsonObject(reply);
                    if (json is not null)
                    {
                        using var doc = JsonDocument.Parse(json);
                        return doc.RootElement.Clone();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "LLM argument extraction failed for {Tool}; using heuristics", toolName);
            }
        }

        return heuristic ?? JsonDocument.Parse("{}").RootElement.Clone();
    }

    public static JsonElement? ExtractHeuristic(
        AiToolName toolName,
        string message,
        AgentExecutionState state,
        AiPageContextDto page)
    {
        return toolName switch
        {
            AiToolName.CreateCustomer => HeuristicCreateCustomer(message),
            AiToolName.SearchCustomer => Obj(("query", ExtractSearchQuery(message, "customer", "client", "گاہک", "کسٹمر"))),
            AiToolName.UpdateCustomer => HeuristicUpdateCustomer(message, state, page),
            AiToolName.DeleteCustomer => HeuristicIdOrName(message, state.CustomerId ?? page.CustomerId, state.CustomerName, "customerId", "name"),
            AiToolName.CreateProduct => HeuristicCreateProduct(message),
            AiToolName.SearchProduct => Obj(("query", ExtractSearchQuery(message, "product", "پروڈکٹ"))),
            AiToolName.UpdateProduct => HeuristicIdOrName(message, state.ProductId, state.ProductName, "productId", "name"),
            AiToolName.DeleteProduct => HeuristicIdOrName(message, state.ProductId, state.ProductName, "productId", "name"),
            AiToolName.AdjustInventory => HeuristicStock(message, state, "adjust"),
            AiToolName.ReceiveStock => HeuristicStock(message, state, "receive"),
            AiToolName.CreateSupplier => HeuristicCreateSupplier(message),
            AiToolName.SearchSupplier => Obj(("query", ExtractSearchQuery(message, "supplier", "سپلائر"))),
            AiToolName.UpdateSupplier => HeuristicIdOrName(message, state.SupplierId, state.SupplierName, "supplierId", "name"),
            AiToolName.DeleteSupplier => HeuristicIdOrName(message, state.SupplierId, state.SupplierName, "supplierId", "name"),
            AiToolName.CreateInvoice => HeuristicInvoice(message, state, page),
            AiToolName.CancelInvoice => HeuristicCancelInvoice(message, state, page),
            AiToolName.SearchInvoice => Obj(("query", ExtractSearchQuery(message, "invoice", "انوائس"))),
            AiToolName.CreateSale => HeuristicSale(message, state, page),
            AiToolName.ApprovePurchaseOrder or AiToolName.ReceivePurchase =>
                HeuristicIdOrName(message, state.PurchaseOrderId, null, "purchaseOrderId", "poNumber"),
            AiToolName.UpdateTaxDefaults => HeuristicTax(message),
            AiToolName.UpdateCompanyProfile => HeuristicCompany(message),
            _ => null
        };
    }

    private static JsonElement HeuristicCreateCustomer(string message)
    {
        var (first, last) = ParseName(message);
        var email = ParseEmail(message);
        var phone = ParsePhone(message);
        var address = ParseLabeled(message, "address", "پتہ") ?? ParseAfter(message, "address");
        var city = ParseLabeled(message, "city", "شہر") ?? InferCity(message);
        var country = ParseLabeled(message, "country", "ملک") ?? "Pakistan";

        return Obj(
            ("firstName", first),
            ("lastName", last),
            ("fullName", $"{first} {last}".Trim()),
            ("email", email),
            ("phone", phone),
            ("address", address),
            ("city", city),
            ("country", country),
            ("postalCode", ParseLabeled(message, "postal", "zip") ?? "00000"));
    }

    private static JsonElement HeuristicUpdateCustomer(string message, AgentExecutionState state, AiPageContextDto page)
    {
        var id = state.CustomerId ?? page.CustomerId;
        var (first, last) = ParseName(message);
        return Obj(
            ("customerId", id?.ToString()),
            ("name", state.CustomerName),
            ("firstName", string.IsNullOrWhiteSpace(first) ? null : first),
            ("lastName", string.IsNullOrWhiteSpace(last) ? null : last),
            ("email", ParseEmail(message)),
            ("phone", ParsePhone(message)),
            ("address", ParseLabeled(message, "address", "پتہ")),
            ("city", ParseLabeled(message, "city", "شہر")));
    }

    private static JsonElement HeuristicCreateProduct(string message)
    {
        var name = ParseLabeled(message, "name", "نام")
            ?? ExtractAfterKeywords(message, "product", "item", "پروڈکٹ");
        var sku = ParseLabeled(message, "sku", "SKU");
        var cost = ParseDecimal(message, "cost", "cost price");
        var sale = ParseDecimal(message, "price", "sale price", "selling");
        return Obj(
            ("name", name),
            ("sku", sku),
            ("costPrice", cost),
            ("salePrice", sale),
            ("reorderLevel", ParseInt(message, "reorder") ?? 10));
    }

    private static JsonElement HeuristicCreateSupplier(string message)
    {
        var name = ParseLabeled(message, "name", "نام")
            ?? ExtractAfterKeywords(message, "supplier", "vendor", "سپلائر");
        return Obj(
            ("name", name),
            ("email", ParseEmail(message)),
            ("phone", ParsePhone(message)),
            ("address", ParseLabeled(message, "address", "پتہ")),
            ("contactPerson", ParseLabeled(message, "contact", "person")));
    }

    private static JsonElement HeuristicStock(string message, AgentExecutionState state, string mode)
    {
        var qty = ParseDecimal(message, "quantity", "qty", "units", "مقدار") ?? ParseFirstNumber(message);
        return Obj(
            ("productId", state.ProductId?.ToString()),
            ("productName", state.ProductName ?? ExtractAfterKeywords(message, "product", "for")),
            ("sku", ParseLabeled(message, "sku")),
            ("quantity", qty),
            ("transactionType", mode == "receive" ? "In" : (ParseLabeled(message, "type") ?? "Adjustment")),
            ("notes", mode));
    }

    private static JsonElement HeuristicInvoice(string message, AgentExecutionState state, AiPageContextDto page)
    {
        return Obj(
            ("orderId", (state.OrderId ?? page.OrderId)?.ToString()),
            ("customerId", (state.CustomerId ?? page.CustomerId)?.ToString()),
            ("customerName", state.CustomerName),
            ("dueDays", ParseInt(message, "due") ?? 14),
            ("notes", ParseLabeled(message, "notes")));
    }

    private static JsonElement HeuristicCancelInvoice(string message, AgentExecutionState state, AiPageContextDto page)
    {
        return Obj(
            ("invoiceId", (state.InvoiceId ?? page.InvoiceId)?.ToString()),
            ("invoiceNumber", ParseLabeled(message, "invoice", "number") ?? ExtractSearchQuery(message, "invoice", "cancel")));
    }

    private static JsonElement HeuristicSale(string message, AgentExecutionState state, AiPageContextDto page)
    {
        return Obj(
            ("customerId", (state.CustomerId ?? page.CustomerId)?.ToString()),
            ("customerName", state.CustomerName ?? ExtractAfterKeywords(message, "for", "customer")),
            ("discount", ParseDecimal(message, "discount") ?? 0),
            ("tax", ParseDecimal(message, "tax") ?? 0));
    }

    private static JsonElement HeuristicTax(string message)
    {
        return Obj(
            ("taxRate", ParseDecimal(message, "tax", "tax rate")),
            ("currency", ParseLabeled(message, "currency")),
            ("invoicePrefix", ParseLabeled(message, "prefix", "invoice prefix")));
    }

    private static JsonElement HeuristicCompany(string message)
    {
        return Obj(
            ("name", ParseLabeled(message, "name", "company", "نام")),
            ("email", ParseEmail(message)),
            ("phone", ParsePhone(message)),
            ("address", ParseLabeled(message, "address")),
            ("businessType", ParseLabeled(message, "type", "industry")));
    }

    private static JsonElement HeuristicIdOrName(
        string message,
        Guid? id,
        string? knownName,
        string idKey,
        string nameKey)
    {
        return Obj(
            (idKey, id?.ToString()),
            (nameKey, knownName ?? ExtractSearchQuery(message, "delete", "update", "remove", "edit")));
    }

    private static bool HasRequiredShape(AiToolName tool, JsonElement el) => tool switch
    {
        AiToolName.CreateCustomer => HasString(el, "firstName") || HasString(el, "fullName"),
        AiToolName.CreateProduct => HasString(el, "name"),
        AiToolName.CreateSupplier => HasString(el, "name"),
        AiToolName.SearchCustomer or AiToolName.SearchProduct or AiToolName.SearchSupplier => HasString(el, "query"),
        AiToolName.AdjustInventory or AiToolName.ReceiveStock => HasNumber(el, "quantity"),
        _ => true
    };

    private static bool HasString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(p.GetString());

    private static bool HasNumber(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind is JsonValueKind.Number;

    private static (string First, string Last) ParseName(string message)
    {
        var labeled = ParseLabeled(message, "name", "نام", "his name", "her name", "customer name");
        var raw = labeled ?? ExtractAfterKeywords(message, "customer", "client", "named", "called");
        if (string.IsNullOrWhiteSpace(raw))
        {
            // "create customer Ahmed Ali"
            var m = Regex.Match(message, @"(?:customer|client|گاہک|کسٹمر)\s+([A-Za-z\u0600-\u06FF][A-Za-z\u0600-\u06FF\s]{1,60})", RegexOptions.IgnoreCase);
            if (m.Success)
                raw = m.Groups[1].Value;
        }

        raw = CleanName(raw);
        if (string.IsNullOrWhiteSpace(raw))
            return ("Customer", "Unknown");

        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return (parts[0], ".");
        return (parts[0], string.Join(' ', parts.Skip(1)));
    }

    private static string CleanName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";
        var stop = new[] { "phone", "email", "address", "city", "his", "her", "is", "فون", "ای میل", "پتہ" };
        var tokens = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .TakeWhile(t => !stop.Contains(t, StringComparer.OrdinalIgnoreCase) && !t.Contains('@') && !Regex.IsMatch(t, @"^\d"))
            .ToArray();
        return string.Join(' ', tokens).Trim(' ', '.', ',', ':');
    }

    private static string? ParseEmail(string message)
    {
        var m = Regex.Match(message, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase);
        return m.Success ? m.Value : null;
    }

    private static string? ParsePhone(string message)
    {
        var labeled = ParseLabeled(message, "phone", "mobile", "فون", "موبائل");
        if (!string.IsNullOrWhiteSpace(labeled))
            return Regex.Replace(labeled, @"[^\d+]", "");

        var m = Regex.Match(message, @"(\+?\d[\d\s-]{8,}\d)");
        return m.Success ? Regex.Replace(m.Groups[1].Value, @"[^\d+]", "") : null;
    }

    private static string? ParseLabeled(string message, params string[] labels)
    {
        foreach (var label in labels)
        {
            var m = Regex.Match(
                message,
                $@"(?:{Regex.Escape(label)})\s*(?:is|:|=)?\s*([^\.\n,;]+)",
                RegexOptions.IgnoreCase);
            if (m.Success)
                return m.Groups[1].Value.Trim();
        }
        return null;
    }

    private static string? ParseAfter(string message, string keyword)
    {
        var idx = message.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var rest = message[(idx + keyword.Length)..].Trim(' ', ':', '=', '.');
        return rest.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
    }

    private static string? ExtractAfterKeywords(string message, params string[] keywords)
    {
        foreach (var kw in keywords)
        {
            var m = Regex.Match(message, $@"(?:{Regex.Escape(kw)})\s+(.+)$", RegexOptions.IgnoreCase);
            if (m.Success)
                return CleanName(m.Groups[1].Value);
        }
        return null;
    }

    private static string ExtractSearchQuery(string message, params string[] stripWords)
    {
        var text = message;
        foreach (var w in new[] { "search", "find", "look up", "show", "get", "delete", "update", "remove", "edit", "cancel", "create", "add", "new" }.Concat(stripWords))
            text = Regex.Replace(text, $@"\b{Regex.Escape(w)}\b", " ", RegexOptions.IgnoreCase);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static string? InferCity(string message)
    {
        foreach (var city in new[] { "Lahore", "Karachi", "Islamabad", "Rawalpindi", "Faisalabad", "Multan", "Peshawar", "Quetta" })
        {
            if (message.Contains(city, StringComparison.OrdinalIgnoreCase))
                return city;
        }
        return ParseLabeled(message, "city", "شہر");
    }

    private static decimal? ParseDecimal(string message, params string[] labels)
    {
        foreach (var label in labels)
        {
            var m = Regex.Match(message, $@"(?:{Regex.Escape(label)})\s*(?:is|:|=)?\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
            if (m.Success && decimal.TryParse(m.Groups[1].Value, out var v))
                return v;
        }
        return null;
    }

    private static int? ParseInt(string message, params string[] labels)
    {
        var d = ParseDecimal(message, labels);
        return d is null ? null : (int)d.Value;
    }

    private static decimal? ParseFirstNumber(string message)
    {
        var m = Regex.Match(message, @"(\d+(?:\.\d+)?)");
        return m.Success && decimal.TryParse(m.Groups[1].Value, out var v) ? v : null;
    }

    private static string? ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        return text[start..(end + 1)];
    }

    private static JsonElement Obj(params (string Key, object? Value)[] pairs)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var (k, v) in pairs)
        {
            if (v is not null)
                dict[k] = v;
        }
        var json = JsonSerializer.Serialize(dict, JsonOptions);
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
