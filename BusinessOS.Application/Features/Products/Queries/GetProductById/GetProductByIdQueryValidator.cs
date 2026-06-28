using BusinessOS.Application.Common.Validators;
using FluentValidation;

namespace BusinessOS.Application.Features.Products.Queries.GetProductById;

public sealed class GetProductByIdQueryValidator : AbstractValidator<GetProductByIdQuery>
{
    public GetProductByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Product id is required.");
    }
}
