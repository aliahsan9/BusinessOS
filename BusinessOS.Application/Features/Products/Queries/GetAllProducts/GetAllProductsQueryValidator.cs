using BusinessOS.Application.Common.Validators;
using FluentValidation;

namespace BusinessOS.Application.Features.Products.Queries.GetAllProducts;

public sealed class GetAllProductsQueryValidator : AbstractValidator<GetAllProductsQuery>
{
    private static readonly HashSet<string> AllowedSortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "name", "sku", "saleprice", "costprice", "currentstock", "isactive"
        };

    public GetAllProductsQueryValidator()
    {
        PaginationValidator.ValidPage(RuleFor(x => x.Page));
        PaginationValidator.ValidPageSize(RuleFor(x => x.PageSize));

        RuleFor(x => x.Search)
            .MaximumLength(200)
            .When(x => x.Search is not null);

        RuleFor(x => x.SortBy)
            .Must(sortBy => sortBy is null || AllowedSortFields.Contains(sortBy))
            .WithMessage("SortBy must be one of: name, sku, salePrice, costPrice, currentStock, isActive.");
    }
}
