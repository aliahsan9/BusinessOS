using System.Linq.Expressions;
using BusinessOS.Domain.Entities;

namespace BusinessOS.Application.Features.Suppliers.Queries;

public static class SupplierProjections
{
    public static readonly Expression<Func<Supplier, SupplierResponse>> ToResponse = x => new SupplierResponse
    {
        Id = x.Id,
        Name = x.Name,
        Email = x.Email,
        Phone = x.Phone,
        Address = x.Address,
        ContactPerson = x.ContactPerson,
        Notes = x.Notes,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt
    };

    public static readonly Expression<Func<Supplier, SupplierSummaryResponse>> ToSummary = x => new SupplierSummaryResponse
    {
        Id = x.Id,
        Name = x.Name,
        Email = x.Email,
        Phone = x.Phone,
        Address = x.Address,
        ContactPerson = x.ContactPerson,
        CreatedAt = x.CreatedAt
    };

    public static readonly Expression<Func<Purchase, SupplierPurchaseSummaryResponse>> ToPurchaseSummary = x =>
        new SupplierPurchaseSummaryResponse
        {
            Id = x.Id,
            PurchaseDate = x.PurchaseDate,
            TotalAmount = x.TotalAmount,
            Status = x.Status,
            ItemCount = x.PurchaseItems.Count
        };
}
