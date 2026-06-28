using FluentValidation;

namespace BusinessOS.Application.Features.Expenses.Commands.DeleteExpenseCategory;

public sealed class DeleteExpenseCategoryCommandValidator : AbstractValidator<DeleteExpenseCategoryCommand>
{
    public DeleteExpenseCategoryCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Expense category id is required.");
    }
}
