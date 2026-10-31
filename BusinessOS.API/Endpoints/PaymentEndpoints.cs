using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Idempotency;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Payments.Commands.CreatePayment;
using BusinessOS.Application.Features.Payments.Commands.DeletePayment;
using BusinessOS.Application.Features.Payments.Commands.RefundPayment;
using BusinessOS.Application.Features.Payments.Commands.UpdatePayment;
using BusinessOS.Application.Features.Payments.Queries;
using BusinessOS.Application.Features.Payments.Queries.GetAllPayments;
using BusinessOS.Application.Features.Payments.Queries.GetPaymentById;
using BusinessOS.API.Authorization;
using MediatR;

namespace BusinessOS.API.Endpoints;

/// <summary>
/// Payment management endpoints.
/// </summary>
public static class PaymentEndpoints
{
    /// <summary>
    /// Maps payment CRUD endpoints under <c>/api/v1/payments</c>.
    /// </summary>
    public static void MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/payments")
            .WithTags("Payments")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapPost("", CreatePayment)
            .RequirePermission(PermissionCodes.PaymentCreate)
            .WithName("CreatePayment")
            .WithSummary("Create a payment")
            .WithDescription(
                "Creates a new payment for an existing order. " +
                "Send an Idempotency-Key header (1-128 visible ASCII characters). " +
                "Reuse the same key when retrying the same payment. " +
                "A different amount, currency, order, or customer with the same key returns 409 IDEMPOTENCY_KEY_REUSED. " +
                "A completed retry returns the original payment id and does not create another payment.")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/refund", RefundPayment)
            .RequirePermission(PermissionCodes.PaymentUpdate)
            .WithName("RefundPayment")
            .WithSummary("Refund a payment")
            .WithDescription(
                "Refunds part or all of a completed payment. Requires Idempotency-Key. " +
                "The same key and payload returns the original refund. " +
                "A different amount or currency returns 409 IDEMPOTENCY_KEY_REUSED. " +
                "Concurrent refunds cannot exceed the remaining refundable amount. " +
                "This records a ledger refund. It does not call Stripe, JazzCash, or EasyPaisa.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("", GetAllPayments)
            .RequirePermission(PermissionCodes.PaymentView)
            .WithName("GetAllPayments")
            .WithSummary("List payments")
            .WithDescription(
                "Returns a paginated list of payments. " +
                "Supports ?page=1&pageSize=10&customerId={guid}&orderId={guid}&paymentMethod=Cash&dateFrom=2026-01-01&dateTo=2026-12-31&sortBy=paymentDate&sortOrder=desc.")
            .Produces<PagedResult<PaymentSummaryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetPaymentById)
            .RequirePermission(PermissionCodes.PaymentView)
            .WithName("GetPaymentById")
            .WithSummary("Get payment by id")
            .WithDescription("Returns complete payment details.")
            .Produces<PaymentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdatePayment)
            .RequirePermission(PermissionCodes.PaymentUpdate)
            .WithName("UpdatePayment")
            .WithSummary("Update a payment")
            .WithDescription("Updates an existing payment.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeletePayment)
            .RequirePermission(PermissionCodes.PaymentDelete)
            .WithName("DeletePayment")
            .WithSummary("Delete a payment")
            .WithDescription("Soft-deletes a payment.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreatePayment(
        CreatePaymentRequest request,
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = RequireIdempotencyKey(httpContext);
        var id = await sender.Send(
            new CreatePaymentCommand(
                request.OrderId,
                request.CustomerId,
                request.Amount,
                request.PaymentMethod,
                request.PaymentDate,
                request.ReferenceNo,
                idempotencyKey),
            cancellationToken);

        return Results.Created($"/api/v1/payments/{id}", new { id });
    }

    private static async Task<IResult> RefundPayment(
        Guid id,
        RefundPaymentRequest request,
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = RequireIdempotencyKey(httpContext);
        var paymentId = await sender.Send(
            new RefundPaymentCommand(id, request.Amount, request.Currency, idempotencyKey),
            cancellationToken);

        return Results.Ok(new { id = paymentId });
    }

    private static string RequireIdempotencyKey(HttpContext httpContext)
    {
        var key = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (!IdempotencyKeyRules.IsValid(key))
            throw new BadRequestException("Idempotency-Key header is required and must be 1-128 visible ASCII characters without spaces.");

        return key!;
    }

    private static async Task<IResult> GetAllPayments(
        Guid? customerId,
        Guid? orderId,
        string? paymentMethod,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page,
        int pageSize,
        string? sortBy,
        string? sortOrder,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetAllPaymentsQuery(
                customerId,
                orderId,
                paymentMethod,
                dateFrom,
                dateTo,
                page,
                pageSize,
                sortBy,
                QueryableSortingExtensions.ParseSortDirection(sortOrder)),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetPaymentById(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPaymentByIdQuery(id), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdatePayment(
        Guid id,
        UpdatePaymentRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdatePaymentCommand(
                id,
                request.OrderId,
                request.CustomerId,
                request.Amount,
                request.PaymentMethod,
                request.PaymentDate,
                request.ReferenceNo),
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DeletePayment(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeletePaymentCommand(id), cancellationToken);
        return Results.NoContent();
    }
}
