using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.Features.Orders.Commands.UpdateOrderStatus;

public sealed class UpdateOrderStatusCommandValidator : AbstractValidator<UpdateOrderStatusCommand>
{
    public UpdateOrderStatusCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Order id is required.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(OrderStatusNames.IsValid)
            .WithMessage("Status must be one of: Pending, Confirmed, Processing, Completed, Cancelled.");
    }
}
