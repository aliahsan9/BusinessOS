using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.Features.Quotations.Commands.UpdateQuotationStatus;

public sealed class UpdateQuotationStatusCommandValidator : AbstractValidator<UpdateQuotationStatusCommand>
{
    public UpdateQuotationStatusCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(QuotationStatusNames.IsValid)
            .WithMessage("Status must be a valid quotation status.");
    }
}
