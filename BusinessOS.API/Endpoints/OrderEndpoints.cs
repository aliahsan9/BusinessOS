using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Orders.Commands.CreateOrder;
using BusinessOS.Application.Features.Orders.Commands.DeleteOrder;
using BusinessOS.Application.Features.Orders.Commands.UpdateOrder;
using BusinessOS.Application.Features.Orders.Commands.UpdateOrderStatus;
using BusinessOS.Application.Features.Orders.Queries;
using BusinessOS.Application.Features.Orders.Queries.GetAllOrders;
using BusinessOS.Application.Features.Orders.Queries.GetOrderById;
using BusinessOS.API.Authorization;
using MediatR;

namespace BusinessOS.API.Endpoints;

/// <summary>
/// Order management endpoints.
/// </summary>
public static class OrderEndpoints
{
    /// <summary>
    /// Maps order CRUD, list, and status endpoints under <c>/api/orders</c>.
    /// </summary>
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders")
            .WithTags("Orders")
            .RequireAuthorization();

        group.MapPost("", CreateOrder)
            .RequirePermission(PermissionCodes.OrderCreate)
            .WithName("CreateOrder")
            .WithSummary("Create an order")
            .WithDescription(
                "Creates a new order for an existing active customer with line items. " +
                "Product prices are loaded automatically and totals are calculated.")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("", GetAllOrders)
            .RequirePermission(PermissionCodes.OrderView)
            .WithName("GetAllOrders")
            .WithSummary("List orders")
            .WithDescription(
                "Returns a paginated, searchable, filterable, and sortable list of orders. " +
                "Supports ?page=1&pageSize=10&search=Ali&status=Pending&sortBy=createdAt&sortOrder=desc.")
            .Produces<PagedResult<OrderSummaryDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetOrderById)
            .RequirePermission(PermissionCodes.OrderView)
            .WithName("GetOrderById")
            .WithSummary("Get order by id")
            .WithDescription("Returns complete order details including line items and product information.")
            .Produces<OrderDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateOrder)
            .RequirePermission(PermissionCodes.OrderUpdate)
            .WithName("UpdateOrder")
            .WithSummary("Update an order")
            .WithDescription(
                "Updates an existing order. Only orders in Pending or Confirmed status can be updated. " +
                "Totals are recalculated from current product prices.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteOrder)
            .RequirePermission(PermissionCodes.OrderDelete)
            .WithName("DeleteOrder")
            .WithSummary("Delete an order")
            .WithDescription(
                "Soft-deletes an order. Orders in Processing or Completed status cannot be deleted.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/{id:guid}/status", UpdateOrderStatus)
            .RequirePermission(PermissionCodes.OrderUpdate)
            .WithName("UpdateOrderStatus")
            .WithSummary("Update order status")
            .WithDescription(
                "Updates the order status. Valid transitions: " +
                "Pending → Confirmed/Cancelled, Confirmed → Processing/Cancelled, " +
                "Processing → Completed/Cancelled.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateOrder(
        CreateOrderCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return Results.Created($"/api/orders/{id}", new { id });
    }

    private static async Task<IResult> GetAllOrders(
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
            new GetAllOrdersQuery(
                status,
                search,
                page,
                pageSize,
                sortBy,
                QueryableSortingExtensions.ParseSortDirection(sortOrder)),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetOrderById(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetOrderByIdQuery(id), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateOrder(
        Guid id,
        UpdateOrderRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateOrderCommand(
                id,
                request.Discount,
                request.Tax,
                request.Items),
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteOrder(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteOrderCommand(id), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> UpdateOrderStatus(
        Guid id,
        UpdateOrderStatusRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new UpdateOrderStatusCommand(id, request.Status), cancellationToken);
        return Results.NoContent();
    }

    private sealed record UpdateOrderRequest(
        decimal Discount,
        decimal Tax,
        IReadOnlyList<CreateOrderItemDto> Items);

    private sealed record UpdateOrderStatusRequest(string Status);
}
