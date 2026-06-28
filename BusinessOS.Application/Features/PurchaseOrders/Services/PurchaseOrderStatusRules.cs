using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Features.PurchaseOrders.Services;

public static class PurchaseOrderStatusRules
{
    private static readonly Dictionary<string, HashSet<string>> AllowedTransitions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [PurchaseOrderStatusNames.Draft] = new(StringComparer.OrdinalIgnoreCase)
            {
                PurchaseOrderStatusNames.Pending,
                PurchaseOrderStatusNames.Cancelled
            },
            [PurchaseOrderStatusNames.Pending] = new(StringComparer.OrdinalIgnoreCase)
            {
                PurchaseOrderStatusNames.Approved,
                PurchaseOrderStatusNames.Cancelled,
                PurchaseOrderStatusNames.Draft
            },
            [PurchaseOrderStatusNames.Approved] = new(StringComparer.OrdinalIgnoreCase)
            {
                PurchaseOrderStatusNames.Cancelled
            },
            [PurchaseOrderStatusNames.Received] = new(StringComparer.OrdinalIgnoreCase),
            [PurchaseOrderStatusNames.Cancelled] = new(StringComparer.OrdinalIgnoreCase)
        };

    public static bool CanTransition(string currentStatus, string newStatus) =>
        AllowedTransitions.TryGetValue(currentStatus, out var allowed) &&
        allowed.Contains(newStatus);

    public static void ValidateTransition(string currentStatus, string newStatus)
    {
        if (!PurchaseOrderStatusNames.IsValid(newStatus))
            throw new BadRequestException($"Invalid purchase order status '{newStatus}'.");

        if (string.Equals(currentStatus, newStatus, StringComparison.OrdinalIgnoreCase))
            return;

        if (!CanTransition(currentStatus, newStatus))
        {
            throw new BadRequestException(
                $"Cannot transition purchase order from '{currentStatus}' to '{newStatus}'.");
        }
    }

    public static bool IsEditable(string status) =>
        string.Equals(status, PurchaseOrderStatusNames.Draft, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, PurchaseOrderStatusNames.Pending, StringComparison.OrdinalIgnoreCase);

    public static bool CanDelete(string status) =>
        !string.Equals(status, PurchaseOrderStatusNames.Received, StringComparison.OrdinalIgnoreCase);

    public static bool CanReceive(string status) =>
        string.Equals(status, PurchaseOrderStatusNames.Approved, StringComparison.OrdinalIgnoreCase);
}
