using FluentValidation;

namespace BusinessOS.Application.Features.Orders.Commands.DeleteOrder;

public sealed class DeleteOrderCommandValidator : AbstractValidator<DeleteOrderCommand>
{
    public DeleteOrderCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Order id is required.");
    }
}
