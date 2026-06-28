using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Features.Quotations.Services;

public static class QuotationStatusRules
{
    private static readonly Dictionary<string, HashSet<string>> AllowedTransitions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [QuotationStatusNames.Draft] = new(StringComparer.OrdinalIgnoreCase)
            {
                QuotationStatusNames.Sent,
                QuotationStatusNames.Rejected
            },
            [QuotationStatusNames.Sent] = new(StringComparer.OrdinalIgnoreCase)
            {
                QuotationStatusNames.Accepted,
                QuotationStatusNames.Rejected,
                QuotationStatusNames.Expired
            },
            [QuotationStatusNames.Accepted] = new(StringComparer.OrdinalIgnoreCase),
            [QuotationStatusNames.Rejected] = new(StringComparer.OrdinalIgnoreCase),
            [QuotationStatusNames.Expired] = new(StringComparer.OrdinalIgnoreCase)
        };

    public static bool CanTransition(string currentStatus, string newStatus) =>
        AllowedTransitions.TryGetValue(currentStatus, out var allowed) &&
        allowed.Contains(newStatus);

    public static void ValidateTransition(string currentStatus, string newStatus)
    {
        if (!QuotationStatusNames.IsValid(newStatus))
            throw new BadRequestException($"Invalid quotation status '{newStatus}'.");

        if (string.Equals(currentStatus, newStatus, StringComparison.OrdinalIgnoreCase))
            return;

        if (!CanTransition(currentStatus, newStatus))
        {
            throw new BadRequestException(
                $"Cannot transition quotation from '{currentStatus}' to '{newStatus}'.");
        }
    }

    public static bool IsEditable(string status) =>
        string.Equals(status, QuotationStatusNames.Draft, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, QuotationStatusNames.Sent, StringComparison.OrdinalIgnoreCase);

    public static bool CanDelete(string status) =>
        string.Equals(status, QuotationStatusNames.Draft, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, QuotationStatusNames.Rejected, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, QuotationStatusNames.Expired, StringComparison.OrdinalIgnoreCase);

    public static bool CanConvertToOrder(string status) =>
        string.Equals(status, QuotationStatusNames.Accepted, StringComparison.OrdinalIgnoreCase);
}
