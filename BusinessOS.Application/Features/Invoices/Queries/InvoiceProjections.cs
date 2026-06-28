using System.Linq.Expressions;
using BusinessOS.Domain.Entities;

namespace BusinessOS.Application.Features.Invoices.Queries;

public static class InvoiceProjections
{
    public static readonly Expression<Func<Invoice, InvoiceSummaryResponse>> ToSummary = x =>
        new InvoiceSummaryResponse
        {
            Id = x.Id,
            InvoiceNumber = x.InvoiceNumber,
            OrderId = x.OrderId,
            OrderNumber = x.Order.OrderNumber,
            CustomerId = x.CustomerId,
            CustomerName = x.Customer.FirstName + " " + x.Customer.LastName,
            InvoiceDate = x.InvoiceDate,
            DueDate = x.DueDate,
            Status = x.Status,
            GrandTotal = x.GrandTotal,
            AmountPaid = x.AmountPaid,
            OutstandingAmount = x.OutstandingAmount,
            CreatedAt = x.CreatedAt
        };

    public static readonly Expression<Func<Invoice, InvoiceResponse>> ToDetail = x =>
        new InvoiceResponse
        {
            Id = x.Id,
            InvoiceNumber = x.InvoiceNumber,
            OrderId = x.OrderId,
            OrderNumber = x.Order.OrderNumber,
            CustomerId = x.CustomerId,
            CustomerName = x.Customer.FirstName + " " + x.Customer.LastName,
            InvoiceDate = x.InvoiceDate,
            DueDate = x.DueDate,
            Status = x.Status,
            SubTotal = x.SubTotal,
            Discount = x.Discount,
            Tax = x.Tax,
            GrandTotal = x.GrandTotal,
            AmountPaid = x.AmountPaid,
            OutstandingAmount = x.OutstandingAmount,
            Notes = x.Notes,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        };
}
