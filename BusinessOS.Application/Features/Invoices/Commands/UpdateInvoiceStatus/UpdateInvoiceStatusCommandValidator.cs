using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.Features.Invoices.Commands.UpdateInvoiceStatus;

public sealed class UpdateInvoiceStatusCommandValidator : AbstractValidator<UpdateInvoiceStatusCommand>
{
    public UpdateInvoiceStatusCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(InvoiceStatusNames.IsValid)
            .WithMessage("Status must be a valid invoice status.");
    }
}
