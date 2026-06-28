using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.Features.Quotations.Queries.GetAllQuotations;

public sealed class GetAllQuotationsQueryValidator : AbstractValidator<GetAllQuotationsQuery>
{
    public GetAllQuotationsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(x => x.Status)
            .Must(status => status is null || QuotationStatusNames.IsValid(status))
            .WithMessage("Status must be a valid quotation status.");
    }
}
