using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Application.Features.Inventory.Services;
using MediatR;

namespace BusinessOS.Application.Features.Inventory.Queries.GetInventoryByProductId;

public sealed record GetInventoryByProductIdQuery(Guid ProductId) : IRequest<InventoryResponse>;

public sealed class GetInventoryByProductIdQueryHandler
    : IRequestHandler<GetInventoryByProductIdQuery, InventoryResponse>
{
    private readonly IInventoryService _inventoryService;

    public GetInventoryByProductIdQueryHandler(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public Task<InventoryResponse> Handle(
        GetInventoryByProductIdQuery request,
        CancellationToken cancellationToken) =>
        _inventoryService.GetByProductIdAsync(request.ProductId, cancellationToken);
}
