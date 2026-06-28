using FluentValidation;

namespace BusinessOS.Application.Features.Quotations.Queries.GetQuotationById;

public sealed class GetQuotationByIdQueryValidator : AbstractValidator<GetQuotationByIdQuery>
{
    public GetQuotationByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required.");
    }
}
