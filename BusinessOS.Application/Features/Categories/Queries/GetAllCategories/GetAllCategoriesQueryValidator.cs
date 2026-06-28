using BusinessOS.Application.Common.Validators;
using FluentValidation;

namespace BusinessOS.Application.Features.Categories.Queries.GetAllCategories;

public sealed class GetAllCategoriesQueryValidator : AbstractValidator<GetAllCategoriesQuery>
{
    private static readonly HashSet<string> AllowedSortFields =
        new(StringComparer.OrdinalIgnoreCase) { "name", "description" };

    public GetAllCategoriesQueryValidator()
    {
        PaginationValidator.ValidPage(RuleFor(x => x.Page));
        PaginationValidator.ValidPageSize(RuleFor(x => x.PageSize));

        RuleFor(x => x.Search)
            .MaximumLength(200)
            .When(x => x.Search is not null);

        RuleFor(x => x.SortBy)
            .Must(sortBy => sortBy is null || AllowedSortFields.Contains(sortBy))
            .WithMessage("SortBy must be one of: name, description.");
    }
}
