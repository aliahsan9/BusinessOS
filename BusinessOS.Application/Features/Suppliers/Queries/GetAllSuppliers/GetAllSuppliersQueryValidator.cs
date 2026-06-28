using FluentValidation;

namespace BusinessOS.Application.Features.Suppliers.Queries.GetAllSuppliers;

public sealed class GetAllSuppliersQueryValidator : AbstractValidator<GetAllSuppliersQuery>
{
    public GetAllSuppliersQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100);
    }
}
