using FluentValidation;

namespace BusinessOS.Application.Features.Products.Commands.UpdateProduct;

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("CategoryId is required");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required")
            .MaximumLength(200);

        RuleFor(x => x.SKU)
            .NotEmpty().WithMessage("SKU is required")
            .MaximumLength(50);

        RuleFor(x => x.CostPrice)
            .GreaterThan(0);

        RuleFor(x => x.SalePrice)
            .GreaterThan(0);

        RuleFor(x => x.Description)
            .MaximumLength(2000).When(x => x.Description is not null);

        RuleFor(x => x.ReorderLevel)
            .GreaterThanOrEqualTo(0);
    }
}
