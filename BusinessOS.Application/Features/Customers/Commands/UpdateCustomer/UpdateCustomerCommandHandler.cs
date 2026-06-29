using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Activities.DTOs;
using BusinessOS.Application.Features.Notifications.Services;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Customers.Commands.UpdateCustomer;

public sealed class UpdateCustomerCommandHandler : IRequestHandler<UpdateCustomerCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IBusinessEventService _businessEvents;
    private readonly ILogger<UpdateCustomerCommandHandler> _logger;

    public UpdateCustomerCommandHandler(
        IApplicationDbContext context,
        IBusinessEventService businessEvents,
        ILogger<UpdateCustomerCommandHandler> logger)
    {
        _context = context;
        _businessEvents = businessEvents;
        _logger = logger;
    }

    public async Task<Unit> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (customer is null)
            throw new NotFoundException("Customer not found");

        var email = request.Email.Trim();

        var duplicateExists = await _context.Customers
            .AnyAsync(x => x.Id != request.Id && x.Email == email, cancellationToken);

        if (duplicateExists)
            throw new ConflictException($"A customer with email '{email}' already exists.");

        customer.FirstName = request.FirstName.Trim();
        customer.LastName = request.LastName.Trim();
        customer.Email = email;
        customer.PhoneNumber = request.PhoneNumber.Trim();
        customer.Address = request.Address.Trim();
        customer.City = request.City.Trim();
        customer.Country = request.Country.Trim();
        customer.PostalCode = request.PostalCode.Trim();
        customer.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        var customerName = $"{customer.FirstName} {customer.LastName}".Trim();
        await PublishEventSafeAsync(
            new BusinessEventRequest(
                ActivityActions.Updated,
                ActivityEntityTypes.Customer,
                customer.Id,
                customerName,
                "Customer Updated",
                $"Customer {customerName} was updated.",
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
