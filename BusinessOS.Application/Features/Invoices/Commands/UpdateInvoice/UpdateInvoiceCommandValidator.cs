using FluentValidation;

namespace BusinessOS.Application.Features.Invoices.Commands.UpdateInvoice;

public sealed class UpdateInvoiceCommandValidator : AbstractValidator<UpdateInvoiceCommand>
{
    public UpdateInvoiceCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required.");

        RuleFor(x => x.DueDate)
            .NotEmpty().WithMessage("DueDate is required.");

        RuleFor(x => x.Notes)
            .MaximumLength(2000);
    }
}
