using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.Features.PurchaseOrders.Queries.GetAllPurchaseOrders;

public sealed class GetAllPurchaseOrdersQueryValidator : AbstractValidator<GetAllPurchaseOrdersQuery>
{
    public GetAllPurchaseOrdersQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(x => x.Status)
            .Must(status => status is null || PurchaseOrderStatusNames.IsValid(status))
            .WithMessage("Status must be a valid purchase order status.");
    }
}
