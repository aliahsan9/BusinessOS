using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Products.Commands.CreateProduct;
using BusinessOS.Application.Features.Products.Commands.DeleteProduct;
using BusinessOS.Application.Features.Products.Commands.UpdateProduct;
using BusinessOS.Application.Features.Products.Queries;
using BusinessOS.Application.Features.Products.Queries.GetAllProducts;
using BusinessOS.Application.Features.Products.Queries.GetProductById;
using BusinessOS.Application.Features.Products.Queries.GetProductsByCategory;
using BusinessOS.API.Authorization;
using MediatR;

namespace BusinessOS.API.Endpoints;

/// <summary>
/// Product management endpoints.
/// </summary>
public static class ProductEndpoints
{
    /// <summary>
    /// Maps product CRUD and list endpoints under <c>/api/products</c>.
    /// </summary>
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products")
            .WithTags("Products")
            .RequireAuthorization();

        group.MapPost("", CreateProduct)
            .RequirePermission(PermissionCodes.ProductCreate)
            .WithName("CreateProduct")
            .WithSummary("Create a product")
            .WithDescription("Creates a new product in the specified category.")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("", GetAllProducts)
            .RequirePermission(PermissionCodes.ProductView)
            .WithName("GetAllProducts")
            .WithSummary("List products")
            .WithDescription("Returns a paginated, searchable, and sortable list of products.")
            .Produces<PagedResult<ProductDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetProductById)
            .RequirePermission(PermissionCodes.ProductView)
            .WithName("GetProductById")
            .WithSummary("Get product by id")
            .WithDescription("Returns a single product by its unique identifier.")
            .Produces<ProductDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/by-category/{categoryId:guid}", GetProductsByCategory)
            .RequirePermission(PermissionCodes.ProductView)
            .WithName("GetProductsByCategory")
            .WithSummary("List products by category")
            .WithDescription("Returns a paginated list of products belonging to the specified category.")
            .Produces<PagedResult<ProductDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateProduct)
            .RequirePermission(PermissionCodes.ProductUpdate)
            .WithName("UpdateProduct")
            .WithSummary("Update a product")
            .WithDescription("Updates an existing product's details.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteProduct)
            .RequirePermission(PermissionCodes.ProductDelete)
            .WithName("DeleteProduct")
            .WithSummary("Delete a product")
            .WithDescription("Soft-deletes a product.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateProduct(
        CreateProductCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return Results.Created($"/api/products/{id}", new { id });
    }

    private static async Task<IResult> GetAllProducts(
        Guid? categoryId,
        string? search,
        int page,
        int pageSize,
        string? sortBy,
        string? sortDirection,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetAllProductsQuery(
                categoryId,
                search,
                page,
                pageSize,
                sortBy,
                QueryableSortingExtensions.ParseSortDirection(sortDirection)),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetProductById(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetProductByIdQuery(id), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetProductsByCategory(
        Guid categoryId,
        string? search,
        int page,
        int pageSize,
        string? sortBy,
        string? sortDirection,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetProductsByCategoryQuery(
                categoryId,
                search,
                page,
                pageSize,
                sortBy,
                QueryableSortingExtensions.ParseSortDirection(sortDirection)),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateProduct(
        Guid id,
        UpdateProductRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateProductCommand(
                id,
                request.CategoryId,
                request.Name,
                request.SKU,
                request.Description,
                request.CostPrice,
                request.SalePrice,
                request.ReorderLevel,
                request.IsActive),
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteProduct(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteProductCommand(id), cancellationToken);
        return Results.NoContent();
    }

    private sealed record UpdateProductRequest(
        Guid CategoryId,
        string Name,
        string SKU,
        string? Description,
        decimal CostPrice,
        decimal SalePrice,
        decimal ReorderLevel,
        bool IsActive);
}
