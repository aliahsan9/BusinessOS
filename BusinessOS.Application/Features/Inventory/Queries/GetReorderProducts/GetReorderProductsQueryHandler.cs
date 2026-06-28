using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Application.Features.Inventory.Services;
using MediatR;

namespace BusinessOS.Application.Features.Inventory.Queries.GetReorderProducts;

public sealed record GetReorderProductsQuery : IRequest<IReadOnlyList<InventorySummaryResponse>>;

public sealed class GetReorderProductsQueryHandler
    : IRequestHandler<GetReorderProductsQuery, IReadOnlyList<InventorySummaryResponse>>
{
    private readonly IInventoryService _inventoryService;

    public GetReorderProductsQueryHandler(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public async Task<IReadOnlyList<InventorySummaryResponse>> Handle(
        GetReorderProductsQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _inventoryService.GetReorderProductsAsync(cancellationToken);
        return items;
    }
}
