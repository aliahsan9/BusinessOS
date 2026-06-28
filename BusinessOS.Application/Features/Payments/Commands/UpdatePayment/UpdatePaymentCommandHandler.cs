using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Payments.Commands.UpdatePayment;

public sealed class UpdatePaymentCommandHandler : IRequestHandler<UpdatePaymentCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<UpdatePaymentCommandHandler> _logger;

    public UpdatePaymentCommandHandler(
        IApplicationDbContext context,
        ILogger<UpdatePaymentCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Unit> Handle(UpdatePaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _context.Payments
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (payment is null)
            throw new NotFoundException("Payment not found.");

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

        payment.OrderId = request.OrderId;
        payment.CustomerId = request.CustomerId;
        payment.Amount = Math.Round(request.Amount, 2);
        payment.PaymentMethod = paymentMethod;
        payment.PaymentDate = request.PaymentDate.ToUniversalTime();
        payment.ReferenceNo = string.IsNullOrWhiteSpace(request.ReferenceNo)
            ? null
            : request.ReferenceNo.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated payment {PaymentId}", payment.Id);

        return Unit.Value;
    }
}
