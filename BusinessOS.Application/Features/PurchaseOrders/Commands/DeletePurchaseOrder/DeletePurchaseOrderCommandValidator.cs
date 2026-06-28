using FluentValidation;

namespace BusinessOS.Application.Features.PurchaseOrders.Commands.DeletePurchaseOrder;

public sealed class DeletePurchaseOrderCommandValidator : AbstractValidator<DeletePurchaseOrderCommand>
{
    public DeletePurchaseOrderCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required.");
    }
}
