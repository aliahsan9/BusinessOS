using System.Linq.Expressions;
using BusinessOS.Domain.Entities;

namespace BusinessOS.Application.Features.Customers.Queries;

public static class CustomerProjections
{
    public static readonly Expression<Func<Customer, CustomerResponse>> ToResponse = x => new CustomerResponse
    {
        Id = x.Id,
        FirstName = x.FirstName,
        LastName = x.LastName,
        FullName = x.FirstName + " " + x.LastName,
        Email = x.Email,
        PhoneNumber = x.PhoneNumber,
        Address = x.Address,
        City = x.City,
        Country = x.Country,
        PostalCode = x.PostalCode,
        IsActive = x.IsActive,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt
    };

    public static readonly Expression<Func<Customer, CustomerSummaryResponse>> ToSummary = x => new CustomerSummaryResponse
    {
        Id = x.Id,
        FullName = x.FirstName + " " + x.LastName,
        Email = x.Email,
        PhoneNumber = x.PhoneNumber,
        City = x.City,
        Country = x.Country,
        IsActive = x.IsActive,
        CreatedAt = x.CreatedAt
    };

    public static readonly Expression<Func<Order, CustomerOrderResponse>> ToOrderResponse = x => new CustomerOrderResponse
    {
        Id = x.Id,
        OrderNumber = x.OrderNumber,
        OrderDate = x.OrderDate,
        Status = x.Status,
        GrandTotal = x.GrandTotal,
        CreatedAt = x.CreatedAt
    };
}
