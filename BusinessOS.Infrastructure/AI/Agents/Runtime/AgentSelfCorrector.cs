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
        var isUrdu = AgentLanguages.Normalize(language) == AgentLanguages.Urdu;
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
                SuggestedFixMessage = isUrdu
                    ? "یہ ریکارڈ پہلے سے موجود ہے۔ کیا میں اسے اپڈیٹ کر دوں؟"
                    : "That record already exists. Would you like me to update it instead?",
                ClarificationMessage = isUrdu
                    ? "یہ ریکارڈ پہلے سے موجود ہے۔ کیا میں اسے اپڈیٹ کر دوں؟"
                    : "That record already exists. Would you like me to update it instead?",
                Suggestions =
                [
                    new AiSuggestionDto
                    {
                        Label = isUrdu ? "ہاں، اپڈیٹ کرو" : "Yes, update it",
                        Message = alt is null
                            ? "Update the existing record"
                            : $"Update the existing {toolName} using alternate {alt}"
                    },
                    new AiSuggestionDto
                    {
                        Label = isUrdu ? "نہیں" : "No, leave it",
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
                SuggestedFixMessage = isUrdu
                    ? "ریکارڈ نہیں ملا۔ میں تلاش کر کے دوبارہ کوشش کرتی ہوں۔"
                    : "I couldn't find that record. I'll search and retry.",
                ClarificationMessage = search is null
                    ? (isUrdu ? "ریکارڈ نہیں ملا۔ براہ کرم درست نام یا آئی ڈی دیں۔" : "Record not found. Please provide the correct name or ID.")
                    : null
            };
        }

        if (LooksLikePermission(lower))
        {
            return new AgentSelfCorrectionResult
            {
                FailureKind = AgentFailureKind.PermissionDenied,
                NeedsClarification = true,
                ClarificationMessage = isUrdu
                    ? "آپ کے پاس اس عمل کی اجازت نہیں ہے۔"
                    : "You don't have permission to perform this action."
            };
        }

        var missing = DetectMissingFields(toolName, args, lower);
        if (missing.Count > 0 || LooksLikeValidation(lower))
        {
            return new AgentSelfCorrectionResult
            {
                FailureKind = AgentFailureKind.ValidationMissingFields,
                NeedsClarification = true,
                MissingFields = missing,
                ClarificationMessage = BuildMissingFieldsPrompt(missing, isUrdu, message)
            };
        }

        if (LooksLikeTransient(lower) || exception is TimeoutException or HttpRequestException)
        {
            return new AgentSelfCorrectionResult
            {
                FailureKind = AgentFailureKind.Transient,
                ShouldRetry = true,
                SuggestedFixMessage = isUrdu
                    ? "عارضی مسئلہ آیا۔ دوبارہ کوشش کر رہی ہوں۔"
                    : "A temporary issue occurred. Retrying once."
            };
        }

        return new AgentSelfCorrectionResult
        {
            FailureKind = AgentFailureKind.Unknown,
            NeedsClarification = true,
            ClarificationMessage = isUrdu
                ? $"عمل ناکام رہا: {Truncate(message, 180)}"
                : $"That action failed: {Truncate(message, 180)}"
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
        AiToolName.UpdateSupplier or AiToolName.DeleteSupplier or AiToolName.CreatePurchaseOrder
            => AiToolName.SearchSupplier,
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
                if (!Has(el, "firstName") && !Has(el, "fullName")) missing.Add("name");
                if (!Has(el, "phone") && lower.Contains("phone")) missing.Add("phone");
                break;
            case AiToolName.CreateProduct:
                if (!Has(el, "name")) missing.Add("name");
                break;
            case AiToolName.CreateSupplier:
                if (!Has(el, "name")) missing.Add("name");
                break;
            case AiToolName.AdjustInventory:
            case AiToolName.ReceiveStock:
                if (!HasNumber(el, "quantity")) missing.Add("quantity");
                if (!Has(el, "productId") && !Has(el, "productName") && !Has(el, "sku"))
                    missing.Add("product");
                break;
            case AiToolName.CreateSale:
                if (!Has(el, "customerId") && !Has(el, "customerName")) missing.Add("customer");
                break;
        }

        if (lower.Contains("required") || lower.Contains("must"))
        {
            foreach (var token in new[] { "email", "phone", "name", "quantity", "customer", "product", "address" })
            {
                if (lower.Contains(token) && !missing.Contains(token))
                    missing.Add(token);
            }
        }

        return missing;
    }

    private static string BuildMissingFieldsPrompt(IReadOnlyList<string> missing, bool isUrdu, string fallback)
    {
        if (missing.Count == 0)
            return isUrdu ? $"تفصیل مکمل نہیں: {Truncate(fallback, 160)}" : $"I still need more details: {Truncate(fallback, 160)}";

        var list = string.Join(", ", missing);
        return isUrdu
            ? $"براہ کرم یہ معلومات دیں: {list}"
            : $"Please provide: {list}.";
    }

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
