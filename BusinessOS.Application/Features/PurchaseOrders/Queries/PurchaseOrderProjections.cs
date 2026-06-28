using System.Linq.Expressions;
using BusinessOS.Domain.Entities;

namespace BusinessOS.Application.Features.PurchaseOrders.Queries;

public static class PurchaseOrderProjections
{
    public static readonly Expression<Func<Purchase, PurchaseOrderSummaryResponse>> ToSummary = x =>
        new PurchaseOrderSummaryResponse
        {
            Id = x.Id,
            SupplierId = x.SupplierId,
            SupplierName = x.Supplier.Name,
            PurchaseDate = x.PurchaseDate,
            TotalAmount = x.TotalAmount,
            Status = x.Status,
            ReferenceNumber = x.ReferenceNumber,
            CreatedAt = x.CreatedAt
        };

    public static readonly Expression<Func<Purchase, PurchaseOrderResponse>> ToDetail = x => new PurchaseOrderResponse
    {
        Id = x.Id,
        SupplierId = x.SupplierId,
        SupplierName = x.Supplier.Name,
        PurchaseDate = x.PurchaseDate,
        TotalAmount = x.TotalAmount,
        Status = x.Status,
        ReferenceNumber = x.ReferenceNumber,
        Notes = x.Notes,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt,
        Items = x.PurchaseItems
            .Select(i => new PurchaseOrderLineItemResponse
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
