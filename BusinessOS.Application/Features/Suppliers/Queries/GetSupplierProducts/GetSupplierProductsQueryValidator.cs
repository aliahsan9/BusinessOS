using FluentValidation;

namespace BusinessOS.Application.Features.Suppliers.Queries.GetSupplierProducts;

public sealed class GetSupplierProductsQueryValidator : AbstractValidator<GetSupplierProductsQuery>
{
    public GetSupplierProductsQueryValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("SupplierId is required.");
    }
}
