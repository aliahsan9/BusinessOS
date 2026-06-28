using BusinessOS.Application.Common.Validators;
using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.Features.Orders.Queries.GetAllOrders;

public sealed class GetAllOrdersQueryValidator : AbstractValidator<GetAllOrdersQuery>
{
    private static readonly HashSet<string> AllowedSortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "ordernumber", "orderdate", "createdat", "status", "grandtotal", "customername"
        };

    public GetAllOrdersQueryValidator()
    {
        PaginationValidator.ValidPage(RuleFor(x => x.Page));
        PaginationValidator.ValidPageSize(RuleFor(x => x.PageSize));

        RuleFor(x => x.Search)
            .MaximumLength(200)
            .When(x => x.Search is not null);

        RuleFor(x => x.Status)
            .Must(status => status is null || OrderStatusNames.IsValid(status))
            .WithMessage("Status must be one of: Pending, Confirmed, Processing, Completed, Cancelled.")
            .When(x => x.Status is not null);

        RuleFor(x => x.SortBy)
            .Must(sortBy => sortBy is null || AllowedSortFields.Contains(sortBy))
            .WithMessage("SortBy must be one of: orderNumber, orderDate, createdAt, status, grandTotal, customerName.");
    }
}
