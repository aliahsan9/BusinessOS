using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.Features.Expenses.Commands.UpdateExpense;

public sealed class UpdateExpenseCommandValidator : AbstractValidator<UpdateExpenseCommand>
{
    public UpdateExpenseCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.ExpenseDate).NotEmpty();
        RuleFor(x => x.ExpenseCategoryId).NotEmpty();
        RuleFor(x => x.PaymentMethod).NotEmpty().Must(PaymentMethodNames.IsValid);
        RuleFor(x => x.Status).NotEmpty().Must(ExpenseStatusNames.IsValid);
    }
}
