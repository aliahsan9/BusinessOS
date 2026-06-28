using BusinessOS.Application.Common.Validators;
using BusinessOS.Application.Features.Inventory.Constants;
using FluentValidation;

namespace BusinessOS.Application.Features.Inventory.Queries.GetAllInventory;

public sealed class GetAllInventoryQueryValidator : AbstractValidator<GetAllInventoryQuery>
{
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "currentstock", "reorderlevel", "productname", "productsku"
    };

    public GetAllInventoryQueryValidator()
    {
        PaginationValidator.ValidPage(RuleFor(x => x.Page));
        PaginationValidator.ValidPageSize(RuleFor(x => x.PageSize));

        RuleFor(x => x.SortBy)
            .Must(x => x is null || AllowedSortFields.Contains(x))
            .WithMessage("Invalid sort field.");
    }
}
