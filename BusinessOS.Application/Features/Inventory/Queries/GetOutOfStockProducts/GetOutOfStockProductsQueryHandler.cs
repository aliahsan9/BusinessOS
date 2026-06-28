using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Application.Features.Inventory.Services;
using MediatR;

namespace BusinessOS.Application.Features.Inventory.Queries.GetOutOfStockProducts;

public sealed record GetOutOfStockProductsQuery : IRequest<IReadOnlyList<InventorySummaryResponse>>;

public sealed class GetOutOfStockProductsQueryHandler
    : IRequestHandler<GetOutOfStockProductsQuery, IReadOnlyList<InventorySummaryResponse>>
{
    private readonly IInventoryService _inventoryService;

    public GetOutOfStockProductsQueryHandler(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public async Task<IReadOnlyList<InventorySummaryResponse>> Handle(
        GetOutOfStockProductsQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _inventoryService.GetOutOfStockProductsAsync(cancellationToken);
        return items;
    }
}
