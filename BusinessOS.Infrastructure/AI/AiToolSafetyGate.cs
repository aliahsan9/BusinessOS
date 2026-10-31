using System.Text.Json;
using BusinessOS.Application.Features.AI.Enums;

namespace BusinessOS.Infrastructure.AI;

/// <summary>
/// Rejects untrusted tool requests before they reach business services.
/// Prompt text is not treated as authorization.
/// </summary>
public static class AiToolSafetyGate
{
    private static readonly HashSet<AiToolName> MutatingTools =
    [
        AiToolName.CreateCustomer,
        AiToolName.UpdateCustomer,
        AiToolName.DeleteCustomer,
        AiToolName.CreateProduct,
        AiToolName.UpdateProduct,
        AiToolName.DeleteProduct,
        AiToolName.AdjustInventory,
        AiToolName.ReceiveStock,
        AiToolName.CreateSale,
        AiToolName.CreateInvoice,
        AiToolName.CancelInvoice,
        AiToolName.CreatePurchaseOrder,
        AiToolName.CreatePurchaseOrderDraft,
        AiToolName.ApprovePurchaseOrder,
        AiToolName.ReceivePurchase,
        AiToolName.CreateSupplier,
        AiToolName.UpdateSupplier,
        AiToolName.DeleteSupplier,
        AiToolName.UpdateCompanyProfile,
        AiToolName.UpdateTaxDefaults,
        AiToolName.ApplyOnboardingProfile,
        AiToolName.CreateProject,
        AiToolName.CreateTask
    ];

    public static bool TryBlock(AiToolName tool, string? message, JsonElement args, out string reason)
    {
        var text = message ?? string.Empty;
        if (IsInstructionOverride(text) && MutatingTools.Contains(tool))
        {
            reason = "The assistant will not run a business change from an instruction-override request.";
            return true;
        }

        if (RequestsUnboundedDestruction(text) && MutatingTools.Contains(tool))
        {
            reason = "Bulk deletion is not available through the assistant.";
            return true;
        }

        if (HasUnboundedArgument(args))
        {
            reason = "Tool arguments that request an unbounded change were rejected.";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    public static bool IsInstructionOverride(string message)
    {
        var text = message.ToLowerInvariant();
        return text.Contains("ignore previous instructions", StringComparison.Ordinal)
            || text.Contains("ignore all previous", StringComparison.Ordinal)
            || text.Contains("disregard your instructions", StringComparison.Ordinal)
            || text.Contains("disregard previous", StringComparison.Ordinal)
            || text.Contains("override the system", StringComparison.Ordinal)
            || text.Contains("you are now", StringComparison.Ordinal);
    }

    private static bool RequestsUnboundedDestruction(string message)
    {
        var text = message.ToLowerInvariant();
        return text.Contains("delete all", StringComparison.Ordinal)
            || text.Contains("delete every", StringComparison.Ordinal)
            || text.Contains("remove all orders", StringComparison.Ordinal)
            || text.Contains("wipe all", StringComparison.Ordinal);
    }

    private static bool HasUnboundedArgument(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in args.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.True &&
                property.Name.Contains("all", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (property.Value.ValueKind == JsonValueKind.String &&
                property.Name.Contains("scope", StringComparison.OrdinalIgnoreCase))
            {
                var scope = property.Value.GetString();
                if (string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase) ||
                    scope == "*")
                {
                    return true;
                }
            }
        }

        return false;
    }
}
