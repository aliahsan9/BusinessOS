using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;

namespace BusinessOS.Infrastructure.AI.Agents.Runtime;

/// <summary>
/// Holds a write action until the owner has supplied the details and confirmed them.
/// </summary>
public sealed class AgentPendingAction
{
    public AiToolName Tool { get; set; }
    public Dictionary<string, string?> Slots { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool AwaitingConfirmation { get; set; }
    public string? NextField { get; set; }
    public string? OriginalRequest { get; set; }
}

public sealed class AgentClarificationDecision
{
    public bool ShouldExecute { get; init; }
    public string Question { get; init; } = "";
    public string? PendingJson { get; init; }
    public JsonElement? Args { get; init; }
    public AiToolName Tool { get; init; }
    public IReadOnlyList<AiSuggestionDto> Suggestions { get; init; } = [];
}

/// <summary>
/// Decides whether a mutating agent tool may run, or whether the owner still needs to clarify.
/// </summary>
public static class AgentActionClarifier
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly HashSet<string> ConfirmPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "yes", "y", "yeah", "yep", "ok", "okay", "sure", "confirm", "confirmed",
        "go ahead", "do it", "create it", "yes create it", "yes please", "please do",
        "proceed", "thats correct", "correct", "looks good", "go for it",
        "yes do it", "yes go ahead", "haan", "han", "ji", "jee", "theek hai", "theek", "kar do",
        "yes delete it", "yes update it", "yes create the order", "yes create the product",
        "yes create the supplier", "yes save it"
    };

    private static readonly HashSet<string> CancelPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "no", "cancel", "stop", "never mind", "nevermind", "dont", "do not",
        "no cancel", "nah", "nahi", "nai", "leave it", "no leave it", "no thanks", "no thank you"
    };

    private static readonly HashSet<string> SmallTalk = new(StringComparer.OrdinalIgnoreCase)
    {
        "hi", "hello", "hey", "thanks", "thank you", "good morning", "good evening",
        "good afternoon", "salam", "assalam o alaikum", "aoa"
    };

    public static bool NeedsBrief(AiToolName tool) => tool is
        AiToolName.CreateCustomer or AiToolName.UpdateCustomer or AiToolName.DeleteCustomer
        or AiToolName.CreateProduct or AiToolName.UpdateProduct or AiToolName.DeleteProduct
        or AiToolName.CreateSupplier or AiToolName.UpdateSupplier or AiToolName.DeleteSupplier
        or AiToolName.CreateSale or AiToolName.CreateInvoice or AiToolName.CancelInvoice
        or AiToolName.AdjustInventory or AiToolName.ReceiveStock
        or AiToolName.CreatePurchaseOrder or AiToolName.CreatePurchaseOrderDraft
        or AiToolName.ApprovePurchaseOrder or AiToolName.ReceivePurchase
        or AiToolName.UpdateCompanyProfile or AiToolName.UpdateTaxDefaults;

    public static AgentPendingAction? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var pending = JsonSerializer.Deserialize<AgentPendingAction>(json, JsonOptions);
            if (pending is null)
                return null;

            pending.Slots = pending.Slots is null
                ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string?>(pending.Slots, StringComparer.OrdinalIgnoreCase);
            return pending;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool IsSmallTalk(string message) => SmallTalk.Contains(Normalize(message));

    public static bool IsDifferentCommand(string message, AiToolName pendingTool)
    {
        if (IsPureConfirmation(message) || IsCancel(message) || IsSmallTalk(message))
            return false;

        var lower = message.ToLowerInvariant();
        if (Regex.IsMatch(lower, @"\b(show|find|search|list|report|help|what can you|how much|summarize)\b"))
            return true;

        if (!Regex.IsMatch(lower, @"\b(create|add|new|delete|remove|make|register)\b"))
            return false;

        var guessed = GuessCommandTool(lower);
        return guessed is not null && guessed.Value != pendingTool;
    }

    public static AgentClarificationDecision Decide(
        AiToolName tool,
        string message,
        AgentPendingAction? pending,
        AgentExecutionState state,
        AiPageContextDto page)
    {
        if (pending is not null && pending.Tool != tool)
            pending = null;

        if (pending is not null && IsCancel(message))
        {
            return new AgentClarificationDecision
            {
                ShouldExecute = false,
                Tool = tool,
                Question = "Okay, I won't do that. Tell me when you want to try again.",
                PendingJson = null
            };
        }

        var slots = pending is null
            ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string?>(pending.Slots, StringComparer.OrdinalIgnoreCase);

        var original = string.IsNullOrWhiteSpace(pending?.OriginalRequest) ? message : pending!.OriginalRequest;
        Absorb(tool, message, slots, state, page);
        ApplyContext(tool, slots, state, page);
        if (!string.IsNullOrWhiteSpace(pending?.NextField))
            AssignBare(pending.NextField, message, slots);

        var draft = new AgentPendingAction
        {
            Tool = tool,
            Slots = slots,
            OriginalRequest = original,
            NextField = pending?.NextField
        };

        var missing = Missing(tool, slots, state);
        if (missing.Count > 0)
        {
            draft.AwaitingConfirmation = false;
            draft.NextField = missing[0];
            return Ask(tool, draft, QuestionForMissing(tool, missing, slots), includeCancelOnly: true);
        }

        var waiting = pending?.AwaitingConfirmation == true;
        if (waiting && IsPureConfirmation(message))
        {
            ApplyConfirmedAssumptions(tool, slots);
            draft.Slots = slots;
            draft.AwaitingConfirmation = true;
            return new AgentClarificationDecision
            {
                ShouldExecute = true,
                Tool = tool,
                Question = "",
                PendingJson = JsonSerializer.Serialize(draft, JsonOptions),
                Args = BuildArgs(tool, slots)
            };
        }

        draft.AwaitingConfirmation = true;
        draft.NextField = null;
        return Ask(tool, draft, Confirmation(tool, slots), includeCancelOnly: false);
    }

    private static AgentClarificationDecision Ask(
        AiToolName tool,
        AgentPendingAction draft,
        string question,
        bool includeCancelOnly)
    {
        IReadOnlyList<AiSuggestionDto> suggestions = includeCancelOnly
            ? [new AiSuggestionDto { Label = "Cancel", Message = "Cancel" }]
            :
            [
                new AiSuggestionDto { Label = ConfirmLabel(tool), Message = ConfirmMessage(tool) },
                new AiSuggestionDto { Label = "No, cancel", Message = "Cancel" }
            ];

        return new AgentClarificationDecision
        {
            ShouldExecute = false,
            Tool = tool,
            Question = question,
            PendingJson = JsonSerializer.Serialize(draft, JsonOptions),
            Suggestions = suggestions
        };
    }

    private static void Absorb(
        AiToolName tool,
        string message,
        Dictionary<string, string?> slots,
        AgentExecutionState state,
        AiPageContextDto page)
    {
        var heuristic = AgentArgumentExtractor.ExtractHeuristic(tool, message, state, page);
        if (heuristic is not null)
            AbsorbElement(heuristic.Value, slots, message);

        ApplyDetails(tool, message, slots);
    }

    private static void AbsorbElement(JsonElement el, Dictionary<string, string?> slots, string message)
    {
        if (el.ValueKind != JsonValueKind.Object)
            return;

        foreach (var prop in el.EnumerateObject())
        {
            if (prop.NameEquals("items") && prop.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.Value.EnumerateArray())
                    AbsorbElement(item, slots, message);
                continue;
            }

            var text = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number => prop.Value.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };

            if (string.IsNullOrWhiteSpace(text) || IsInventedDefault(prop.Name, text, message))
                continue;
            if (ShouldKeepSlot(prop.Name, text, slots, message))
                continue;

            slots[prop.Name] = text.Trim();
        }

        if (Has(slots, "firstName") && !Has(slots, "fullName"))
        {
            var last = Get(slots, "lastName");
            var full = string.IsNullOrWhiteSpace(last) || last == "."
                ? Get(slots, "firstName")
                : $"{Get(slots, "firstName")} {last}".Trim();
            slots["fullName"] = full;
        }
    }

    private static bool IsInventedDefault(string key, string value, string message)
    {
        if (key.Equals("country", StringComparison.OrdinalIgnoreCase)
            && value.Equals("Pakistan", StringComparison.OrdinalIgnoreCase)
            && !message.Contains("Pakistan", StringComparison.OrdinalIgnoreCase))
            return true;

        if (key.Equals("postalCode", StringComparison.OrdinalIgnoreCase)
            && value is "00000"
            && !message.Contains("00000", StringComparison.Ordinal))
            return true;

        if (key.Equals("reorderLevel", StringComparison.OrdinalIgnoreCase)
            && value is "10"
            && !message.Contains("reorder", StringComparison.OrdinalIgnoreCase))
            return true;

        if ((key.Equals("discount", StringComparison.OrdinalIgnoreCase) || key.Equals("tax", StringComparison.OrdinalIgnoreCase))
            && value is "0" or "0.0"
            && !message.Contains(key, StringComparison.OrdinalIgnoreCase))
            return true;

        if (key.Equals("quantity", StringComparison.OrdinalIgnoreCase))
        {
            if (!SaleOrderLanguage.TryParseQuantity(message, out var stated))
                return true;
            if (decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                && parsed != stated)
                return true;
        }

        if (key.Equals("transactionType", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static bool QuantityWasStated(string message)
    {
        if (Regex.IsMatch(message, @"\b(qty|quantity)\b", RegexOptions.IgnoreCase))
            return true;
        if (WordQuantity(message) is not null)
            return true;
        if (Regex.IsMatch(message, @"\b\d{1,4}\s*(?:x|units?|pcs)\b", RegexOptions.IgnoreCase))
            return true;

        // A short count counts. A phone number or other long digit run does not.
        if (Regex.IsMatch(message, @"\d{5,}"))
            return false;
        return Regex.IsMatch(message, @"\b\d{1,4}\b");
    }

    private static void ApplyDetails(AiToolName tool, string message, Dictionary<string, string?> slots)
    {
        var email = Regex.Match(message, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase);
        if (email.Success)
            slots["email"] = email.Value;

        var phone = Regex.Match(message, @"(\+?\d[\d\s\-]{6,}\d)");
        if (phone.Success)
            slots["phone"] = Regex.Replace(phone.Groups[1].Value, @"[^\d+]", "");

        var sale = Regex.Match(message, @"(?:sale|selling)\s*(?:price)?\s*(?:is|:)?\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
        if (sale.Success)
            slots["salePrice"] = sale.Groups[1].Value;

        var cost = Regex.Match(message, @"cost\s*(?:price)?\s*(?:is|:)?\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
        if (cost.Success)
            slots["costPrice"] = cost.Groups[1].Value;

        if (tool == AiToolName.CreateSale)
            SaleOrderLanguage.ApplyToSlots(message, slots);
        else
        {
            var qty = Regex.Match(message, @"(?:qty|quantity)\s*(?:is|:)?\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
            if (qty.Success)
                slots["quantity"] = qty.Groups[1].Value;
            else
            {
                var wordQty = WordQuantity(message);
                if (wordQty is not null)
                    slots["quantity"] = wordQty;
            }
        }

        TakeLabeled(message, slots, "city", "city");
        TakeLabeled(message, slots, "address", "address");
        TakeLabeled(message, slots, "country", "country");
        TakeLabeled(message, slots, "company", "company");

        if (message.Contains(',') && tool != AiToolName.CreateSale)
            ClassifyTokens(tool, message, slots);
    }

    private static void ClassifyTokens(AiToolName tool, string message, Dictionary<string, string?> slots)
    {
        foreach (var raw in message.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var token = raw.Trim().Trim('.');
            if (token.Length == 0 || IsUnusableValue(token))
                continue;

            if (token.Contains('@'))
            {
                slots["email"] = token;
                continue;
            }

            if (Regex.IsMatch(token, @"\b(qty|quantity|sale|selling|cost)\b", RegexOptions.IgnoreCase))
                continue;

            var digits = Regex.Replace(token, @"[^\d+]", "");
            if (digits.Count(char.IsDigit) >= 7)
            {
                slots["phone"] = digits;
                continue;
            }

            if (Regex.IsMatch(token, @"^\d+(?:\.\d+)?$"))
            {
                if (tool == AiToolName.CreateProduct && !Has(slots, "salePrice"))
                    slots["salePrice"] = token;
                else if (tool == AiToolName.CreateProduct && !Has(slots, "costPrice"))
                    slots["costPrice"] = token;
                else if (!Has(slots, "quantity"))
                    slots["quantity"] = token;
                continue;
            }

            if (IsKnownCity(token))
            {
                slots["city"] = token;
                if (!Has(slots, "address"))
                    slots["address"] = token;
                continue;
            }

            var cleaned = CleanCommandWords(token);
            if (!LooksLikeName(cleaned))
                continue;

            if (tool is AiToolName.CreateSale or AiToolName.CreatePurchaseOrder or AiToolName.AdjustInventory or AiToolName.ReceiveStock)
            {
                var sameAsCustomer = string.Equals(cleaned, Get(slots, "customerName"), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(cleaned, Get(slots, "fullName"), StringComparison.OrdinalIgnoreCase);
                if (!sameAsCustomer && !Has(slots, "customerName") && !Has(slots, "customerId") && tool == AiToolName.CreateSale)
                    slots["customerName"] = cleaned;
                else if (!sameAsCustomer && !Has(slots, "productName"))
                    slots["productName"] = cleaned;
                continue;
            }

            if (tool is AiToolName.CreateProduct or AiToolName.CreateSupplier)
            {
                if (!Has(slots, "name"))
                    slots["name"] = cleaned;
                continue;
            }

            if (!Has(slots, "firstName") && !Has(slots, "fullName") && !Has(slots, "name"))
                AssignName(cleaned, slots);
            else if (!Has(slots, "address"))
                slots["address"] = cleaned;
        }
    }

    private static void AssignBare(string field, string message, Dictionary<string, string?> slots)
    {
        var text = message.Trim();
        if (text.Length is < 1 or > 180 || IsUnusableValue(text) || IsPureConfirmation(text) || IsCancel(text))
            return;

        if (Regex.IsMatch(text, @"\b(create|delete|update|search|show|report)\b", RegexOptions.IgnoreCase)
            && !text.Contains(','))
            return;

        switch (field)
        {
            case "customerName":
            case "name":
                if (!Has(slots, "firstName") && !Has(slots, "fullName") && !Has(slots, "name") && LooksLikeName(text))
                    AssignName(text, slots);
                break;
            case "phone" when !Has(slots, "phone"):
                var phone = Regex.Replace(text, @"[^\d+]", "");
                if (phone.Count(char.IsDigit) >= 7)
                    slots["phone"] = phone;
                break;
            case "email" when !Has(slots, "email") && text.Contains('@'):
                slots["email"] = text;
                break;
            case "place":
            case "address":
                if (!Has(slots, "address"))
                    slots["address"] = text;
                if (IsKnownCity(text))
                    slots["city"] = text;
                break;
            case "product":
                var phrase = SaleOrderLanguage.ExtractProductPhrase(text);
                if (!string.IsNullOrWhiteSpace(phrase) && !Has(slots, "productName"))
                {
                    slots["productName"] = phrase;
                    slots["productQuery"] = phrase;
                }
                if (!Has(slots, "quantity") && SaleOrderLanguage.TryParseQuantity(text, out var productQty))
                    slots["quantity"] = productQty.ToString(System.Globalization.CultureInfo.InvariantCulture);
                break;
            case "quantity" when !Has(slots, "quantity"):
                if (SaleOrderLanguage.TryParseQuantity(text, out var statedQty))
                    slots["quantity"] = statedQty.ToString(System.Globalization.CultureInfo.InvariantCulture);
                break;
            case "customer":
                if (!Has(slots, "customerName") && LooksLikeName(CleanCommandWords(text)))
                    slots["customerName"] = CleanCommandWords(text);
                break;
            case "prices":
            case "salePrice":
                if (!Has(slots, "salePrice"))
                {
                    var n = Regex.Match(text, @"(\d+(?:\.\d+)?)");
                    if (n.Success)
                        slots["salePrice"] = n.Groups[1].Value;
                }
                break;
            case "supplierName":
                if (!Has(slots, "name") && LooksLikeName(text))
                    slots["name"] = text;
                break;
            case "change":
                break;
        }
    }

    private static void AssignName(string text, Dictionary<string, string?> slots)
    {
        var cleaned = CleanCommandWords(text);
        if (!LooksLikeName(cleaned))
            return;

        var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        slots["fullName"] = cleaned;
        slots["name"] = cleaned;
        slots["firstName"] = parts[0];
        slots["lastName"] = parts.Length == 1 ? "." : string.Join(' ', parts.Skip(1));
        if (!Has(slots, "customerName"))
            slots["customerName"] = cleaned;
    }

    private static void ApplyContext(
        AiToolName tool,
        Dictionary<string, string?> slots,
        AgentExecutionState state,
        AiPageContextDto page)
    {
        if (tool is AiToolName.CreateSale or AiToolName.CreateInvoice or AiToolName.UpdateCustomer or AiToolName.DeleteCustomer)
        {
            var customerId = state.CustomerId ?? page.CustomerId;
            if (customerId is Guid id && !Has(slots, "customerId") && !Has(slots, "customerName"))
                slots["customerId"] = id.ToString();
            if (!Has(slots, "customerName") && !string.IsNullOrWhiteSpace(state.CustomerName))
                slots["customerName"] = state.CustomerName;
        }

        if (tool is AiToolName.CreateInvoice or AiToolName.CancelInvoice)
        {
            var orderId = state.OrderId ?? page.OrderId;
            if (orderId is Guid oid && !Has(slots, "orderId"))
                slots["orderId"] = oid.ToString();
            var invoiceId = state.InvoiceId ?? page.InvoiceId;
            if (invoiceId is Guid iid && !Has(slots, "invoiceId"))
                slots["invoiceId"] = iid.ToString();
        }

        if (tool is AiToolName.AdjustInventory or AiToolName.ReceiveStock or AiToolName.CreateSale or AiToolName.CreatePurchaseOrder)
        {
            if (state.ProductId is Guid pid && !Has(slots, "productId") && !Has(slots, "productName"))
                slots["productId"] = pid.ToString();
            if (!Has(slots, "productName") && !string.IsNullOrWhiteSpace(state.ProductName))
                slots["productName"] = state.ProductName;
        }
    }

    private static void ApplyConfirmedAssumptions(AiToolName tool, Dictionary<string, string?> slots)
    {
        if (tool == AiToolName.CreateCustomer)
        {
            if (!Has(slots, "country"))
                slots["country"] = "Pakistan";
            if (!Has(slots, "address") && Has(slots, "city"))
                slots["address"] = Get(slots, "city");
            if (!Has(slots, "city") && Has(slots, "address") && IsKnownCity(Get(slots, "address")))
                slots["city"] = Get(slots, "address");
            if (!Has(slots, "postalCode"))
                slots["postalCode"] = "";
        }

        if (tool == AiToolName.CreateProduct)
        {
            if (!Has(slots, "salePrice") && Has(slots, "costPrice"))
                slots["salePrice"] = Get(slots, "costPrice");
            if (!Has(slots, "costPrice") && Has(slots, "salePrice"))
                slots["costPrice"] = Get(slots, "salePrice");
        }
    }

    private static List<string> Missing(AiToolName tool, Dictionary<string, string?> slots, AgentExecutionState state)
    {
        var missing = new List<string>();
        switch (tool)
        {
            case AiToolName.CreateCustomer:
                if (!Has(slots, "firstName") && !Has(slots, "fullName")) missing.Add("customerName");
                if (!Has(slots, "phone")) missing.Add("phone");
                if (!Has(slots, "email")) missing.Add("email");
                if (!Has(slots, "address") && !Has(slots, "city")) missing.Add("place");
                break;
            case AiToolName.CreateSale:
                if (!Has(slots, "customerId") && !Has(slots, "customerName") && state.CustomerId is null)
                    missing.Add("customer");
                if (!Has(slots, "productName") && !Has(slots, "productId"))
                    missing.Add("product");
                if (!Has(slots, "quantity"))
                    missing.Add("quantity");
                break;
            case AiToolName.CreateProduct:
                if (!Has(slots, "name")) missing.Add("name");
                if (!Has(slots, "salePrice") && !Has(slots, "costPrice")) missing.Add("prices");
                else if (!Has(slots, "salePrice")) missing.Add("salePrice");
                break;
            case AiToolName.CreateSupplier:
                if (!Has(slots, "name")) missing.Add("supplierName");
                if (!Has(slots, "phone")) missing.Add("phone");
                if (!Has(slots, "email")) missing.Add("email");
                if (!Has(slots, "address")) missing.Add("address");
                break;
            case AiToolName.UpdateCustomer:
                if (!Has(slots, "customerId") && !Has(slots, "name") && !Has(slots, "customerName") && state.CustomerId is null)
                    missing.Add("customer");
                if (!Has(slots, "phone") && !Has(slots, "email") && !Has(slots, "address") && !Has(slots, "city") && !Has(slots, "firstName"))
                    missing.Add("change");
                break;
            case AiToolName.DeleteCustomer:
            case AiToolName.DeleteProduct:
            case AiToolName.DeleteSupplier:
                if (!HasIdentity(tool, slots, state))
                    missing.Add("name");
                break;
            case AiToolName.AdjustInventory:
            case AiToolName.ReceiveStock:
                if (!Has(slots, "productId") && !Has(slots, "productName") && !Has(slots, "sku"))
                    missing.Add("product");
                if (!Has(slots, "quantity"))
                    missing.Add("quantity");
                break;
            case AiToolName.CreatePurchaseOrder:
                if (!Has(slots, "productName") && !Has(slots, "productId"))
                    missing.Add("product");
                if (!Has(slots, "quantity"))
                    missing.Add("quantity");
                break;
            case AiToolName.CreateInvoice:
                if (!Has(slots, "orderId") && !Has(slots, "customerId") && !Has(slots, "customerName") && state.OrderId is null && state.CustomerId is null)
                    missing.Add("customer");
                break;
            case AiToolName.CancelInvoice:
                if (!Has(slots, "invoiceId") && !Has(slots, "invoiceNumber") && state.InvoiceId is null)
                    missing.Add("name");
                break;
        }

        return missing;
    }

    private static bool HasIdentity(AiToolName tool, Dictionary<string, string?> slots, AgentExecutionState state)
    {
        if (Has(slots, "name") || Has(slots, "customerName") || Has(slots, "fullName") || Has(slots, "productName") || Has(slots, "supplierName"))
            return true;

        return tool switch
        {
            AiToolName.DeleteCustomer => Has(slots, "customerId") || state.CustomerId is not null,
            AiToolName.DeleteProduct => Has(slots, "productId") || state.ProductId is not null,
            AiToolName.DeleteSupplier => Has(slots, "supplierId") || state.SupplierId is not null,
            _ => false
        };
    }

    private static string QuestionForMissing(AiToolName tool, IReadOnlyList<string> missing, Dictionary<string, string?> slots)
    {
        var known = KnownSummary(tool, slots);
        var lead = string.IsNullOrWhiteSpace(known) ? "" : known + " ";

        if (tool == AiToolName.CreateCustomer)
        {
            return lead + "I can add that customer, but I need a few details before I save anything: "
                + HumanList(missing.Select(Label))
                + ".\nYou can send them in one message. Example: Ahmed Ali, 03001234567, ahmed@gmail.com, Lahore.";
        }

        if (tool == AiToolName.CreateSale)
        {
            if (missing.Contains("customer") && missing.Contains("product"))
                return "I can create that order. Who is it for, which product, and how many?\nExample: Ahmed Ali, Laptop, quantity 2.";
            if (missing.Contains("product") || missing.Contains("quantity"))
            {
                var who = DisplayCustomer(slots);
                var prefix = string.IsNullOrWhiteSpace(who)
                    ? "I can create that order."
                    : $"I'll create the order for {who}.";
                return prefix + " Which product, and how many?\nExample: Laptop, quantity 2.";
            }
            return "Which customer is this order for?";
        }

        if (tool == AiToolName.CreateProduct)
        {
            if (missing.Contains("name") && missing.Contains("prices"))
                return "I can add that product. What is the name, sale price, and cost price?\nExample: Wireless Mouse, sale 2500, cost 1800.";
            if (missing.Contains("prices") || missing.Contains("salePrice"))
                return $"What sale price and cost price should I use for {Get(slots, "name") ?? "this product"}?\nExample: sale 2500, cost 1800.";
            return "What should I name this product?";
        }

        if (tool == AiToolName.CreateSupplier)
            return "I can add that supplier. I need the name, phone, email, and address before I save them.\nExample: Acme Traders, 03001112233, acme@gmail.com, Lahore.";

        if (tool == AiToolName.CreatePurchaseOrder)
            return "Which product should I order, and how many?\nExample: Laptop, quantity 5.";

        if (tool == AiToolName.UpdateCustomer && missing.Contains("change"))
            return "What should I change? Tell me the new phone, email, or address.";

        if (tool is AiToolName.DeleteCustomer or AiToolName.DeleteProduct or AiToolName.DeleteSupplier)
            return "Which record should I delete? Tell me the name.";

        if (missing.Contains("quantity"))
            return "How many units?";
        if (missing.Contains("product"))
            return "Which product?";
        if (missing.Contains("customer"))
            return "Which customer is this for?";

        return "I need a bit more detail before I do that: " + HumanList(missing.Select(Label)) + ".";
    }

    private static string Confirmation(AiToolName tool, Dictionary<string, string?> slots)
    {
        var lines = new List<string>();
        switch (tool)
        {
            case AiToolName.CreateCustomer:
                lines.Add($"Name: {DisplayPerson(slots)}");
                lines.Add($"Phone: {Get(slots, "phone")}");
                lines.Add($"Email: {Get(slots, "email")}");
                lines.Add($"Address: {Get(slots, "address") ?? Get(slots, "city")}");
                lines.Add(Has(slots, "country")
                    ? $"Country: {Get(slots, "country")}"
                    : "Country: Pakistan, unless you want a different one");
                return "Here's the customer I'll create:\n" + Bullet(lines) + "\nShould I create this customer?";
            case AiToolName.CreateSale:
                return $"I'll create an order for {DisplayCustomer(slots)}: {Get(slots, "productName") ?? "the selected product"} × {Get(slots, "quantity")}.\nShould I go ahead?";
            case AiToolName.CreateProduct:
                var costNote = Has(slots, "costPrice")
                    ? Get(slots, "costPrice")
                    : $"{Get(slots, "salePrice")} (same as the sale price, unless you tell me otherwise)";
                return $"I'll create product {Get(slots, "name")}.\nSale price: {Get(slots, "salePrice") ?? Get(slots, "costPrice")}\nCost price: {costNote}\nI'll generate a SKU if you don't give one.\nShould I create this product?";
            case AiToolName.CreateSupplier:
                return $"I'll create supplier {Get(slots, "name")}.\nPhone: {Get(slots, "phone")}\nEmail: {Get(slots, "email")}\nAddress: {Get(slots, "address")}\nShould I save this supplier?";
            case AiToolName.DeleteCustomer:
            case AiToolName.DeleteProduct:
            case AiToolName.DeleteSupplier:
                return $"Delete {DisplayPerson(slots)}? This removes the record.\nShould I go ahead?";
            case AiToolName.CreatePurchaseOrder:
                var supplier = Get(slots, "supplierName");
                var from = string.IsNullOrWhiteSpace(supplier) ? "" : $" from {supplier}";
                return $"I'll create a purchase order for {Get(slots, "productName")} × {Get(slots, "quantity")}{from}.\nShould I go ahead?";
            case AiToolName.CreatePurchaseOrderDraft:
                return "I'll prepare a purchase order draft from products that are low on stock. Nothing is sent to a supplier until you review it.\nShould I create that draft?";
            case AiToolName.CreateInvoice:
                return "I'll create an invoice from the latest matching order, due in 14 days.\nShould I go ahead?";
            case AiToolName.AdjustInventory:
            case AiToolName.ReceiveStock:
                var verb = tool == AiToolName.ReceiveStock ? "receive" : "adjust";
                return $"I'll {verb} {Get(slots, "quantity")} units of {Get(slots, "productName") ?? "the selected product"}.\nShould I go ahead?";
            default:
                return "I have what I need for this change.\nShould I go ahead?";
        }
    }

    private static string KnownSummary(AiToolName tool, Dictionary<string, string?> slots)
    {
        if (tool == AiToolName.CreateCustomer && (Has(slots, "fullName") || Has(slots, "firstName")))
            return $"I have the name {DisplayPerson(slots)}.";
        return "";
    }

    private static JsonElement BuildArgs(AiToolName tool, Dictionary<string, string?> slots)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var pair in slots)
        {
            if (string.IsNullOrWhiteSpace(pair.Value))
                continue;
            if (pair.Key is "salePrice" or "costPrice" or "quantity" or "discount" or "tax" or "reorderLevel" or "dueDays")
            {
                if (decimal.TryParse(pair.Value, out var number))
                    dict[pair.Key] = number;
                continue;
            }

            dict[pair.Key] = pair.Value;
        }

        if (tool == AiToolName.CreateSale)
        {
            var item = new Dictionary<string, object?>();
            if (dict.TryGetValue("productId", out var pid))
                item["productId"] = pid;
            if (dict.TryGetValue("productName", out var pname))
                item["productName"] = pname;
            item["quantity"] = dict.TryGetValue("quantity", out var qty) ? qty : 1m;
            dict["items"] = new List<object> { item };
        }

        var json = JsonSerializer.Serialize(dict);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static string ConfirmLabel(AiToolName tool) => tool switch
    {
        AiToolName.DeleteCustomer or AiToolName.DeleteProduct or AiToolName.DeleteSupplier => "Yes, delete it",
        AiToolName.UpdateCustomer or AiToolName.UpdateProduct or AiToolName.UpdateSupplier => "Yes, update it",
        AiToolName.CreateSale => "Yes, create the order",
        AiToolName.CreateProduct => "Yes, create the product",
        AiToolName.CreateSupplier => "Yes, create the supplier",
        _ => "Yes, create it"
    };

    private static string ConfirmMessage(AiToolName tool) => ConfirmLabel(tool);

    private static AiToolName? GuessCommandTool(string lower)
    {
        var delete = lower.Contains("delete") || lower.Contains("remove");
        if (lower.Contains("customer") || lower.Contains("client"))
            return delete ? AiToolName.DeleteCustomer : AiToolName.CreateCustomer;
        if (lower.Contains("supplier") || lower.Contains("vendor"))
            return delete ? AiToolName.DeleteSupplier : AiToolName.CreateSupplier;
        if (lower.Contains("product") || lower.Contains("item"))
            return delete ? AiToolName.DeleteProduct : AiToolName.CreateProduct;
        if (lower.Contains("purchase order") || lower.Contains("purchase"))
            return AiToolName.CreatePurchaseOrder;
        if ((lower.Contains("order") || lower.Contains("sale")) && !lower.Contains("purchase"))
            return AiToolName.CreateSale;
        if (lower.Contains("invoice"))
            return AiToolName.CreateInvoice;
        return null;
    }

    private static string? WordQuantity(string message)
    {
        var lower = message.ToLowerInvariant();
        var words = new Dictionary<string, string>
        {
            ["one"] = "1", ["two"] = "2", ["three"] = "3", ["four"] = "4", ["five"] = "5",
            ["six"] = "6", ["seven"] = "7", ["eight"] = "8", ["nine"] = "9", ["ten"] = "10"
        };
        foreach (var pair in words)
        {
            if (Regex.IsMatch(lower, $@"\b{pair.Key}\b"))
                return pair.Value;
        }
        return null;
    }

    private static void TakeLabeled(string message, Dictionary<string, string?> slots, string slot, string label)
    {
        var match = Regex.Match(message, $@"(?:{label})\s*(?:is|:)?\s*([^,\n.]+)", RegexOptions.IgnoreCase);
        if (match.Success)
            slots[slot] = match.Groups[1].Value.Trim();
    }

    private static bool LooksLikeName(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var trimmed = text.Trim();
        if (trimmed.Length is < 2 or > 80)
            return false;
        if (!Regex.IsMatch(trimmed, @"^[A-Za-z][A-Za-z\s'.-]{1,80}$"))
            return false;
        var lower = trimmed.ToLowerInvariant();
        return lower is not "customer" and not "product" and not "order" and not "supplier" and not "invoice" and not "yes" and not "no";
    }

    private static string CleanCommandWords(string text)
    {
        var cleaned = Regex.Replace(
            text,
            @"\b(create|add|new|customer|client|product|order|sale|supplier|please|the|a|an|named|called|for|with|his|her|name|is|number|phone|email|address)\b",
            " ",
            RegexOptions.IgnoreCase);
        return Regex.Replace(cleaned, @"\s+", " ").Trim(' ', ',', '.', ':');
    }

    private static bool IsKnownCity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        return value.Trim().ToLowerInvariant() is "lahore" or "karachi" or "islamabad" or "rawalpindi"
            or "faisalabad" or "multan" or "peshawar" or "quetta";
    }

    private static bool IsUnusableValue(string value)
    {
        var t = Normalize(value);
        return t.Length < 2 || ConfirmPhrases.Contains(t) || CancelPhrases.Contains(t) || SmallTalk.Contains(t);
    }

    private static bool IsPureConfirmation(string message) => SaleOrderLanguage.IsConfirmation(message);

    public static string Write(AgentPendingAction pending) =>
        JsonSerializer.Serialize(pending, JsonOptions);

    public static JsonElement BuildExecutionArgs(AiToolName tool, Dictionary<string, string?> slots) =>
        BuildArgs(tool, slots);

    private static bool ShouldKeepSlot(
        string key,
        string newValue,
        Dictionary<string, string?> slots,
        string message)
    {
        if (!Has(slots, key))
            return false;
        if (string.Equals(Get(slots, key), newValue, StringComparison.OrdinalIgnoreCase))
            return false;

        if (key.Equals("customerId", StringComparison.OrdinalIgnoreCase)
            || key.Equals("productId", StringComparison.OrdinalIgnoreCase))
            return true;

        if (key.Equals("customerName", StringComparison.OrdinalIgnoreCase)
            && !message.Contains(newValue, StringComparison.OrdinalIgnoreCase))
            return true;

        if (key.Equals("productName", StringComparison.OrdinalIgnoreCase) && IsPureConfirmation(message))
            return true;

        if (key.Equals("quantity", StringComparison.OrdinalIgnoreCase)
            && !SaleOrderLanguage.TryParseQuantity(message, out _))
            return true;

        return false;
    }

    private static bool IsCancel(string message) => CancelPhrases.Contains(Normalize(message));

    private static string Normalize(string message)
    {
        var t = message.Trim().ToLowerInvariant().Replace("'", "");
        t = Regex.Replace(t, @"[!.?,]", " ");
        return Regex.Replace(t, @"\s+", " ").Trim();
    }

    private static bool Has(Dictionary<string, string?> slots, string key) =>
        !string.IsNullOrWhiteSpace(Get(slots, key));

    private static string? Get(Dictionary<string, string?> slots, string key) =>
        slots.TryGetValue(key, out var value) ? value : null;

    private static string DisplayPerson(Dictionary<string, string?> slots)
    {
        var full = Get(slots, "fullName") ?? Get(slots, "name") ?? Get(slots, "customerName") ?? Get(slots, "productName");
        if (!string.IsNullOrWhiteSpace(full))
            return full.EndsWith(" .", StringComparison.Ordinal) ? full[..^2] : full.Replace(" .", " ").Trim();

        var first = Get(slots, "firstName");
        var last = Get(slots, "lastName");
        if (string.IsNullOrWhiteSpace(last) || last == ".")
            return first ?? "this record";
        return $"{first} {last}".Trim();
    }

    private static string DisplayCustomer(Dictionary<string, string?> slots)
    {
        if (Has(slots, "customerName") || Has(slots, "fullName") || Has(slots, "name"))
            return DisplayPerson(slots);
        if (Has(slots, "customerId"))
            return "the open customer";
        return "the customer";
    }

    private static string Label(string field) => field switch
    {
        "customerName" or "name" => "full name",
        "phone" => "phone",
        "email" => "email",
        "place" or "address" => "city or address",
        "product" => "product",
        "quantity" => "quantity",
        "customer" => "customer",
        "prices" or "salePrice" => "sale price and cost price",
        "supplierName" => "supplier name",
        "change" => "what to change",
        _ => field
    };

    private static string HumanList(IEnumerable<string> items)
    {
        var list = items.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        if (list.Count == 0) return "a few details";
        if (list.Count == 1) return list[0];
        if (list.Count == 2) return $"{list[0]} and {list[1]}";
        return string.Join(", ", list.Take(list.Count - 1)) + ", and " + list[^1];
    }

    private static string Bullet(IEnumerable<string> lines) =>
        string.Join("\n", lines.Select(line => "• " + line));
}
