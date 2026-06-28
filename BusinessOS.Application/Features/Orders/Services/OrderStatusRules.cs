using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Features.Orders.Services;

public static class OrderStatusRules
{
    private static readonly Dictionary<string, HashSet<string>> AllowedTransitions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [OrderStatusNames.Pending] = new(StringComparer.OrdinalIgnoreCase)
            {
                OrderStatusNames.Confirmed,
                OrderStatusNames.Cancelled
            },
            [OrderStatusNames.Confirmed] = new(StringComparer.OrdinalIgnoreCase)
            {
                OrderStatusNames.Processing,
                OrderStatusNames.Cancelled
            },
            [OrderStatusNames.Processing] = new(StringComparer.OrdinalIgnoreCase)
            {
                OrderStatusNames.Completed,
                OrderStatusNames.Cancelled
            },
            [OrderStatusNames.Completed] = new(StringComparer.OrdinalIgnoreCase),
            [OrderStatusNames.Cancelled] = new(StringComparer.OrdinalIgnoreCase)
        };

    public static bool CanTransition(string currentStatus, string newStatus) =>
        AllowedTransitions.TryGetValue(currentStatus, out var allowed) &&
        allowed.Contains(newStatus);

    public static void ValidateTransition(string currentStatus, string newStatus)
    {
        if (!OrderStatusNames.IsValid(newStatus))
            throw new BadRequestException($"Invalid order status '{newStatus}'.");

        if (string.Equals(currentStatus, newStatus, StringComparison.OrdinalIgnoreCase))
            return;

        if (!CanTransition(currentStatus, newStatus))
        {
            throw new BadRequestException(
                $"Cannot transition order from '{currentStatus}' to '{newStatus}'.");
        }
    }

    public static bool IsEditable(string status) =>
        string.Equals(status, OrderStatusNames.Pending, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, OrderStatusNames.Confirmed, StringComparison.OrdinalIgnoreCase);
}
