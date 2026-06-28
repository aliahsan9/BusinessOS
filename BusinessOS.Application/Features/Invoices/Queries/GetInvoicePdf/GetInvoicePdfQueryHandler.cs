using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Invoices.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace BusinessOS.Application.Features.Invoices.Queries.GetInvoicePdf;

public sealed class GetInvoicePdfQueryHandler : IRequestHandler<GetInvoicePdfQuery, string>
{
    private readonly IApplicationDbContext _context;

    public GetInvoicePdfQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> Handle(GetInvoicePdfQuery request, CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
            .Select(x => new
            {
                x.InvoiceNumber,
                x.InvoiceDate,
                x.DueDate,
                x.Status,
                x.SubTotal,
                x.Discount,
                x.Tax,
                x.GrandTotal,
                x.OrderId,
                x.Notes,
                CustomerName = x.Customer.FirstName + " " + x.Customer.LastName,
                OrderNumber = x.Order.OrderNumber
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (invoice is null)
            throw new NotFoundException("Invoice not found.");

        var amountPaidByOrderId = await InvoicePaymentCalculator.GetAmountPaidByOrderIdsAsync(
            _context,
            [invoice.OrderId],
            cancellationToken);

        var amountPaid = amountPaidByOrderId.TryGetValue(invoice.OrderId, out var paid)
            ? Math.Round(paid, 2)
            : 0;
        var outstanding = Math.Round(invoice.GrandTotal - amountPaid, 2);

        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html><head><title>Invoice</title></head><body>");
        html.AppendLine($"<h1>Invoice {invoice.InvoiceNumber}</h1>");
        html.AppendLine($"<p><strong>Order:</strong> {invoice.OrderNumber}</p>");
        html.AppendLine($"<p><strong>Customer:</strong> {invoice.CustomerName}</p>");
        html.AppendLine($"<p><strong>Invoice Date:</strong> {invoice.InvoiceDate:yyyy-MM-dd}</p>");
        html.AppendLine($"<p><strong>Due Date:</strong> {invoice.DueDate:yyyy-MM-dd}</p>");
        html.AppendLine($"<p><strong>Status:</strong> {invoice.Status}</p>");
        html.AppendLine("<hr/>");
        html.AppendLine($"<p>Sub Total: {invoice.SubTotal:C}</p>");
        html.AppendLine($"<p>Discount: {invoice.Discount:C}</p>");
        html.AppendLine($"<p>Tax: {invoice.Tax:C}</p>");
        html.AppendLine($"<p><strong>Grand Total: {invoice.GrandTotal:C}</strong></p>");
        html.AppendLine($"<p>Amount Paid: {amountPaid:C}</p>");
        html.AppendLine($"<p>Outstanding: {outstanding:C}</p>");

        if (!string.IsNullOrWhiteSpace(invoice.Notes))
            html.AppendLine($"<p><strong>Notes:</strong> {invoice.Notes}</p>");

        html.AppendLine("</body></html>");

        return html.ToString();
    }
}
