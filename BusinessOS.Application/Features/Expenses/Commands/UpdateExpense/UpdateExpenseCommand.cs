using MediatR;

namespace BusinessOS.Application.Features.Expenses.Commands.UpdateExpense;

public record UpdateExpenseCommand(
    Guid Id,
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
) : IRequest<Unit>;
