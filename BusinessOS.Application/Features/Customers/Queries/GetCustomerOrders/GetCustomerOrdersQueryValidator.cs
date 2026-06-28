using BusinessOS.Application.Common.Validators;
using FluentValidation;

namespace BusinessOS.Application.Features.Customers.Queries.GetCustomerOrders;

public sealed class GetCustomerOrdersQueryValidator : AbstractValidator<GetCustomerOrdersQuery>
{
    private static readonly HashSet<string> AllowedSortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "ordernumber", "orderdate", "status", "grandtotal", "createdat"
        };

    public GetCustomerOrdersQueryValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("Customer id is required.");

        PaginationValidator.ValidPage(RuleFor(x => x.Page));
        PaginationValidator.ValidPageSize(RuleFor(x => x.PageSize));

        RuleFor(x => x.SortBy)
            .Must(sortBy => sortBy is null || AllowedSortFields.Contains(sortBy))
            .WithMessage("SortBy must be one of: orderNumber, orderDate, status, grandTotal, createdAt.");
    }
}
