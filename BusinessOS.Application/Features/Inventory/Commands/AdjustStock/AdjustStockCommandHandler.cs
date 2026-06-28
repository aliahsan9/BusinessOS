using BusinessOS.Application.Features.Inventory.Constants;
using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Application.Features.Inventory.Services;
using BusinessOS.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BusinessOS.Application.Features.Inventory.Commands.AdjustStock;

public sealed record AdjustStockCommand(
    Guid ProductId,
    decimal Quantity,
    string TransactionType,
    string? ReferenceNumber,
    string? Notes
) : IRequest<Unit>;

public sealed class AdjustStockCommandHandler : IRequestHandler<AdjustStockCommand, Unit>
{
    private readonly IInventoryService _inventoryService;

    public AdjustStockCommandHandler(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public async Task<Unit> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        await _inventoryService.AdjustStockAsync(
            new StockAdjustmentRequest
            {
                ProductId = request.ProductId,
                Quantity = request.Quantity,
                TransactionType = request.TransactionType,
                ReferenceNumber = request.ReferenceNumber,
                Notes = request.Notes
            },
            cancellationToken);

        return Unit.Value;
    }
}

public sealed class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .LessThanOrEqualTo(InventoryConstants.MaxAdjustmentQuantity);
        RuleFor(x => x.TransactionType)
            .NotEmpty()
            .Must(StockTransactionTypeNames.IsValid)
            .WithMessage("Invalid transaction type.");
    }
}
