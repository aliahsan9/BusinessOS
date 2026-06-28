using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Features.Invoices.Services;

public static class InvoiceStatusRules
{
    private static readonly Dictionary<string, HashSet<string>> AllowedTransitions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [InvoiceStatusNames.Draft] = new(StringComparer.OrdinalIgnoreCase)
            {
                InvoiceStatusNames.Sent,
                InvoiceStatusNames.Cancelled
            },
            [InvoiceStatusNames.Sent] = new(StringComparer.OrdinalIgnoreCase)
            {
                InvoiceStatusNames.Paid,
                InvoiceStatusNames.PartiallyPaid,
                InvoiceStatusNames.Overdue,
                InvoiceStatusNames.Cancelled
            },
            [InvoiceStatusNames.PartiallyPaid] = new(StringComparer.OrdinalIgnoreCase)
            {
                InvoiceStatusNames.Paid,
                InvoiceStatusNames.Overdue
            },
            [InvoiceStatusNames.Overdue] = new(StringComparer.OrdinalIgnoreCase)
            {
                InvoiceStatusNames.Paid,
                InvoiceStatusNames.PartiallyPaid,
                InvoiceStatusNames.Cancelled
            },
            [InvoiceStatusNames.Paid] = new(StringComparer.OrdinalIgnoreCase),
            [InvoiceStatusNames.Cancelled] = new(StringComparer.OrdinalIgnoreCase)
        };

    public static bool CanTransition(string currentStatus, string newStatus) =>
        AllowedTransitions.TryGetValue(currentStatus, out var allowed) &&
        allowed.Contains(newStatus);

    public static void ValidateTransition(string currentStatus, string newStatus)
    {
        if (!InvoiceStatusNames.IsValid(newStatus))
            throw new BadRequestException($"Invalid invoice status '{newStatus}'.");

        if (string.Equals(currentStatus, newStatus, StringComparison.OrdinalIgnoreCase))
            return;

        if (!CanTransition(currentStatus, newStatus))
        {
            throw new BadRequestException(
                $"Cannot transition invoice from '{currentStatus}' to '{newStatus}'.");
        }
    }

    public static bool IsEditable(string status) =>
        string.Equals(status, InvoiceStatusNames.Draft, StringComparison.OrdinalIgnoreCase);

    public static bool CanDelete(string status) =>
        string.Equals(status, InvoiceStatusNames.Draft, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, InvoiceStatusNames.Cancelled, StringComparison.OrdinalIgnoreCase);
}
