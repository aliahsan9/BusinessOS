using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.Features.PurchaseOrders.Commands.UpdatePurchaseOrderStatus;

public sealed class UpdatePurchaseOrderStatusCommandValidator : AbstractValidator<UpdatePurchaseOrderStatusCommand>
{
    public UpdatePurchaseOrderStatusCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(PurchaseOrderStatusNames.IsValid)
            .WithMessage("Status must be a valid purchase order status.");
    }
}
