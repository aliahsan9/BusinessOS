using System.Text.Json;
using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Idempotency;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Invoices.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Payments.Commands.CreatePayment;

public sealed class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly IIdempotencyService _idempotency;
    private readonly ILogger<CreatePaymentCommandHandler> _logger;

    public CreatePaymentCommandHandler(
        IApplicationDbContext context,
        ICacheService cache,
        ITenantProvider tenantProvider,
        IIdempotencyService idempotency,
        ILogger<CreatePaymentCommandHandler> logger)
    {
        _context = context;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _idempotency = idempotency;
        _logger = logger;
    }

    public async Task<Guid> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        var paymentMethod = request.PaymentMethod.Trim();

        if (!PaymentMethodNames.IsValid(paymentMethod))
            throw new BadRequestException($"Invalid payment method '{paymentMethod}'.");

        if (request.Amount <= 0)
            throw new BadRequestException("Amount must be greater than zero.");

        var amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        var tenantId = _tenantProvider.TenantId;
        var currency = await _context.TenantSettings
            .AsNoTracking()
            .Select(settings => settings.Currency)
            .FirstOrDefaultAsync(cancellationToken) ?? "USD";

        IdempotencyRecord? staged = null;
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            if (!IdempotencyKeyRules.IsValid(request.IdempotencyKey))
                throw new BadRequestException("Idempotency-Key must be 1-128 visible ASCII characters without spaces.");

            var hash = CanonicalRequestHasher.PaymentCreate(
                tenantId,
                request.OrderId,
                request.CustomerId,
                amount,
                currency,
                paymentMethod,
                request.PaymentDate,
                request.ReferenceNo);

            var outcome = await _idempotency.BeginLocalAsync(
                IdempotencyOperations.PaymentCreate,
                request.IdempotencyKey,
                hash,
                cancellationToken);

            switch (outcome.Kind)
            {
                case IdempotencyOutcomeKind.Replay:
                    _logger.LogDebug(
                        "Replayed payment create {PaymentId} for tenant {TenantId}",
                        outcome.ResourceId,
                        tenantId);
                    return outcome.ResourceId
                        ?? throw new ConflictException(
                            "The original payment could not be replayed.",
                            ErrorCodes.IdempotencyKeyReused);
                case IdempotencyOutcomeKind.Conflict:
                    throw new ConflictException(
                        "This Idempotency-Key was already used for a different payment request.",
                        ErrorCodes.IdempotencyKeyReused);
                case IdempotencyOutcomeKind.InProgress:
                    throw new ConflictException(
                        "This payment is still processing. Retry the same request.",
                        ErrorCodes.IdempotencyInProgress);
                case IdempotencyOutcomeKind.Started:
                    staged = outcome.Record;
                    break;
            }
        }

        var order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.OrderId, cancellationToken);

        if (order is null)
            throw new NotFoundException("Order not found.");

        if (order.Status.Equals(OrderStatusNames.Cancelled, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Cancelled orders cannot be paid.");

        if (order.CustomerId != request.CustomerId)
            throw new BadRequestException("CustomerId does not match the order customer.");

        var customerExists = await _context.Customers
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.CustomerId, cancellationToken);

        if (!customerExists)
            throw new NotFoundException("Customer not found.");

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = request.OrderId,
            CustomerId = request.CustomerId,
            Amount = amount,
            PaymentMethod = paymentMethod,
            PaymentDate = request.PaymentDate.ToUniversalTime(),
            ReferenceNo = string.IsNullOrWhiteSpace(request.ReferenceNo)
                ? null
                : request.ReferenceNo.Trim(),
            Status = PaymentStatusNames.Completed,
            Currency = currency.Trim().ToUpperInvariant(),
            RefundedAmount = 0
        };

        _context.Payments.Add(payment);
        if (staged is not null)
        {
            _idempotency.CompleteLocal(
                staged,
                201,
                JsonSerializer.Serialize(new { id = payment.Id }),
                payment.Id);
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (staged is not null && DbExceptionClassifier.IsUniqueViolation(ex))
        {
            _context.Detach(payment);
            _idempotency.Detach(staged);
            var replay = await _idempotency.BeginLocalAsync(
                IdempotencyOperations.PaymentCreate,
                request.IdempotencyKey!,
                CanonicalRequestHasher.PaymentCreate(
                    tenantId,
                    request.OrderId,
                    request.CustomerId,
                    amount,
                    currency,
                    paymentMethod,
                    request.PaymentDate,
                    request.ReferenceNo),
                cancellationToken);

            if (replay.Kind == IdempotencyOutcomeKind.Replay && replay.ResourceId.HasValue)
                return replay.ResourceId.Value;

            if (replay.Record is not null && replay.Kind == IdempotencyOutcomeKind.Started)
                _idempotency.Detach(replay.Record);

            if (replay.Kind == IdempotencyOutcomeKind.Conflict)
            {
                throw new ConflictException(
                    "This Idempotency-Key was already used for a different payment request.",
                    ErrorCodes.IdempotencyKeyReused);
            }

            throw new ConflictException(
                "This payment is still processing. Retry the same request.",
                ErrorCodes.IdempotencyInProgress);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict while creating payment for order {OrderId}", order.Id);
            throw new ConflictException(
                "The order changed while the payment was being saved. Refresh and try again.",
                ErrorCodes.ConcurrencyConflict);
        }

        await ReconcileSafeAsync(order.Id, cancellationToken);

        await EntityCacheInvalidator.InvalidatePaymentAsync(
            _cache,
            tenantId,
            payment.Id,
            cancellationToken);

        _logger.LogInformation(
            "Created payment {PaymentId} for order {OrderId} in tenant {TenantId}",
            payment.Id,
            order.Id,
            tenantId);

        return payment.Id;
    }

    private async Task ReconcileSafeAsync(Guid orderId, CancellationToken cancellationToken)
    {
        try
        {
            await InvoicePaymentCalculator.ReconcileOrdersAsync(_context, [orderId], cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invoice balance reconcile failed for order {OrderId}", orderId);
        }
    }
}
