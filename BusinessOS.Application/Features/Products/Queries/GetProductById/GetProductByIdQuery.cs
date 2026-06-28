using BusinessOS.Application.Features.Products.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Products.Queries.GetProductById;

public sealed record GetProductByIdQuery(Guid Id) : IRequest<ProductDto>;
