using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Expenses.Commands.CreateExpense;

public sealed class CreateExpenseCommandHandler : IRequestHandler<CreateExpenseCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CreateExpenseCommandHandler> _logger;

    public CreateExpenseCommandHandler(
        IApplicationDbContext context,
        ILogger<CreateExpenseCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Guid> Handle(CreateExpenseCommand request, CancellationToken cancellationToken)
    {
        var categoryExists = await _context.ExpenseCategories
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.ExpenseCategoryId, cancellationToken);

        if (!categoryExists)
            throw new NotFoundException("Expense category not found.");

        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Amount = Math.Round(request.Amount, 2),
            ExpenseDate = request.ExpenseDate.ToUniversalTime(),
            ExpenseCategoryId = request.ExpenseCategoryId,
            PaymentMethod = request.PaymentMethod.Trim(),
            Vendor = string.IsNullOrWhiteSpace(request.Vendor) ? null : request.Vendor.Trim(),
            ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ReceiptUrl = string.IsNullOrWhiteSpace(request.ReceiptUrl) ? null : request.ReceiptUrl.Trim(),
            Status = request.Status.Trim(),
            IsRecurring = request.IsRecurring,
            RecurrencePattern = string.IsNullOrWhiteSpace(request.RecurrencePattern) ? null : request.RecurrencePattern.Trim()
        };

        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created expense {ExpenseId}", expense.Id);

        return expense.Id;
    }
}
