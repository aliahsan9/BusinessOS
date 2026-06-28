using System.Linq.Expressions;
using BusinessOS.Domain.Entities;

namespace BusinessOS.Application.Features.Quotations.Queries;

public static class QuotationProjections
{
    public static readonly Expression<Func<Quotation, QuotationSummaryResponse>> ToSummary = x =>
        new QuotationSummaryResponse
        {
            Id = x.Id,
            QuotationNumber = x.QuotationNumber,
            CustomerId = x.CustomerId,
            CustomerName = x.Customer.FirstName + " " + x.Customer.LastName,
            QuotationDate = x.QuotationDate,
            ExpiryDate = x.ExpiryDate,
            Status = x.Status,
            GrandTotal = x.GrandTotal,
            CreatedAt = x.CreatedAt
        };

    public static readonly Expression<Func<Quotation, QuotationResponse>> ToDetail = x =>
        new QuotationResponse
        {
            Id = x.Id,
            QuotationNumber = x.QuotationNumber,
            CustomerId = x.CustomerId,
            CustomerName = x.Customer.FirstName + " " + x.Customer.LastName,
            QuotationDate = x.QuotationDate,
            ExpiryDate = x.ExpiryDate,
            Status = x.Status,
            SubTotal = x.SubTotal,
            Discount = x.Discount,
            Tax = x.Tax,
            GrandTotal = x.GrandTotal,
            Notes = x.Notes,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt,
            Items = x.QuotationItems
                .Select(i => new QuotationLineItemResponse
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.Product != null ? i.Product.Name : string.Empty,
                    ProductSku = i.Product != null ? i.Product.SKU : string.Empty,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Total = i.Total
                })
                .ToList()
        };
}
