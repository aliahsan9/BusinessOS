using FluentValidation;

namespace BusinessOS.Application.Features.Inventory.Queries.GetInventoryByProductId;

public sealed class GetInventoryByProductIdQueryValidator : AbstractValidator<GetInventoryByProductIdQuery>
{
    public GetInventoryByProductIdQueryValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
    }
}
