using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Payments.Commands.DeletePayment;

public sealed class DeletePaymentCommandHandler : IRequestHandler<DeletePaymentCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<DeletePaymentCommandHandler> _logger;

    public DeletePaymentCommandHandler(
        IApplicationDbContext context,
        ILogger<DeletePaymentCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Unit> Handle(DeletePaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _context.Payments
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (payment is null)
            throw new NotFoundException("Payment not found.");

        _context.Payments.Remove(payment);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted payment {PaymentId}", payment.Id);

        return Unit.Value;
    }
}
