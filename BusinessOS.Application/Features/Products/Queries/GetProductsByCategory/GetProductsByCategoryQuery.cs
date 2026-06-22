using MediatR;
using BusinessOS.Application.Features.Products.Queries;

namespace BusinessOS.Application.Features.Products.Queries.GetProductsByCategory;

public record GetProductsByCategoryQuery(Guid CategoryId)
    : IRequest<List<ProductDto>>;
