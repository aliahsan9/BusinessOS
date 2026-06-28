using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Suppliers.Commands.CreateSupplier;
using BusinessOS.Application.Features.Suppliers.Commands.DeleteSupplier;
using BusinessOS.Application.Features.Suppliers.Commands.UpdateSupplier;
using BusinessOS.Application.Features.Suppliers.Queries;
using BusinessOS.Application.Features.Suppliers.Queries.GetAllSuppliers;
using BusinessOS.Application.Features.Suppliers.Queries.GetSupplierById;
using BusinessOS.Application.Features.Suppliers.Queries.GetSupplierProducts;
using BusinessOS.Application.Features.Suppliers.Queries.GetSupplierPurchases;
using BusinessOS.API.Authorization;
using MediatR;

namespace BusinessOS.API.Endpoints;

/// <summary>
/// Supplier management endpoints.
/// </summary>
public static class SupplierEndpoints
{
    /// <summary>
    /// Maps supplier CRUD and related endpoints under <c>/api/suppliers</c>.
    /// </summary>
    public static void MapSupplierEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/suppliers")
            .WithTags("Suppliers")
            .RequireAuthorization();

        group.MapPost("", CreateSupplier)
            .RequirePermission(PermissionCodes.SupplierCreate)
            .WithName("CreateSupplier")
            .WithSummary("Create a supplier")
            .WithDescription("Creates a new supplier for the current tenant.")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("", GetAllSuppliers)
            .RequirePermission(PermissionCodes.SupplierView)
            .WithName("GetAllSuppliers")
            .WithSummary("List suppliers")
            .WithDescription(
                "Returns a paginated, searchable, and sortable list of suppliers. " +
                "Supports ?page=1&pageSize=10&search=Acme&sortBy=name&sortOrder=asc.")
            .Produces<PagedResult<SupplierSummaryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetSupplierById)
            .RequirePermission(PermissionCodes.SupplierView)
            .WithName("GetSupplierById")
            .WithSummary("Get supplier by id")
            .WithDescription("Returns complete supplier details by unique identifier.")
            .Produces<SupplierResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateSupplier)
            .RequirePermission(PermissionCodes.SupplierUpdate)
            .WithName("UpdateSupplier")
            .WithSummary("Update a supplier")
            .WithDescription("Updates an existing supplier's profile.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteSupplier)
            .RequirePermission(PermissionCodes.SupplierDelete)
            .WithName("DeleteSupplier")
            .WithSummary("Delete a supplier")
            .WithDescription("Soft-deletes a supplier.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/purchases", GetSupplierPurchases)
            .RequirePermission(PermissionCodes.SupplierView)
            .WithName("GetSupplierPurchases")
            .WithSummary("Get supplier purchase history")
            .WithDescription("Returns a paginated list of purchase orders for the specified supplier.")
            .Produces<PagedResult<SupplierPurchaseSummaryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/products", GetSupplierProducts)
            .RequirePermission(PermissionCodes.SupplierView)
            .WithName("GetSupplierProducts")
            .WithSummary("Get supplier products")
            .WithDescription("Returns products linked to the supplier via purchase order line items.")
            .Produces<IReadOnlyList<SupplierProductSummaryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateSupplier(
        CreateSupplierRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var id = await sender.Send(
            new CreateSupplierCommand(
                request.Name,
                request.Email,
                request.Phone,
                request.Address,
                request.ContactPerson,
                request.Notes),
            cancellationToken);

        return Results.Created($"/api/suppliers/{id}", new { id });
    }

    private static async Task<IResult> GetAllSuppliers(
        string? search,
        int page,
        int pageSize,
        string? sortBy,
        string? sortOrder,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetAllSuppliersQuery(
                search,
                page,
                pageSize,
                sortBy,
                QueryableSortingExtensions.ParseSortDirection(sortOrder)),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetSupplierById(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSupplierByIdQuery(id), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateSupplier(
        Guid id,
        UpdateSupplierRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateSupplierCommand(
                id,
                request.Name,
                request.Email,
                request.Phone,
                request.Address,
                request.ContactPerson,
                request.Notes),
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteSupplier(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteSupplierCommand(id), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetSupplierPurchases(
        Guid id,
        int page,
        int pageSize,
        string? sortBy,
        string? sortOrder,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSupplierPurchasesQuery(
                id,
                page,
                pageSize,
                sortBy,
                QueryableSortingExtensions.ParseSortDirection(sortOrder)),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetSupplierProducts(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSupplierProductsQuery(id), cancellationToken);
        return Results.Ok(result);
    }
}
