using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.Features.Payments.Commands.UpdatePayment;

public sealed class UpdatePaymentCommandValidator : AbstractValidator<UpdatePaymentCommand>
{
    public UpdatePaymentCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required.");

        RuleFor(x => x.OrderId)
            .NotEmpty().WithMessage("OrderId is required.");

        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("CustomerId is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.");

        RuleFor(x => x.PaymentMethod)
            .NotEmpty().WithMessage("PaymentMethod is required.")
            .Must(PaymentMethodNames.IsValid)
            .WithMessage("PaymentMethod must be a valid payment method.");

        RuleFor(x => x.PaymentDate)
            .NotEmpty().WithMessage("PaymentDate is required.");

        RuleFor(x => x.ReferenceNo)
            .MaximumLength(100);
    }
}
