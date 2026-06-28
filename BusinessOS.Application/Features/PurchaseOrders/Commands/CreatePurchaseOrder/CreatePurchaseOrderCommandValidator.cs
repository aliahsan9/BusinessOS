using BusinessOS.Application.Features.PurchaseOrders.Constants;
using BusinessOS.Application.Features.PurchaseOrders.Queries;
using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.Features.PurchaseOrders.Commands.CreatePurchaseOrder;

public sealed class CreatePurchaseOrderCommandValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    public CreatePurchaseOrderCommandValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("SupplierId is required.");

        RuleFor(x => x.PurchaseDate)
            .NotEmpty().WithMessage("PurchaseDate is required.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(PurchaseOrderStatusNames.IsValid)
            .WithMessage("Status must be a valid purchase order status.");

        RuleFor(x => x.ReferenceNumber)
            .MaximumLength(100);

        RuleFor(x => x.Notes)
            .MaximumLength(2000);

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one line item is required.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.ProductId)
                .NotEmpty().WithMessage("ProductId is required.");

            item.RuleFor(x => x.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be greater than zero.")
                .LessThanOrEqualTo(PurchaseOrderConstants.MaxItemQuantity)
                .WithMessage($"Quantity cannot exceed {PurchaseOrderConstants.MaxItemQuantity}.");

            item.RuleFor(x => x.UnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage("Unit price cannot be negative.");
        });
    }
}
