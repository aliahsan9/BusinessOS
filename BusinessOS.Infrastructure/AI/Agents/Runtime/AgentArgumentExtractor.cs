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
            AiToolName.SearchCustomer => Obj(("query", ExtractSearchQuery(message, "customer", "client"))),
            AiToolName.UpdateCustomer => HeuristicUpdateCustomer(message, state, page),
            AiToolName.DeleteCustomer => HeuristicIdOrName(message, state.CustomerId ?? page.CustomerId, state.CustomerName, "customerId", "name"),
            AiToolName.CreateProduct => HeuristicCreateProduct(message),
            AiToolName.SearchProduct => Obj(("query", ExtractSearchQuery(message, "product"))),
            AiToolName.UpdateProduct => HeuristicIdOrName(message, state.ProductId, state.ProductName, "productId", "name"),
            AiToolName.DeleteProduct => HeuristicIdOrName(message, state.ProductId, state.ProductName, "productId", "name"),
            AiToolName.AdjustInventory => HeuristicStock(message, state, "adjust"),
            AiToolName.ReceiveStock => HeuristicStock(message, state, "receive"),
            AiToolName.CreateSupplier => HeuristicCreateSupplier(message),
            AiToolName.SearchSupplier => Obj(("query", ExtractSearchQuery(message, "supplier"))),
            AiToolName.UpdateSupplier => HeuristicIdOrName(message, state.SupplierId, state.SupplierName, "supplierId", "name"),
            AiToolName.DeleteSupplier => HeuristicIdOrName(message, state.SupplierId, state.SupplierName, "supplierId", "name"),
            AiToolName.CreateInvoice => HeuristicInvoice(message, state, page),
            AiToolName.CancelInvoice => HeuristicCancelInvoice(message, state, page),
            AiToolName.SearchInvoice => Obj(("query", ExtractSearchQuery(message, "invoice"))),
            AiToolName.CreateSale => HeuristicSale(message, state, page),
            AiToolName.CreatePurchaseOrder or AiToolName.CreatePurchaseOrderDraft =>
                HeuristicCreatePurchaseOrder(message, state),
            AiToolName.ApprovePurchaseOrder or AiToolName.ReceivePurchase =>
                HeuristicIdOrName(message, state.PurchaseOrderId, null, "purchaseOrderId", "poNumber"),
            AiToolName.UpdateTaxDefaults => HeuristicTax(message),
            AiToolName.UpdateCompanyProfile => HeuristicCompany(message),
            _ => null
        };
    }

    private static JsonElement HeuristicCreateCustomer(string message)
    {
        var parsed = ParseName(message);
        var email = ParseEmail(message);
        var phone = ParsePhone(message);
        var address = ParseLabeled(message, "address") ?? ParseAfter(message, "address");
        var city = ParseLabeled(message, "city") ?? InferCity(message);
        var country = ParseLabeled(message, "country") ?? "Pakistan";

        if (parsed is null)
        {
            return Obj(
                ("email", email),
                ("phone", phone),
                ("address", address),
                ("city", city),
                ("country", country),
                ("postalCode", ParseLabeled(message, "postal", "zip") ?? "00000"));
        }

        var (first, last) = parsed.Value;
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
        var parsed = ParseName(message);
        return Obj(
            ("customerId", id?.ToString()),
            ("name", state.CustomerName),
            ("firstName", parsed?.First),
            ("lastName", parsed?.Last),
            ("email", ParseEmail(message)),
            ("phone", ParsePhone(message)),
            ("address", ParseLabeled(message, "address")),
            ("city", ParseLabeled(message, "city")));
    }

    private static JsonElement HeuristicCreateProduct(string message)
    {
        var name = ParseLabeled(message, "name")
            ?? ExtractAfterKeywords(message, "product", "item");
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
        var name = ParseLabeled(message, "name")
            ?? ExtractAfterKeywords(message, "supplier", "vendor");
        return Obj(
            ("name", name),
            ("email", ParseEmail(message)),
            ("phone", ParsePhone(message)),
            ("address", ParseLabeled(message, "address")),
            ("contactPerson", ParseLabeled(message, "contact", "person")));
    }

    private static JsonElement HeuristicStock(string message, AgentExecutionState state, string mode)
    {
        var qty = ParseDecimal(message, "quantity", "qty", "units") ?? ParseFirstNumber(message);
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
        var refersToThisCustomer = ContainsThisCustomer(message);
        var customerId = state.CustomerId ?? page.CustomerId;
        var customerName = state.CustomerName;
        if (string.IsNullOrWhiteSpace(customerName) && !refersToThisCustomer)
            customerName = ExtractCustomerNameForSale(message);

        var productName = ParseLabeled(message, "product", "item")
            ?? ExtractAfterKeywords(message, "product", "item");
        var qty = ParseDecimal(message, "quantity", "qty", "units") ?? ParseFirstNumber(message);

        var dict = new Dictionary<string, object?>
        {
            ["customerId"] = customerId?.ToString(),
            ["customerName"] = string.IsNullOrWhiteSpace(customerName) ? null : customerName,
            ["discount"] = ParseDecimal(message, "discount") ?? 0,
            ["tax"] = ParseDecimal(message, "tax") ?? 0
        };

        if (!string.IsNullOrWhiteSpace(productName) || state.ProductId is not null)
        {
            var item = new Dictionary<string, object?>();
            if (state.ProductId is Guid pid)
                item["productId"] = pid.ToString();
            if (!string.IsNullOrWhiteSpace(productName))
                item["productName"] = productName;
            if (state.ProductName is not null && string.IsNullOrWhiteSpace(productName))
                item["productName"] = state.ProductName;
            item["quantity"] = qty ?? 1m;
            dict["items"] = new List<object> { item };
            dict["productName"] = item.GetValueOrDefault("productName");
        }

        var json = JsonSerializer.Serialize(dict, JsonOptions);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static bool ContainsThisCustomer(string message)
    {
        var t = message.ToLowerInvariant();
        return t.Contains("this customer")
            || t.Contains("the customer")
            || t.Contains("current customer");
    }

    private static string? ExtractCustomerNameForSale(string message)
    {
        var labeled = ParseLabeled(message, "customer", "client", "for");
        if (string.IsNullOrWhiteSpace(labeled))
            labeled = ExtractAfterKeywords(message, "for customer", "for client", "customer", "client");

        var cleaned = CleanName(labeled);
        if (string.IsNullOrWhiteSpace(cleaned))
            return null;

        var lower = cleaned.ToLowerInvariant();
        if (lower is "this" or "the" or "that" or "current" or "this customer" or "the customer")
            return null;

        return cleaned;
    }

    private static JsonElement HeuristicCreatePurchaseOrder(string message, AgentExecutionState state)
    {
        var supplierName = ParseLabeled(message, "supplier", "vendor")
            ?? ExtractAfterKeywords(message, "from supplier", "from vendor", "supplier");
        var qty = ParseDecimal(message, "quantity", "qty", "units") ?? ParseQtyBeforeProduct(message);
        var productName = ParseLabeled(message, "product", "item")
            ?? ExtractPurchaseProductName(message)
            ?? state.ProductName;

        // Short clarification replies like "Laptop" or "5 Laptop".
        if (string.IsNullOrWhiteSpace(productName) && LooksLikeProductOnlyReply(message))
            productName = CleanName(Regex.Replace(message, @"^\d+(?:\.\d+)?\s*", "").Trim());

        var items = new List<object>();
        if (!string.IsNullOrWhiteSpace(productName) || state.ProductId is not null)
        {
            var item = new Dictionary<string, object?>();
            if (state.ProductId is Guid pid)
                item["productId"] = pid.ToString();
            if (!string.IsNullOrWhiteSpace(productName))
                item["productName"] = productName;
            item["quantity"] = qty ?? 1m;
            items.Add(item);
        }

        var dict = new Dictionary<string, object?>();
        if (state.SupplierId is Guid sid)
            dict["supplierId"] = sid.ToString();
        if (!string.IsNullOrWhiteSpace(supplierName))
            dict["supplierName"] = CleanName(supplierName);
        if (!string.IsNullOrWhiteSpace(productName))
            dict["productName"] = productName;
        if (qty is not null)
            dict["quantity"] = qty;
        if (items.Count > 0)
            dict["items"] = items;

        var json = JsonSerializer.Serialize(dict, JsonOptions);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static string? ExtractPurchaseProductName(string message)
    {
        // "create purchase order for Laptop", "order 5 laptops", "buy stock of Mouse"
        var patterns = new[]
        {
            @"(?:purchase\s*order|create\s*po|draft\s*po|reorder|buy|order|purchase)\s+(?:for|of)?\s*(.+)$",
            @"(?:for|of)\s+([A-Za-z][A-Za-z0-9\s\-]{1,60})$",
            @"(\d+(?:\.\d+)?)\s+(?:units?\s+(?:of\s+)?)?([A-Za-z][A-Za-z0-9\s\-]{1,40})"
        };

        foreach (var pattern in patterns)
        {
            var m = Regex.Match(message, pattern, RegexOptions.IgnoreCase);
            if (!m.Success)
                continue;

            var raw = m.Groups.Count >= 3 && !string.IsNullOrWhiteSpace(m.Groups[2].Value)
                ? m.Groups[2].Value
                : m.Groups[1].Value;

            var cleaned = CleanPurchaseProduct(raw);
            if (!string.IsNullOrWhiteSpace(cleaned))
                return cleaned;
        }

        return null;
    }

    private static string CleanPurchaseProduct(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var text = Regex.Replace(raw, @"^\d+(?:\.\d+)?\s*(?:units?\s+of\s+|x\s+)?", "", RegexOptions.IgnoreCase);
        var stop = new[]
        {
            "purchase", "order", "po", "draft", "create", "new", "add", "make", "prepare", "generate",
            "supplier", "vendor", "please", "the", "a", "an", "items", "item", "stock", "reorder",
            "from", "with", "and", "then", "quantity", "qty", "units", "unit", "cost", "price"
        };
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .TakeWhile(t => !stop.Contains(t, StringComparer.OrdinalIgnoreCase) && !t.Contains('@') && !Regex.IsMatch(t, @"^\d"))
            .ToArray();
        return string.Join(' ', tokens).Trim(' ', '.', ',', ':', '-', '"', '\'');
    }

    private static bool LooksLikeProductOnlyReply(string message)
    {
        var t = message.Trim();
        if (t.Length is < 1 or > 80)
            return false;
        if (Regex.IsMatch(t, @"\b(create|delete|update|search|show|report|invoice|customer|supplier|hello|hi|hey|thanks)\b", RegexOptions.IgnoreCase))
            return false;
        var words = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length is >= 1 and <= 6;
    }

    private static decimal? ParseQtyBeforeProduct(string message)
    {
        var m = Regex.Match(message, @"(\d+(?:\.\d+)?)\s*(?:units?\s+(?:of\s+)?|x\s+)?[A-Za-z]", RegexOptions.IgnoreCase);
        return m.Success && decimal.TryParse(m.Groups[1].Value, out var v) ? v : null;
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
            ("name", ParseLabeled(message, "name", "company")),
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
        AiToolName.CreateCustomer =>
            (HasString(el, "firstName") || HasString(el, "fullName"))
            && !IsPlaceholderName(el),
        AiToolName.CreateProduct => HasString(el, "name"),
        AiToolName.CreateSupplier => HasString(el, "name"),
        AiToolName.CreateSale =>
            HasString(el, "customerId") || HasString(el, "customerName"),
        AiToolName.CreatePurchaseOrder =>
            HasItemsArray(el) || HasString(el, "productName") || HasString(el, "supplierName"),
        AiToolName.SearchCustomer or AiToolName.SearchProduct or AiToolName.SearchSupplier => HasString(el, "query"),
        AiToolName.AdjustInventory or AiToolName.ReceiveStock => HasNumber(el, "quantity"),
        _ => true
    };

    private static bool IsPlaceholderName(JsonElement el)
    {
        var first = el.TryGetProperty("firstName", out var f) ? f.GetString() : null;
        var last = el.TryGetProperty("lastName", out var l) ? l.GetString() : null;
        var full = el.TryGetProperty("fullName", out var n) ? n.GetString() : null;
        var combined = $"{first} {last} {full}".Trim().ToLowerInvariant();
        return combined.Contains("customer unknown")
            || combined is "customer ." or "customer" or "unknown"
            || string.IsNullOrWhiteSpace(combined.Replace(".", "").Replace("customer", "").Trim());
    }

    private static bool HasItemsArray(JsonElement el) =>
        el.TryGetProperty("items", out var items)
        && items.ValueKind == JsonValueKind.Array
        && items.GetArrayLength() > 0;

    private static bool HasString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(p.GetString());

    private static bool HasNumber(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind is JsonValueKind.Number;

    private static (string First, string Last)? ParseName(string message)
    {
        var labeled = ParseLabeled(message, "name", "his name", "her name", "customer name");
        var raw = labeled ?? ExtractAfterKeywords(message, "customer", "client", "named", "called");
        if (string.IsNullOrWhiteSpace(raw))
        {
            // "create customer Ahmed Ali"
            var m = Regex.Match(message, @"(?:customer|client)\s+([A-Za-z][A-Za-z\s]{1,60})", RegexOptions.IgnoreCase);
            if (m.Success)
                raw = m.Groups[1].Value;
        }

        raw = CleanName(raw);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var lower = raw.ToLowerInvariant();
        if (lower is "this" or "the" or "that" or "unknown" or "this customer" or "the customer" or "with name")
            return null;

        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return (parts[0], ".");
        return (parts[0], string.Join(' ', parts.Skip(1)));
    }

    private static string CleanName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";
        var stop = new[] { "phone", "email", "address", "city", "his", "her", "is", "this", "the", "that", "for", "order", "sale" };
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
        var labeled = ParseLabeled(message, "phone", "mobile");
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
        return ParseLabeled(message, "city");
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
