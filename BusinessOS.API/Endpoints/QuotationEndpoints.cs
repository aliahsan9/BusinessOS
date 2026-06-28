using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Quotations.Commands.ConvertQuotationToOrder;
using BusinessOS.Application.Features.Quotations.Commands.CreateQuotation;
using BusinessOS.Application.Features.Quotations.Commands.DeleteQuotation;
using BusinessOS.Application.Features.Quotations.Commands.UpdateQuotation;
using BusinessOS.Application.Features.Quotations.Commands.UpdateQuotationStatus;
using BusinessOS.Application.Features.Quotations.Queries;
using BusinessOS.Application.Features.Quotations.Queries.GetAllQuotations;
using BusinessOS.Application.Features.Quotations.Queries.GetQuotationById;
using BusinessOS.API.Authorization;
using MediatR;

namespace BusinessOS.API.Endpoints;

/// <summary>
/// Quotation management endpoints.
/// </summary>
public static class QuotationEndpoints
{
    /// <summary>
    /// Maps quotation CRUD, status, and convert endpoints under <c>/api/quotations</c>.
    /// </summary>
    public static void MapQuotationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/quotations")
            .WithTags("Quotations")
            .RequireAuthorization();

        group.MapPost("", CreateQuotation)
            .RequirePermission(PermissionCodes.QuotationCreate)
            .WithName("CreateQuotation")
            .WithSummary("Create a quotation")
            .WithDescription("Creates a new quotation with line items. No inventory is deducted on creation.")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("", GetAllQuotations)
            .RequirePermission(PermissionCodes.QuotationView)
            .WithName("GetAllQuotations")
            .WithSummary("List quotations")
            .WithDescription(
                "Returns a paginated list of quotations. " +
                "Supports ?page=1&pageSize=10&status=Draft&customerId={guid}&search=QUO-001&sortBy=quotationDate&sortOrder=desc.")
            .Produces<PagedResult<QuotationSummaryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetQuotationById)
            .RequirePermission(PermissionCodes.QuotationView)
            .WithName("GetQuotationById")
            .WithSummary("Get quotation by id")
            .WithDescription("Returns complete quotation details including line items.")
            .Produces<QuotationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateQuotation)
            .RequirePermission(PermissionCodes.QuotationUpdate)
            .WithName("UpdateQuotation")
            .WithSummary("Update a quotation")
            .WithDescription("Updates an existing quotation. Only Draft or Sent quotations can be updated.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteQuotation)
            .RequirePermission(PermissionCodes.QuotationDelete)
            .WithName("DeleteQuotation")
            .WithSummary("Delete a quotation")
            .WithDescription("Soft-deletes a quotation. Accepted quotations cannot be deleted.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/{id:guid}/status", UpdateQuotationStatus)
            .RequirePermission(PermissionCodes.QuotationUpdate)
            .WithName("UpdateQuotationStatus")
            .WithSummary("Update quotation status")
            .WithDescription(
                "Updates the quotation status. Valid transitions: " +
                "Draft → Sent/Rejected, Sent → Accepted/Rejected/Expired.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/convert-to-order", ConvertQuotationToOrder)
            .RequirePermission(PermissionCodes.QuotationUpdate)
            .WithName("ConvertQuotationToOrder")
            .WithSummary("Convert quotation to order")
            .WithDescription(
                "Creates an order from an accepted quotation and deducts inventory for each line item.")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> CreateQuotation(
        CreateQuotationRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var id = await sender.Send(
            new CreateQuotationCommand(
                request.CustomerId,
                request.QuotationDate,
                request.ExpiryDate,
                request.Status,
                request.Discount,
                request.Tax,
                request.Notes,
                request.Items),
            cancellationToken);

        return Results.Created($"/api/quotations/{id}", new { id });
    }

    private static async Task<IResult> GetAllQuotations(
        string? status,
        Guid? customerId,
        string? search,
        int page,
        int pageSize,
        string? sortBy,
        string? sortOrder,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetAllQuotationsQuery(
                status,
                customerId,
                search,
                page,
                pageSize,
                sortBy,
                QueryableSortingExtensions.ParseSortDirection(sortOrder)),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetQuotationById(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetQuotationByIdQuery(id), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateQuotation(
        Guid id,
        UpdateQuotationRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateQuotationCommand(
                id,
                request.CustomerId,
                request.QuotationDate,
                request.ExpiryDate,
                request.Status,
                request.Discount,
                request.Tax,
                request.Notes,
                request.Items),
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteQuotation(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteQuotationCommand(id), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> UpdateQuotationStatus(
        Guid id,
        UpdateQuotationStatusRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new UpdateQuotationStatusCommand(id, request.Status), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ConvertQuotationToOrder(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var orderId = await sender.Send(new ConvertQuotationToOrderCommand(id), cancellationToken);
        return Results.Created($"/api/orders/{orderId}", new { id = orderId });
    }
}
