using BusinessOS.Application.Common.Validators;
using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.Features.Expenses.Queries.GetAllExpenses;

public sealed class GetAllExpensesQueryValidator : AbstractValidator<GetAllExpensesQuery>
{
    private static readonly HashSet<string> AllowedSortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "title", "amount", "expensedate", "categoryname", "paymentmethod",
            "vendor", "status", "isrecurring", "createdat"
        };

    public GetAllExpensesQueryValidator()
    {
        PaginationValidator.ValidPage(RuleFor(x => x.Page));
        PaginationValidator.ValidPageSize(RuleFor(x => x.PageSize));

        RuleFor(x => x.Search)
            .MaximumLength(200)
            .When(x => x.Search is not null);

        RuleFor(x => x.Status)
            .Must(status => status is null || ExpenseStatusNames.IsValid(status))
            .WithMessage("Status must be a valid expense status.");

        RuleFor(x => x)
            .Must(x => x.DateFrom is null || x.DateTo is null || x.DateFrom <= x.DateTo)
            .WithMessage("DateFrom must be before or equal to DateTo.");

        RuleFor(x => x.SortBy)
            .Must(sortBy => sortBy is null || AllowedSortFields.Contains(sortBy))
            .WithMessage("SortBy must be one of: title, amount, expenseDate, categoryName, paymentMethod, vendor, status, isRecurring, createdAt.");
    }
}
