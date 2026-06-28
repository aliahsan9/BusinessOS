using MediatR;

namespace BusinessOS.Application.Features.Products.Commands.UpdateProduct;

public sealed record UpdateProductCommand(
    Guid Id,
    Guid CategoryId,
    string Name,
    string SKU,
    string? Description,
    decimal CostPrice,
    decimal SalePrice,
    decimal ReorderLevel,
    bool IsActive) : IRequest<Unit>;
