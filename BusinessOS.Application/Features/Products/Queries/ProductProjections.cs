using System.Linq.Expressions;
using BusinessOS.Domain.Entities;

namespace BusinessOS.Application.Features.Products.Queries;

public static class ProductProjections
{
    public static readonly Expression<Func<Product, ProductDto>> ToDto = x => new ProductDto
    {
        Id = x.Id,
        CategoryId = x.CategoryId,
        Name = x.Name,
        SKU = x.SKU,
        Description = x.Description,
        CostPrice = x.CostPrice,
        SalePrice = x.SalePrice,
        CurrentStock = x.CurrentStock,
        ReorderLevel = x.ReorderLevel,
        IsActive = x.IsActive
    };
}
