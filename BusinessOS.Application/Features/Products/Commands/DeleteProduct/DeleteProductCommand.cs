using MediatR;

namespace BusinessOS.Application.Features.Products.Commands.DeleteProduct;

public sealed record DeleteProductCommand(Guid Id) : IRequest<Unit>;
