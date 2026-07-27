using System.Text.Json;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.Agents.Enums;
using BusinessOS.Application.Features.Agents.Services;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;

namespace BusinessOS.Infrastructure.AI.Agents.Runtime;

public sealed class AgentSelfCorrector : IAgentSelfCorrector
{
    public AgentSelfCorrectionResult Analyze(
        AiToolName toolName,
        AiToolResult result,
        Exception? exception,
        JsonElement? args,
        string language)
    {
        var message = exception?.Message ?? result.Summary ?? "";
        var lower = message.ToLowerInvariant();

        if (result.Success && exception is null)
            return new AgentSelfCorrectionResult { FailureKind = AgentFailureKind.None };

        if (LooksLikeDuplicate(lower))
        {
            var alt = SuggestUpdateTool(toolName);
            return new AgentSelfCorrectionResult
            {
                FailureKind = AgentFailureKind.DuplicateEntity,
                ShouldRetry = false,
                NeedsClarification = true,
                AlternateTool = alt,
                SuggestedFixMessage = "That record already exists. Would you like me to update it instead?",
                ClarificationMessage = "That record already exists. Would you like me to update it instead?",
                Suggestions =
                [
                    new AiSuggestionDto
                    {
                        Label = "Yes, update it",
                        Message = alt is null
                            ? "Update the existing record"
                            : $"Update the existing {toolName} using alternate {alt}"
                    },
                    new AiSuggestionDto
                    {
                        Label = "No, leave it",
                        Message = "Cancel"
                    }
                ]
            };
        }

        if (LooksLikeNotFound(lower))
        {
            var search = SuggestSearchTool(toolName);
            return new AgentSelfCorrectionResult
            {
                FailureKind = AgentFailureKind.NotFound,
                ShouldRetry = search is not null,
                AlternateTool = search,
                SuggestedFixMessage = "I couldn't find that record. I'll search and retry.",
                ClarificationMessage = search is null
                    ? "Record not found. Please provide the correct name or ID."
                    : null
            };
        }

        if (LooksLikePermission(lower))
        {
            return new AgentSelfCorrectionResult
            {
                FailureKind = AgentFailureKind.PermissionDenied,
                NeedsClarification = true,
                ClarificationMessage = "You don't have permission to perform this action."
            };
        }

        var missing = DetectMissingFields(toolName, args, lower);
        if (missing.Count > 0 || LooksLikeValidation(lower))
        {
            var suggestions = new List<AiSuggestionDto>();
            if (toolName is AiToolName.CreatePurchaseOrder or AiToolName.CreatePurchaseOrderDraft)
            {
                suggestions.Add(new AiSuggestionDto
                {
                    Label = "Draft from low stock",
                    Message = "Create purchase order draft from low stock"
                });
                suggestions.Add(new AiSuggestionDto
                {
                    Label = "Order Laptop x5",
                    Message = "Create purchase order for Laptop quantity 5"
                });
            }

            return new AgentSelfCorrectionResult
            {
                FailureKind = AgentFailureKind.ValidationMissingFields,
                NeedsClarification = true,
                MissingFields = missing,
                ClarificationMessage = BuildMissingFieldsPrompt(missing, message),
                Suggestions = suggestions
            };
        }

        if (LooksLikeTransient(lower) || exception is TimeoutException or HttpRequestException)
        {
            return new AgentSelfCorrectionResult
            {
                FailureKind = AgentFailureKind.Transient,
                ShouldRetry = true,
                SuggestedFixMessage = "A temporary issue occurred. Retrying once."
            };
        }

        return new AgentSelfCorrectionResult
        {
            FailureKind = AgentFailureKind.Unknown,
            NeedsClarification = true,
            ClarificationMessage = $"That action failed: {Truncate(message, 180)}"
        };
    }

    private static AiToolName? SuggestUpdateTool(AiToolName tool) => tool switch
    {
        AiToolName.CreateCustomer => AiToolName.UpdateCustomer,
        AiToolName.CreateProduct => AiToolName.UpdateProduct,
        AiToolName.CreateSupplier => AiToolName.UpdateSupplier,
        _ => null
    };

    private static AiToolName? SuggestSearchTool(AiToolName tool) => tool switch
    {
        AiToolName.UpdateCustomer or AiToolName.DeleteCustomer or AiToolName.CreateInvoice or AiToolName.CreateSale
            => AiToolName.SearchCustomer,
        AiToolName.UpdateProduct or AiToolName.DeleteProduct or AiToolName.AdjustInventory or AiToolName.ReceiveStock
            => AiToolName.SearchProduct,
        AiToolName.UpdateSupplier or AiToolName.DeleteSupplier
            => AiToolName.SearchSupplier,
        AiToolName.CreatePurchaseOrder or AiToolName.CreatePurchaseOrderDraft
            => AiToolName.SearchProduct,
        AiToolName.CancelInvoice => AiToolName.SearchInvoice,
        _ => null
    };

    private static List<string> DetectMissingFields(AiToolName tool, JsonElement? args, string lower)
    {
        var missing = new List<string>();
        if (args is null)
            return missing;

        var el = args.Value;
        switch (tool)
        {
            case AiToolName.CreateCustomer:
                if (!Has(el, "firstName") && !Has(el, "fullName")) missing.Add("customerName");
                if (!Has(el, "phone") && lower.Contains("phone")) missing.Add("phone");
                break;
            case AiToolName.CreateProduct:
                if (!Has(el, "name")) missing.Add("productName");
                break;
            case AiToolName.CreateSupplier:
                if (!Has(el, "name")) missing.Add("supplierName");
                break;
            case AiToolName.CreatePurchaseOrder:
            case AiToolName.CreatePurchaseOrderDraft:
                if (!HasItems(el) && !Has(el, "productName") && !Has(el, "productId")
                    && (lower.Contains("line item") || lower.Contains("product") || lower.Contains("which product")
                        || string.IsNullOrWhiteSpace(lower) || lower.Contains("required")))
                    missing.Add("product");
                if (lower.Contains("supplier"))
                    missing.Add("supplier");
                break;
            case AiToolName.AdjustInventory:
            case AiToolName.ReceiveStock:
                if (!HasNumber(el, "quantity")) missing.Add("quantity");
                if (!Has(el, "productId") && !Has(el, "productName") && !Has(el, "sku"))
                    missing.Add("product");
                break;
            case AiToolName.CreateSale:
                if (!Has(el, "customerId") && !Has(el, "customerName"))
                    missing.Add("customer");
                if (!HasItems(el) && !Has(el, "productName") && !Has(el, "productId")
                    && (lower.Contains("product") || lower.Contains("line item") || lower.Contains("required")))
                    missing.Add("product");
                break;
        }

        if (lower.Contains("required") || lower.Contains("must"))
        {
            foreach (var token in new[] { "email", "phone", "name", "quantity", "customer", "product", "address", "supplier" })
            {
                if (lower.Contains(token) && !missing.Contains(token) && !missing.Any(m => m.Contains(token, StringComparison.OrdinalIgnoreCase)))
                    missing.Add(token);
            }
        }

        return missing;
    }

    private static string BuildMissingFieldsPrompt(IReadOnlyList<string> missing, string fallback)
    {
        if (missing.Count == 0)
            return NaturalClarify(fallback);

        // Ask one focused question like a real teammate — not "Please provide: name".
        var field = missing[0];
        return FieldQuestion(field);
    }

    private static string FieldQuestion(string field)
    {
        var key = field.Trim().ToLowerInvariant();
        if (key.Contains("customer"))
            return "Which customer is this for? Tell me their name, or open the customer page and say \"Create order for this customer\".";
        if (key.Contains("supplier"))
            return "What's the supplier name? Example: \"Create supplier Acme\".";
        if (key.Contains("product") || key.Contains("item"))
            return "Which product and how many? Example: \"Laptop quantity 5\".";
        if (key.Contains("quantity") || key.Contains("qty"))
            return "How many units?";
        if (key.Contains("phone"))
            return "What's the phone number?";
        if (key.Contains("email"))
            return "What's the email address?";
        if (key.Contains("customername") || key is "name")
            return "What's the customer's full name?";
        if (key.Contains("productname"))
            return "What's the product name?";
        if (key.Contains("suppliername"))
            return "What's the supplier's name?";

        return $"Could you tell me the {field}?";
    }

    private static string NaturalClarify(string fallback)
    {
        var lower = fallback.ToLowerInvariant();
        if (lower.Contains("customer"))
            return FieldQuestion("customer");
        if (lower.Contains("product") || lower.Contains("line item"))
            return FieldQuestion("product");
        if (lower.Contains("supplier"))
            return FieldQuestion("supplier");
        return Truncate(fallback, 280);
    }

    private static bool HasItems(JsonElement el) =>
        el.TryGetProperty("items", out var items)
        && items.ValueKind == JsonValueKind.Array
        && items.GetArrayLength() > 0;

    private static bool Has(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p)
        && p.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(p.GetString());

    private static bool HasNumber(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number;

    private static bool LooksLikeDuplicate(string lower) =>
        lower.Contains("already exists")
        || lower.Contains("duplicate")
        || lower.Contains("unique constraint")
        || lower.Contains("already registered");

    private static bool LooksLikeNotFound(string lower) =>
        lower.Contains("not found")
        || lower.Contains("does not exist")
        || lower.Contains("no customer")
        || lower.Contains("no product")
        || lower.Contains("could not find");

    private static bool LooksLikePermission(string lower) =>
        lower.Contains("permission")
        || lower.Contains("forbidden")
        || lower.Contains("unauthorized")
        || lower.Contains("not allowed");

    private static bool LooksLikeValidation(string lower) =>
        lower.Contains("validation")
        || lower.Contains("required")
        || lower.Contains("invalid")
        || lower.Contains("must be");

    private static bool LooksLikeTransient(string lower) =>
        lower.Contains("timeout")
        || lower.Contains("temporar")
        || lower.Contains("try again")
        || lower.Contains("unavailable");

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";
}
