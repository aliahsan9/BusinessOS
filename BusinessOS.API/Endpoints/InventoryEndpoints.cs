using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Inventory.Commands.AdjustStock;
using BusinessOS.Application.Features.Inventory.Commands.DecreaseStock;
using BusinessOS.Application.Features.Inventory.Commands.IncreaseStock;
using BusinessOS.Application.Features.Inventory.Commands.UpdateInventory;
using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Application.Features.Inventory.Queries.GetAllInventory;
using BusinessOS.Application.Features.Inventory.Queries.GetInventoryAnalytics;
using BusinessOS.Application.Features.Inventory.Queries.GetInventoryByProductId;
using BusinessOS.Application.Features.Inventory.Queries.GetLowStockProducts;
using BusinessOS.Application.Features.Inventory.Queries.GetOutOfStockProducts;
using BusinessOS.Application.Features.Inventory.Queries.GetReorderProducts;
using BusinessOS.Application.Features.Inventory.Queries.GetStockTransactions;
using BusinessOS.API.Authorization;
using MediatR;

namespace BusinessOS.API.Endpoints;

/// <summary>
/// Inventory and stock management endpoints.
/// </summary>
public static class InventoryEndpoints
{
    /// <summary>
    /// Maps inventory endpoints under <c>/api/inventory</c>.
    /// </summary>
    public static void MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory")
            .WithTags("Inventory")
            .RequireAuthorization();

        group.MapGet("", GetAllInventory)
            .RequirePermission(PermissionCodes.InventoryView)
            .WithName("GetAllInventory")
            .WithSummary("List inventory records")
            .WithDescription(
                "Returns a paginated list of inventory records. " +
                "Supports search, low-stock/out-of-stock filters, and sorting by CurrentStock.")
            .Produces<PagedResult<InventorySummaryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{productId:guid}", GetInventoryByProductId)
            .RequirePermission(PermissionCodes.InventoryView)
            .WithName("GetInventoryByProductId")
            .WithSummary("Get inventory by product id")
            .WithDescription("Returns inventory details for a specific product.")
            .Produces<InventoryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{productId:guid}", UpdateInventory)
            .RequirePermission(PermissionCodes.InventoryUpdate)
            .WithName("UpdateInventory")
            .WithSummary("Update inventory thresholds")
            .WithDescription(
                "Updates minimum, maximum, and reorder levels for a product's inventory record.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/increase", IncreaseStock)
            .RequirePermission(PermissionCodes.InventoryAdjust)
            .WithName("IncreaseStock")
            .WithSummary("Increase stock")
            .WithDescription(
                "Increases product stock (e.g. purchase receipt). " +
                "Example: { \"productId\": \"...\", \"quantity\": 10, \"notes\": \"Supplier delivery\" }")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/decrease", DecreaseStock)
            .RequirePermission(PermissionCodes.InventoryAdjust)
            .WithName("DecreaseStock")
            .WithSummary("Decrease stock")
            .WithDescription(
                "Decreases product stock (e.g. sale or damage). " +
                "Stock cannot go below zero.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/adjust", AdjustStock)
            .RequirePermission(PermissionCodes.InventoryAdjust)
            .WithName("AdjustStock")
            .WithSummary("Adjust stock with transaction type")
            .WithDescription(
                "Adjusts stock using a specific transaction type: Purchase, Sale, Adjustment, Return, Damage, Transfer.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/transactions", GetStockTransactions)
            .RequirePermission(PermissionCodes.InventoryView)
            .WithName("GetStockTransactions")
            .WithSummary("Get stock transaction history")
            .WithDescription("Returns paginated stock transaction audit trail with optional product and type filters.")
            .Produces<PagedResult<StockTransactionResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/analytics", GetInventoryAnalytics)
            .RequirePermission(PermissionCodes.InventoryView)
            .WithName("GetInventoryAnalytics")
            .WithSummary("Get inventory analytics")
            .WithDescription(
                "Returns inventory KPIs including total stock, low/out-of-stock counts, inventory value, and movement trends.")
            .Produces<InventoryAnalyticsResponse>(StatusCodes.Status200OK);

        group.MapGet("/low-stock", GetLowStockProducts)
            .RequirePermission(PermissionCodes.InventoryView)
            .WithName("GetLowStockProducts")
            .WithSummary("Get low stock products")
            .WithDescription("Returns products where current stock is above zero but at or below reorder level.")
            .Produces<IReadOnlyList<InventorySummaryResponse>>(StatusCodes.Status200OK);

        group.MapGet("/out-of-stock", GetOutOfStockProducts)
            .RequirePermission(PermissionCodes.InventoryView)
            .WithName("GetOutOfStockProducts")
            .WithSummary("Get out of stock products")
            .WithDescription("Returns products with zero or negative stock.")
            .Produces<IReadOnlyList<InventorySummaryResponse>>(StatusCodes.Status200OK);

        group.MapGet("/reorder-products", GetReorderProducts)
            .RequirePermission(PermissionCodes.InventoryView)
            .WithName("GetReorderProducts")
            .WithSummary("Get products needing reorder")
            .WithDescription("Returns products at or below reorder level with suggested reorder quantities.")
            .Produces<IReadOnlyList<InventorySummaryResponse>>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> GetAllInventory(
        string? search,
        bool? lowStock,
        bool? outOfStock,
        int page,
        int pageSize,
        string? sortBy,
        string? sortOrder,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetAllInventoryQuery(
                search,
                lowStock,
                outOfStock,
                page,
                pageSize,
                sortBy,
                QueryableSortingExtensions.ParseSortDirection(sortOrder)),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetInventoryByProductId(
        Guid productId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetInventoryByProductIdQuery(productId), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateInventory(
        Guid productId,
        UpdateInventoryRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateInventoryCommand(
                productId,
                request.MinimumStockLevel,
                request.MaximumStockLevel,
                request.ReorderLevel),
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> IncreaseStock(
        StockChangeRequestBody request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new IncreaseStockCommand(
                request.ProductId,
                request.Quantity,
                request.ReferenceNumber,
                request.Notes),
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DecreaseStock(
        StockChangeRequestBody request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new DecreaseStockCommand(
                request.ProductId,
                request.Quantity,
                request.ReferenceNumber,
                request.Notes),
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> AdjustStock(
        AdjustStockRequestBody request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new AdjustStockCommand(
                request.ProductId,
                request.Quantity,
                request.TransactionType,
                request.ReferenceNumber,
                request.Notes),
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetStockTransactions(
        Guid? productId,
        string? transactionType,
        string? search,
        int page,
        int pageSize,
        string? sortBy,
        string? sortOrder,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetStockTransactionsQuery(
                productId,
                transactionType,
                search,
                page,
                pageSize,
                sortBy,
                QueryableSortingExtensions.ParseSortDirection(sortOrder)),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetInventoryAnalytics(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetInventoryAnalyticsQuery(), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetLowStockProducts(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetLowStockProductsQuery(), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetOutOfStockProducts(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetOutOfStockProductsQuery(), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetReorderProducts(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetReorderProductsQuery(), cancellationToken);
        return Results.Ok(result);
    }

    /// <summary>Update inventory threshold levels.</summary>
    /// <example>{ "minimumStockLevel": 5, "maximumStockLevel": 100, "reorderLevel": 10 }</example>
    private sealed record UpdateInventoryRequest(
        decimal MinimumStockLevel,
        decimal MaximumStockLevel,
        decimal ReorderLevel);

    /// <summary>Increase or decrease stock quantity.</summary>
    /// <example>{ "productId": "3fa85f64-5717-4562-b3fc-2c963f66afa6", "quantity": 10, "notes": "Restock" }</example>
    private sealed record StockChangeRequestBody(
        Guid ProductId,
        decimal Quantity,
        string? ReferenceNumber,
        string? Notes);

    /// <summary>Adjust stock with explicit transaction type.</summary>
    /// <example>{ "productId": "3fa85f64-5717-4562-b3fc-2c963f66afa6", "quantity": 5, "transactionType": "Adjustment", "notes": "Cycle count" }</example>
    private sealed record AdjustStockRequestBody(
        Guid ProductId,
        decimal Quantity,
        string TransactionType,
        string? ReferenceNumber,
        string? Notes);
}
