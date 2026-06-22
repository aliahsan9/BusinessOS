using MediatR;

namespace BusinessOS.Application.Features.Products.Commands.CreateProduct;

public record CreateProductCommand(
    Guid CategoryId,
    string Name,
    string SKU,
    string? Description,
    decimal CostPrice,
    decimal SalePrice,
    int ReorderLevel
) : IRequest<Guid>;
