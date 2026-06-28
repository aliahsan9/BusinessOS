using BusinessOS.Application.Common.Validators;
using FluentValidation;

namespace BusinessOS.Application.Features.Customers.Queries.GetAllCustomers;

public sealed class GetAllCustomersQueryValidator : AbstractValidator<GetAllCustomersQuery>
{
    private static readonly HashSet<string> AllowedSortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "firstname", "lastname", "email", "city", "country", "createdat", "isactive"
        };

    public GetAllCustomersQueryValidator()
    {
        PaginationValidator.ValidPage(RuleFor(x => x.Page));
        PaginationValidator.ValidPageSize(RuleFor(x => x.PageSize));

        RuleFor(x => x.Search)
            .MaximumLength(200)
            .When(x => x.Search is not null);

        RuleFor(x => x.City)
            .MaximumLength(100)
            .When(x => x.City is not null);

        RuleFor(x => x.Country)
            .MaximumLength(100)
            .When(x => x.Country is not null);

        RuleFor(x => x.SortBy)
            .Must(sortBy => sortBy is null || AllowedSortFields.Contains(sortBy))
            .WithMessage("SortBy must be one of: firstName, lastName, email, city, country, createdAt, isActive.");
    }
}
