using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Inventory.Constants;
using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Application.Features.Inventory.Services;
using FluentValidation;
using MediatR;

namespace BusinessOS.Application.Features.Inventory.Commands.IncreaseStock;

public sealed record IncreaseStockCommand(
    Guid ProductId,
    decimal Quantity,
    string? ReferenceNumber,
    string? Notes
) : IRequest<Unit>;

public sealed class IncreaseStockCommandHandler : IRequestHandler<IncreaseStockCommand, Unit>
{
    private readonly IInventoryService _inventoryService;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;

    public IncreaseStockCommandHandler(
        IInventoryService inventoryService,
        ICacheService cache,
        ITenantProvider tenantProvider)
    {
        _inventoryService = inventoryService;
        _cache = cache;
        _tenantProvider = tenantProvider;
    }

    public async Task<Unit> Handle(IncreaseStockCommand request, CancellationToken cancellationToken)
    {
        await _inventoryService.IncreaseStockAsync(
            new StockChangeRequest
            {
                ProductId = request.ProductId,
                Quantity = request.Quantity,
                ReferenceNumber = request.ReferenceNumber,
                Notes = request.Notes
            },
            cancellationToken);

        await EntityCacheInvalidator.InvalidateInventoryAsync(
            _cache,
            _tenantProvider.TenantId,
            request.ProductId,
            cancellationToken);

        return Unit.Value;
    }
}

public sealed class IncreaseStockCommandValidator : AbstractValidator<IncreaseStockCommand>
{
    public IncreaseStockCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .LessThanOrEqualTo(InventoryConstants.MaxAdjustmentQuantity);
    }
}
