using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Activities.DTOs;
using BusinessOS.Application.Features.Notifications.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Customers.Commands.CreateCustomer;

public sealed class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IBusinessEventService _businessEvents;
    private readonly ILogger<CreateCustomerCommandHandler> _logger;

    public CreateCustomerCommandHandler(
        IApplicationDbContext context,
        IBusinessEventService businessEvents,
        ILogger<CreateCustomerCommandHandler> logger)
    {
        _context = context;
        _businessEvents = businessEvents;
        _logger = logger;
    }

    public async Task<Guid> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();

        var duplicateExists = await _context.Customers
            .AnyAsync(x => x.Email == email, cancellationToken);

        if (duplicateExists)
            throw new ConflictException($"A customer with email '{email}' already exists.");

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            PhoneNumber = request.PhoneNumber.Trim(),
            Address = request.Address.Trim(),
            City = request.City.Trim(),
            Country = request.Country.Trim(),
            PostalCode = request.PostalCode.Trim(),
            IsActive = true
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(cancellationToken);

        var customerName = $"{customer.FirstName} {customer.LastName}".Trim();
        await PublishEventSafeAsync(
            new BusinessEventRequest(
                ActivityActions.Created,
                ActivityEntityTypes.Customer,
                customer.Id,
                customerName,
                "Customer Created",
                $"Customer {customerName} was created.",
                NotificationTypes.Customer),
            cancellationToken);

        return customer.Id;
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
