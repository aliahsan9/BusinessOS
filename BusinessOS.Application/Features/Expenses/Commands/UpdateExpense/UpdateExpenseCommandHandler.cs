using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Activities.DTOs;
using BusinessOS.Application.Features.Audit;
using BusinessOS.Application.Features.Audit.Services;
using BusinessOS.Application.Features.Notifications.Services;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Expenses.Commands.UpdateExpense;

public sealed class UpdateExpenseCommandHandler : IRequestHandler<UpdateExpenseCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IBusinessEventService _businessEvents;
    private readonly IEntityAuditService _entityAudit;
    private readonly ILogger<UpdateExpenseCommandHandler> _logger;

    public UpdateExpenseCommandHandler(
        IApplicationDbContext context,
        IBusinessEventService businessEvents,
        IEntityAuditService entityAudit,
        ILogger<UpdateExpenseCommandHandler> logger)
    {
        _context = context;
        _businessEvents = businessEvents;
        _entityAudit = entityAudit;
        _logger = logger;
    }

    public async Task<Unit> Handle(UpdateExpenseCommand request, CancellationToken cancellationToken)
    {
        var expense = await _context.Expenses
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Expense not found.");

        var oldSnapshot = EntityAuditSnapshots.ExpenseSnapshot(expense);

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

        try
        {
            await _entityAudit.LogChangeAsync(
                ActivityEntityTypes.Expense,
                expense.Id,
                ActivityActions.Update,
                oldSnapshot,
                EntityAuditSnapshots.ExpenseSnapshot(expense),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write entity audit for expense {ExpenseId}", expense.Id);
        }

        await PublishEventSafeAsync(
            new BusinessEventRequest(
                ActivityActions.Update,
                ActivityEntityTypes.Expense,
                expense.Id,
                expense.Title,
                "Expense Updated",
                $"Updated expense \"{expense.Title}\"",
                NotificationTypes.Info,
                Link: $"/expenses/{expense.Id}"),
            cancellationToken);

        return Unit.Value;
    }

    private async Task PublishEventSafeAsync(
        BusinessEventRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _businessEvents.PublishAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish business event for expense {ExpenseId}", request.EntityId);
        }
    }
}
