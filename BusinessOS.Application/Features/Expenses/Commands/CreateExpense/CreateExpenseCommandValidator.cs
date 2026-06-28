using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.Features.Expenses.Commands.CreateExpense;

public sealed class CreateExpenseCommandValidator : AbstractValidator<CreateExpenseCommand>
{
    public CreateExpenseCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.ExpenseDate).NotEmpty();
        RuleFor(x => x.ExpenseCategoryId).NotEmpty();
        RuleFor(x => x.PaymentMethod).NotEmpty().Must(PaymentMethodNames.IsValid);
        RuleFor(x => x.Status).NotEmpty().Must(ExpenseStatusNames.IsValid);
        RuleFor(x => x.ReferenceNumber).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.ReceiptUrl).MaximumLength(500);
    }
}
