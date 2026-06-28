using FluentValidation;

namespace BusinessOS.Application.Features.Quotations.Commands.DeleteQuotation;

public sealed class DeleteQuotationCommandValidator : AbstractValidator<DeleteQuotationCommand>
{
    public DeleteQuotationCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required.");
    }
}
