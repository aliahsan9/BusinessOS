using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Activities.DTOs;
using BusinessOS.Application.Features.Audit;
using BusinessOS.Application.Features.Audit.Services;
using BusinessOS.Application.Features.Notifications.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Expenses.Commands.CreateExpense;

public sealed class CreateExpenseCommandHandler : IRequestHandler<CreateExpenseCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IBusinessEventService _businessEvents;
    private readonly IEntityAuditService _entityAudit;
    private readonly ILogger<CreateExpenseCommandHandler> _logger;

    public CreateExpenseCommandHandler(
        IApplicationDbContext context,
        IBusinessEventService businessEvents,
        IEntityAuditService entityAudit,
        ILogger<CreateExpenseCommandHandler> logger)
    {
        _context = context;
        _businessEvents = businessEvents;
        _entityAudit = entityAudit;
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

        try
        {
            await _entityAudit.LogChangeAsync(
                ActivityEntityTypes.Expense,
                expense.Id,
                ActivityActions.ExpenseAdded,
                null,
                EntityAuditSnapshots.ExpenseSnapshot(expense),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write entity audit for expense {ExpenseId}", expense.Id);
        }

        await PublishEventSafeAsync(
            new BusinessEventRequest(
                ActivityActions.ExpenseAdded,
                ActivityEntityTypes.Expense,
                expense.Id,
                expense.Title,
                "Expense Added",
                $"Expense \"{expense.Title}\" was added.",
                NotificationTypes.Info,
                Link: $"/expenses/{expense.Id}"),
            cancellationToken);

        return expense.Id;
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
