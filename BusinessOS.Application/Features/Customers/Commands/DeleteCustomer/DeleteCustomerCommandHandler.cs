using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Activities.DTOs;
using BusinessOS.Application.Features.Notifications.Services;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Customers.Commands.DeleteCustomer;

public sealed class DeleteCustomerCommandHandler : IRequestHandler<DeleteCustomerCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IBusinessEventService _businessEvents;
    private readonly ILogger<DeleteCustomerCommandHandler> _logger;

    public DeleteCustomerCommandHandler(
        IApplicationDbContext context,
        IBusinessEventService businessEvents,
        ILogger<DeleteCustomerCommandHandler> logger)
    {
        _context = context;
        _businessEvents = businessEvents;
        _logger = logger;
    }

    public async Task<Unit> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (customer is null)
            throw new NotFoundException("Customer not found");

        var customerName = $"{customer.FirstName} {customer.LastName}".Trim();

        customer.IsActive = false;
        _context.Customers.Remove(customer);

        await _context.SaveChangesAsync(cancellationToken);

        await PublishEventSafeAsync(
            new BusinessEventRequest(
                ActivityActions.Deleted,
                ActivityEntityTypes.Customer,
                customer.Id,
                customerName,
                "Customer Deleted",
                $"Customer {customerName} was deleted.",
                NotificationTypes.Customer),
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
            _logger.LogWarning(ex, "Failed to publish business event for customer {CustomerId}", request.EntityId);
        }
    }
}
