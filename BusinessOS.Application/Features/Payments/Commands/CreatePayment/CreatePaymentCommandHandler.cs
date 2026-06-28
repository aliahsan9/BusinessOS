using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Payments.Commands.CreatePayment;

public sealed class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CreatePaymentCommandHandler> _logger;

    public CreatePaymentCommandHandler(
        IApplicationDbContext context,
        ILogger<CreatePaymentCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Guid> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        var paymentMethod = request.PaymentMethod.Trim();

        if (!PaymentMethodNames.IsValid(paymentMethod))
            throw new BadRequestException($"Invalid payment method '{paymentMethod}'.");

        var order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.OrderId, cancellationToken);

        if (order is null)
            throw new NotFoundException("Order not found.");

        if (order.CustomerId != request.CustomerId)
            throw new BadRequestException("CustomerId does not match the order customer.");

        var customer = await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken);

        if (customer is null)
            throw new NotFoundException("Customer not found.");

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = request.OrderId,
            CustomerId = request.CustomerId,
            Amount = Math.Round(request.Amount, 2),
            PaymentMethod = paymentMethod,
            PaymentDate = request.PaymentDate.ToUniversalTime(),
            ReferenceNo = string.IsNullOrWhiteSpace(request.ReferenceNo)
                ? null
                : request.ReferenceNo.Trim()
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created payment {PaymentId} for order {OrderId}",
            payment.Id,
            order.Id);

        return payment.Id;
    }
}
