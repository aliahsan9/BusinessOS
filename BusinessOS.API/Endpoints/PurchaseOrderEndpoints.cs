using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.PurchaseOrders.Commands.CreatePurchaseOrder;
using BusinessOS.Application.Features.PurchaseOrders.Commands.DeletePurchaseOrder;
using BusinessOS.Application.Features.PurchaseOrders.Commands.ReceivePurchaseOrder;
using BusinessOS.Application.Features.PurchaseOrders.Commands.UpdatePurchaseOrder;
using BusinessOS.Application.Features.PurchaseOrders.Commands.UpdatePurchaseOrderStatus;
using BusinessOS.Application.Features.PurchaseOrders.Queries;
using BusinessOS.Application.Features.PurchaseOrders.Queries.GetAllPurchaseOrders;
using BusinessOS.Application.Features.PurchaseOrders.Queries.GetPurchaseOrderById;
using BusinessOS.API.Authorization;
using MediatR;

namespace BusinessOS.API.Endpoints;

/// <summary>
/// Purchase order management endpoints.
/// </summary>
public static class PurchaseOrderEndpoints
{
    /// <summary>
    /// Maps purchase order CRUD, status, and receive endpoints under <c>/api/purchase-orders</c>.
    /// </summary>
    public static void MapPurchaseOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/purchase-orders")
            .WithTags("Purchase Orders")
            .RequireAuthorization();

        group.MapPost("", CreatePurchaseOrder)
            .RequirePermission(PermissionCodes.PurchaseOrderCreate)
            .WithName("CreatePurchaseOrder")
            .WithSummary("Create a purchase order")
            .WithDescription("Creates a new purchase order with line items for an existing supplier.")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("", GetAllPurchaseOrders)
            .RequirePermission(PermissionCodes.PurchaseOrderView)
            .WithName("GetAllPurchaseOrders")
            .WithSummary("List purchase orders")
            .WithDescription(
                "Returns a paginated, searchable, filterable, and sortable list of purchase orders. " +
                "Supports ?page=1&pageSize=10&supplierId={guid}&status=Approved&search=PO-001&sortBy=purchaseDate&sortOrder=desc.")
            .Produces<PagedResult<PurchaseOrderSummaryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetPurchaseOrderById)
            .RequirePermission(PermissionCodes.PurchaseOrderView)
            .WithName("GetPurchaseOrderById")
            .WithSummary("Get purchase order by id")
            .WithDescription("Returns complete purchase order details including line items.")
            .Produces<PurchaseOrderResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdatePurchaseOrder)
            .RequirePermission(PermissionCodes.PurchaseOrderUpdate)
            .WithName("UpdatePurchaseOrder")
            .WithSummary("Update a purchase order")
            .WithDescription(
                "Updates an existing purchase order. Only orders in Draft or Pending status can be updated.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeletePurchaseOrder)
            .RequirePermission(PermissionCodes.PurchaseOrderDelete)
            .WithName("DeletePurchaseOrder")
            .WithSummary("Delete a purchase order")
            .WithDescription("Soft-deletes a purchase order. Received purchase orders cannot be deleted.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/{id:guid}/status", UpdatePurchaseOrderStatus)
            .RequirePermission(PermissionCodes.PurchaseOrderUpdate)
            .WithName("UpdatePurchaseOrderStatus")
            .WithSummary("Update purchase order status")
            .WithDescription(
                "Updates the purchase order status. Valid transitions: " +
                "Draft → Pending/Cancelled, Pending → Approved/Cancelled/Draft, Approved → Cancelled. " +
                "Use the receive endpoint to mark as Received.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/receive", ReceivePurchaseOrder)
            .RequirePermission(PermissionCodes.PurchaseOrderUpdate)
            .WithName("ReceivePurchaseOrder")
            .WithSummary("Receive a purchase order")
            .WithDescription(
                "Marks an Approved purchase order as Received and increases stock for each line item.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreatePurchaseOrder(
        CreatePurchaseOrderRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var id = await sender.Send(
            new CreatePurchaseOrderCommand(
                request.SupplierId,
                request.PurchaseDate,
                request.Status,
                request.ReferenceNumber,
                request.Notes,
                request.Items),
            cancellationToken);

        return Results.Created($"/api/purchase-orders/{id}", new { id });
    }

    private static async Task<IResult> GetAllPurchaseOrders(
        Guid? supplierId,
        string? status,
        string? search,
        int page,
        int pageSize,
        string? sortBy,
        string? sortOrder,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetAllPurchaseOrdersQuery(
                supplierId,
                status,
                search,
                page,
                pageSize,
                sortBy,
                QueryableSortingExtensions.ParseSortDirection(sortOrder)),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetPurchaseOrderById(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPurchaseOrderByIdQuery(id), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdatePurchaseOrder(
        Guid id,
        UpdatePurchaseOrderRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdatePurchaseOrderCommand(
                id,
                request.SupplierId,
                request.PurchaseDate,
                request.Status,
                request.ReferenceNumber,
                request.Notes,
                request.Items),
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DeletePurchaseOrder(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeletePurchaseOrderCommand(id), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> UpdatePurchaseOrderStatus(
        Guid id,
        UpdatePurchaseOrderStatusRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new UpdatePurchaseOrderStatusCommand(id, request.Status), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ReceivePurchaseOrder(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new ReceivePurchaseOrderCommand(id), cancellationToken);
        return Results.NoContent();
    }
}
