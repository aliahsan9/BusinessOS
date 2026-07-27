using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Interfaces;
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
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;

    public AdjustStockCommandHandler(
        IInventoryService inventoryService,
        ICacheService cache,
        ITenantProvider tenantProvider)
    {
        _inventoryService = inventoryService;
        _cache = cache;
        _tenantProvider = tenantProvider;
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

        await EntityCacheInvalidator.InvalidateInventoryAsync(
            _cache,
            _tenantProvider.TenantId,
            request.ProductId,
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
