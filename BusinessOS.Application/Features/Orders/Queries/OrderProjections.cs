using System.Linq.Expressions;
using BusinessOS.Domain.Entities;

namespace BusinessOS.Application.Features.Orders.Queries;

public static class OrderProjections
{
    public static readonly Expression<Func<Order, OrderSummaryDto>> ToSummaryDto = x => new OrderSummaryDto
    {
        Id = x.Id,
        OrderNumber = x.OrderNumber,
        OrderDate = x.OrderDate,
        CreatedAt = x.CreatedAt,
        Status = x.Status,
        CustomerName = x.Customer.Name,
        CustomerEmail = x.Customer.Email,
        GrandTotal = x.GrandTotal
    };

    public static readonly Expression<Func<Order, OrderDto>> ToDetailDto = x => new OrderDto
    {
        Id = x.Id,
        CustomerId = x.CustomerId,
        OrderNumber = x.OrderNumber,
        OrderDate = x.OrderDate,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt,
        Status = x.Status,
        CustomerName = x.Customer.Name,
        CustomerEmail = x.Customer.Email,
        CustomerPhone = x.Customer.Phone,
        CustomerAddress = x.Customer.Address,
        TotalAmount = x.TotalAmount,
        Discount = x.Discount,
        Tax = x.Tax,
        GrandTotal = x.GrandTotal,
        Items = x.OrderItems
            .Select(i => new OrderItemDto
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
