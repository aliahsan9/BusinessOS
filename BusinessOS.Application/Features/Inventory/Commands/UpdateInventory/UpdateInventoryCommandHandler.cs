using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Application.Features.Inventory.Services;
using FluentValidation;
using MediatR;

namespace BusinessOS.Application.Features.Inventory.Commands.UpdateInventory;

public sealed record UpdateInventoryCommand(
    Guid ProductId,
    decimal MinimumStockLevel,
    decimal MaximumStockLevel,
    decimal ReorderLevel
) : IRequest<Unit>;

public sealed class UpdateInventoryCommandHandler : IRequestHandler<UpdateInventoryCommand, Unit>
{
    private readonly IInventoryService _inventoryService;

    public UpdateInventoryCommandHandler(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public async Task<Unit> Handle(UpdateInventoryCommand request, CancellationToken cancellationToken)
    {
        await _inventoryService.UpdateStockLevelsAsync(
            request.ProductId,
            new UpdateStockRequest
            {
                MinimumStockLevel = request.MinimumStockLevel,
                MaximumStockLevel = request.MaximumStockLevel,
                ReorderLevel = request.ReorderLevel
            },
            cancellationToken);

        return Unit.Value;
    }
}

public sealed class UpdateInventoryCommandValidator : AbstractValidator<UpdateInventoryCommand>
{
    public UpdateInventoryCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.MinimumStockLevel).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaximumStockLevel)
            .GreaterThanOrEqualTo(x => x.MinimumStockLevel)
            .WithMessage("MaximumStockLevel must be greater than or equal to MinimumStockLevel.");
    }
}
