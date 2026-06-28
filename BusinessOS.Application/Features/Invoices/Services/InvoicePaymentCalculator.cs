using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Invoices.Queries;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Invoices.Services;

public static class InvoicePaymentCalculator
{
    public static async Task<Dictionary<Guid, decimal>> GetAmountPaidByOrderIdsAsync(
        IApplicationDbContext context,
        IReadOnlyList<Guid> orderIds,
        CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
            return new Dictionary<Guid, decimal>();

        return await context.Payments
            .AsNoTracking()
            .Where(p => orderIds.Contains(p.OrderId))
            .GroupBy(p => p.OrderId)
            .Select(g => new { OrderId = g.Key, AmountPaid = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(x => x.OrderId, x => x.AmountPaid, cancellationToken);
    }

    public static void ApplyPaymentAmounts(
        InvoiceSummaryResponse invoice,
        IReadOnlyDictionary<Guid, decimal> amountPaidByOrderId)
    {
        var amountPaid = amountPaidByOrderId.TryGetValue(invoice.OrderId, out var paid)
            ? Math.Round(paid, 2)
            : 0;

        invoice.AmountPaid = amountPaid;
        invoice.OutstandingAmount = Math.Round(invoice.GrandTotal - amountPaid, 2);
    }

    public static void ApplyPaymentAmounts(
        InvoiceResponse invoice,
        IReadOnlyDictionary<Guid, decimal> amountPaidByOrderId)
    {
        var amountPaid = amountPaidByOrderId.TryGetValue(invoice.OrderId, out var paid)
            ? Math.Round(paid, 2)
            : 0;

        invoice.AmountPaid = amountPaid;
        invoice.OutstandingAmount = Math.Round(invoice.GrandTotal - amountPaid, 2);
    }
}
