using FluentValidation;

namespace BusinessOS.Application.Features.Expenses.Commands.DeleteExpense;

public sealed class DeleteExpenseCommandValidator : AbstractValidator<DeleteExpenseCommand>
{
    public DeleteExpenseCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Expense id is required.");
    }
}
