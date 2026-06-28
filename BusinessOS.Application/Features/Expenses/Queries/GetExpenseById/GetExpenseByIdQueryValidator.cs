using FluentValidation;

namespace BusinessOS.Application.Features.Expenses.Queries.GetExpenseById;

public sealed class GetExpenseByIdQueryValidator : AbstractValidator<GetExpenseByIdQuery>
{
    public GetExpenseByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Expense id is required.");
    }
}
