using BusinessOS.Application.Features.Expenses.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Expenses.Commands.CreateExpense;

public record CreateExpenseCommand(
    string Title,
    decimal Amount,
    DateTime ExpenseDate,
    Guid ExpenseCategoryId,
    string PaymentMethod,
    string? Vendor,
    string? ReferenceNumber,
    string? Description,
    string? ReceiptUrl,
    string Status,
    bool IsRecurring,
    string? RecurrencePattern
) : IRequest<Guid>;
