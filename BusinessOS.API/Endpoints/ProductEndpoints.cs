using BusinessOS.Application.Features.Products.Commands.CreateProduct;
using BusinessOS.Application.Features.Products.Commands.DeleteProduct;
using BusinessOS.Application.Features.Products.Commands.UpdateProduct;
using BusinessOS.Application.Features.Products.Queries.GetAllProducts;
using BusinessOS.Application.Features.Products.Queries.GetProductById;
using BusinessOS.Application.Features.Products.Queries.GetProductsByCategory;
using MediatR;

namespace BusinessOS.API.Endpoints;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products")
            .WithTags("Products")
            .RequireAuthorization();

        group.MapPost("", async (CreateProductCommand command, ISender sender) =>
            {
                var id = await sender.Send(command);
                return Results.Created($"/api/products/{id}", new { id });
            })
            .WithSummary("Create a product")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("", async (
                Guid? categoryId,
                string? search,
                int page,
                int pageSize,
                ISender sender) =>
            {
                var result = await sender.Send(new GetAllProductsQuery(
                    categoryId,
                    search,
                    page == 0 ? 1 : page,
                    pageSize == 0 ? 20 : pageSize));

                return Results.Ok(result);
            })
            .WithSummary("Get products with optional filtering and pagination")
            .Produces(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
            {
                var result = await sender.Send(new GetProductByIdQuery(id));
                return result is null
                    ? Results.Problem(
                        title: "Product not found",
                        statusCode: StatusCodes.Status404NotFound)
                    : Results.Ok(result);
            })
            .WithSummary("Get product by id")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/by-category/{categoryId:guid}", async (Guid categoryId, ISender sender) =>
            {
                var result = await sender.Send(new GetProductsByCategoryQuery(categoryId));
                return Results.Ok(result);
            })
            .WithSummary("Get products by category")
            .Produces(StatusCodes.Status200OK);

        group.MapPut("/{id:guid}", async (Guid id, UpdateProductRequest request, ISender sender) =>
            {
                await sender.Send(new UpdateProductCommand(
                    id,
                    request.CategoryId,
                    request.Name,
                    request.SKU,
                    request.Description,
                    request.CostPrice,
                    request.SalePrice,
                    request.ReorderLevel,
                    request.IsActive));

                return Results.NoContent();
            })
            .WithSummary("Update a product")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
            {
                await sender.Send(new DeleteProductCommand(id));
                return Results.NoContent();
            })
            .WithSummary("Delete a product")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
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
