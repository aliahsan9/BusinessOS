using BusinessOS.Application.Features.Expenses.Queries;
using BusinessOS.Domain.Entities;

namespace BusinessOS.Application.Features.Expenses.Queries;

internal static class ExpenseProjections
{
    public static readonly System.Linq.Expressions.Expression<Func<Expense, ExpenseSummaryResponse>> ToSummary =
        x => new ExpenseSummaryResponse
        {
            Id = x.Id,
            Title = x.Title,
            Amount = x.Amount,
            ExpenseDate = x.ExpenseDate,
            CategoryName = x.ExpenseCategory.Name,
            PaymentMethod = x.PaymentMethod,
            Vendor = x.Vendor,
            Status = x.Status,
            IsRecurring = x.IsRecurring,
            CreatedAt = x.CreatedAt
        };

    public static readonly System.Linq.Expressions.Expression<Func<Expense, ExpenseResponse>> ToDetail =
        x => new ExpenseResponse
        {
            Id = x.Id,
            Title = x.Title,
            Amount = x.Amount,
            ExpenseDate = x.ExpenseDate,
            ExpenseCategoryId = x.ExpenseCategoryId,
            CategoryName = x.ExpenseCategory.Name,
            PaymentMethod = x.PaymentMethod,
            Vendor = x.Vendor,
            ReferenceNumber = x.ReferenceNumber,
            Description = x.Description,
            ReceiptUrl = x.ReceiptUrl,
            Status = x.Status,
            IsRecurring = x.IsRecurring,
            RecurrencePattern = x.RecurrencePattern,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        };

    public static readonly System.Linq.Expressions.Expression<Func<ExpenseCategory, ExpenseCategoryResponse>> ToCategoryResponse =
        x => new ExpenseCategoryResponse
        {
            Id = x.Id,
            Name = x.Name,
            Description = x.Description,
            IsActive = x.IsActive,
            ExpenseCount = x.Expenses.Count,
            TotalAmount = x.Expenses.Sum(e => e.Amount),
            CreatedAt = x.CreatedAt
        };
}
