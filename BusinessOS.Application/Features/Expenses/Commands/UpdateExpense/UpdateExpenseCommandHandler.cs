using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Expenses.Commands.UpdateExpense;

public sealed class UpdateExpenseCommandHandler : IRequestHandler<UpdateExpenseCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<UpdateExpenseCommandHandler> _logger;

    public UpdateExpenseCommandHandler(
        IApplicationDbContext context,
        ILogger<UpdateExpenseCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Unit> Handle(UpdateExpenseCommand request, CancellationToken cancellationToken)
    {
        var expense = await _context.Expenses
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Expense not found.");

        var categoryExists = await _context.ExpenseCategories
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.ExpenseCategoryId, cancellationToken);

        if (!categoryExists)
            throw new NotFoundException("Expense category not found.");

        expense.Title = request.Title.Trim();
        expense.Amount = Math.Round(request.Amount, 2);
        expense.ExpenseDate = request.ExpenseDate.ToUniversalTime();
        expense.ExpenseCategoryId = request.ExpenseCategoryId;
        expense.PaymentMethod = request.PaymentMethod.Trim();
        expense.Vendor = string.IsNullOrWhiteSpace(request.Vendor) ? null : request.Vendor.Trim();
        expense.ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim();
        expense.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        expense.ReceiptUrl = string.IsNullOrWhiteSpace(request.ReceiptUrl) ? null : request.ReceiptUrl.Trim();
        expense.Status = request.Status.Trim();
        expense.IsRecurring = request.IsRecurring;
        expense.RecurrencePattern = string.IsNullOrWhiteSpace(request.RecurrencePattern) ? null : request.RecurrencePattern.Trim();
        expense.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated expense {ExpenseId}", expense.Id);

        return Unit.Value;
    }
}
