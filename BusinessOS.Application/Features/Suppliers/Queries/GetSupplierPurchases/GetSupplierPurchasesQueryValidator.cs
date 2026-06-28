using FluentValidation;

namespace BusinessOS.Application.Features.Suppliers.Queries.GetSupplierPurchases;

public sealed class GetSupplierPurchasesQueryValidator : AbstractValidator<GetSupplierPurchasesQuery>
{
    public GetSupplierPurchasesQueryValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("SupplierId is required.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100);
    }
}
