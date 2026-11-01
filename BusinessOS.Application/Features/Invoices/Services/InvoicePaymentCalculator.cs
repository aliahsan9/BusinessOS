using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Invoices.Queries;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Invoices.Services;

public static class InvoicePaymentCalculator
{
    public static decimal Outstanding(decimal grandTotal, decimal amountPaid) =>
        Math.Max(0, Math.Round(grandTotal - amountPaid, 2, MidpointRounding.AwayFromZero));

    public static bool IsCollectibleOverdue(string? status, decimal outstanding, DateTime dueDate, DateTime utcNow) =>
        outstanding > 0
        && dueDate < utcNow
        && !IsStatus(status, InvoiceStatusNames.Paid)
        && !IsStatus(status, InvoiceStatusNames.Cancelled)
        && !IsStatus(status, InvoiceStatusNames.Draft);

    public static bool IsPaidStatusWithOpenBalance(string? status, decimal outstanding) =>
        IsStatus(status, InvoiceStatusNames.Paid) && outstanding > 0;

    public static async Task<Dictionary<Guid, decimal>> GetAmountPaidByOrderIdsAsync(
        IApplicationDbContext context,
        IReadOnlyList<Guid> orderIds,
        CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
            return new Dictionary<Guid, decimal>();

        var rows = await context.Payments
            .AsNoTracking()
            .Where(p => orderIds.Contains(p.OrderId)
                && (p.Status == PaymentStatusNames.Completed || p.Status == PaymentStatusNames.Refunded))
            .GroupBy(p => p.OrderId)
            .Select(g => new { OrderId = g.Key, AmountPaid = g.Sum(p => p.Amount - p.RefundedAmount) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            x => x.OrderId,
            x => Math.Max(0, Math.Round(x.AmountPaid, 2, MidpointRounding.AwayFromZero)));
    }

    /// <summary>
    /// Writes payment-backed amounts onto invoices for the order.
    /// Paid, draft, and cancelled statuses are left as declared. Sent invoices
    /// become Paid, PartiallyPaid, or Overdue from the balance and due date.
    /// </summary>
    public static async Task ReconcileOrdersAsync(
        IApplicationDbContext context,
        IEnumerable<Guid> orderIds,
        CancellationToken cancellationToken)
    {
        var ids = orderIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
            return;

        var paidByOrder = await GetAmountPaidByOrderIdsAsync(context, ids, cancellationToken);
        var invoices = await context.Invoices
            .Where(i => ids.Contains(i.OrderId))
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var invoice in invoices)
        {
            var paid = paidByOrder.TryGetValue(invoice.OrderId, out var amount) ? amount : 0m;
            ApplyStoredBalance(invoice, paid, now);
        }

        if (invoices.Count > 0)
            await context.SaveChangesAsync(cancellationToken);
    }

    public static void ApplyStoredBalance(Invoice invoice, decimal netPaid, DateTime utcNow)
    {
        netPaid = Math.Max(0, Math.Round(netPaid, 2, MidpointRounding.AwayFromZero));
        invoice.AmountPaid = netPaid;
        invoice.OutstandingAmount = Outstanding(invoice.GrandTotal, netPaid);

        if (IsStatus(invoice.Status, InvoiceStatusNames.Draft)
            || IsStatus(invoice.Status, InvoiceStatusNames.Cancelled)
            || IsStatus(invoice.Status, InvoiceStatusNames.Paid))
            return;

        if (invoice.OutstandingAmount <= 0)
        {
            invoice.Status = InvoiceStatusNames.Paid;
            return;
        }

        var pastDue = invoice.DueDate < utcNow;
        if (netPaid > 0)
        {
            invoice.Status = pastDue ? InvoiceStatusNames.Overdue : InvoiceStatusNames.PartiallyPaid;
            return;
        }

        if (pastDue && IsStatus(invoice.Status, InvoiceStatusNames.Sent))
            invoice.Status = InvoiceStatusNames.Overdue;
        else if (!pastDue && IsStatus(invoice.Status, InvoiceStatusNames.Overdue))
            invoice.Status = InvoiceStatusNames.Sent;
    }

    public static void ApplyPaymentAmounts(
        InvoiceSummaryResponse invoice,
        IReadOnlyDictionary<Guid, decimal> amountPaidByOrderId)
    {
        var amountPaid = amountPaidByOrderId.TryGetValue(invoice.OrderId, out var paid)
            ? Math.Round(paid, 2, MidpointRounding.AwayFromZero)
            : 0;

        invoice.AmountPaid = amountPaid;
        invoice.OutstandingAmount = Outstanding(invoice.GrandTotal, amountPaid);
    }

    public static void ApplyPaymentAmounts(
        InvoiceResponse invoice,
        IReadOnlyDictionary<Guid, decimal> amountPaidByOrderId)
    {
        var amountPaid = amountPaidByOrderId.TryGetValue(invoice.OrderId, out var paid)
            ? Math.Round(paid, 2, MidpointRounding.AwayFromZero)
            : 0;

        invoice.AmountPaid = amountPaid;
        invoice.OutstandingAmount = Outstanding(invoice.GrandTotal, amountPaid);
    }

    private static bool IsStatus(string? status, string expected) =>
        string.Equals(status, expected, StringComparison.OrdinalIgnoreCase);
}
