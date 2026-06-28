using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Payments.Commands.CreatePayment;
using BusinessOS.Application.Features.Payments.Commands.DeletePayment;
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
    /// Maps payment CRUD endpoints under <c>/api/payments</c>.
    /// </summary>
    public static void MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments")
            .WithTags("Payments")
            .RequireAuthorization();

        group.MapPost("", CreatePayment)
            .RequirePermission(PermissionCodes.PaymentCreate)
            .WithName("CreatePayment")
            .WithSummary("Create a payment")
            .WithDescription("Creates a new payment for an existing order.")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

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
        ISender sender,
        CancellationToken cancellationToken)
    {
        var id = await sender.Send(
            new CreatePaymentCommand(
                request.OrderId,
                request.CustomerId,
                request.Amount,
                request.PaymentMethod,
                request.PaymentDate,
                request.ReferenceNo),
            cancellationToken);

        return Results.Created($"/api/payments/{id}", new { id });
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
