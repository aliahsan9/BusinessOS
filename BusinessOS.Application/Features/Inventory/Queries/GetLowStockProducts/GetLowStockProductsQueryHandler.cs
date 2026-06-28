using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Application.Features.Inventory.Services;
using MediatR;

namespace BusinessOS.Application.Features.Inventory.Queries.GetLowStockProducts;

public sealed record GetLowStockProductsQuery : IRequest<IReadOnlyList<InventorySummaryResponse>>;

public sealed class GetLowStockProductsQueryHandler
    : IRequestHandler<GetLowStockProductsQuery, IReadOnlyList<InventorySummaryResponse>>
{
    private readonly IInventoryService _inventoryService;

    public GetLowStockProductsQueryHandler(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public async Task<IReadOnlyList<InventorySummaryResponse>> Handle(
        GetLowStockProductsQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _inventoryService.GetLowStockProductsAsync(cancellationToken);
        return items;
    }
}
