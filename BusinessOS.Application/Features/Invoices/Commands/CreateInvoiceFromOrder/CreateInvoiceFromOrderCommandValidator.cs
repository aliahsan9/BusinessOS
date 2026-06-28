using FluentValidation;

namespace BusinessOS.Application.Features.Invoices.Commands.CreateInvoiceFromOrder;

public sealed class CreateInvoiceFromOrderCommandValidator : AbstractValidator<CreateInvoiceFromOrderCommand>
{
    public CreateInvoiceFromOrderCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty().WithMessage("OrderId is required.");

        RuleFor(x => x.DueDate)
            .NotEmpty().WithMessage("DueDate is required.");

        RuleFor(x => x.Notes)
            .MaximumLength(2000);
    }
}
