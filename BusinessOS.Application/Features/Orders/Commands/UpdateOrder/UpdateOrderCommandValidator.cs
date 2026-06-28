using BusinessOS.Application.Features.Orders.Constants;
using BusinessOS.Application.Features.Orders.Queries;
using FluentValidation;

namespace BusinessOS.Application.Features.Orders.Commands.UpdateOrder;

public sealed class UpdateOrderCommandValidator : AbstractValidator<UpdateOrderCommand>
{
    public UpdateOrderCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Order id is required.");

        RuleFor(x => x.Discount)
            .GreaterThanOrEqualTo(0).WithMessage("Discount cannot be negative.");

        RuleFor(x => x.Tax)
            .GreaterThanOrEqualTo(0).WithMessage("Tax cannot be negative.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one order item is required.");

        RuleForEach(x => x.Items).SetValidator(new CreateOrderItemDtoValidator());
    }

    private sealed class CreateOrderItemDtoValidator : AbstractValidator<CreateOrderItemDto>
    {
        public CreateOrderItemDtoValidator()
        {
            RuleFor(x => x.ProductId)
                .NotEmpty().WithMessage("ProductId is required.");

            RuleFor(x => x.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be greater than zero.")
                .LessThanOrEqualTo(OrderConstants.MaxItemQuantity)
                .WithMessage($"Quantity cannot exceed {OrderConstants.MaxItemQuantity}.");
        }
    }
}
