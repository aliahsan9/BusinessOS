using BusinessOS.Application.Features.Inventory.Constants;
using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Application.Features.Inventory.Services;
using FluentValidation;
using MediatR;

namespace BusinessOS.Application.Features.Inventory.Commands.DecreaseStock;

public sealed record DecreaseStockCommand(
    Guid ProductId,
    decimal Quantity,
    string? ReferenceNumber,
    string? Notes
) : IRequest<Unit>;

public sealed class DecreaseStockCommandHandler : IRequestHandler<DecreaseStockCommand, Unit>
{
    private readonly IInventoryService _inventoryService;

    public DecreaseStockCommandHandler(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public async Task<Unit> Handle(DecreaseStockCommand request, CancellationToken cancellationToken)
    {
        await _inventoryService.DecreaseStockAsync(
            new StockChangeRequest
            {
                ProductId = request.ProductId,
                Quantity = request.Quantity,
                ReferenceNumber = request.ReferenceNumber,
                Notes = request.Notes
            },
            cancellationToken);

        return Unit.Value;
    }
}

public sealed class DecreaseStockCommandValidator : AbstractValidator<DecreaseStockCommand>
{
    public DecreaseStockCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .LessThanOrEqualTo(InventoryConstants.MaxAdjustmentQuantity);
    }
}
