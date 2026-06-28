using FluentValidation;

namespace BusinessOS.Application.Features.Quotations.Commands.ConvertQuotationToOrder;

public sealed class ConvertQuotationToOrderCommandValidator : AbstractValidator<ConvertQuotationToOrderCommand>
{
    public ConvertQuotationToOrderCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required.");
    }
}
