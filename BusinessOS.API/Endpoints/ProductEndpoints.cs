using BusinessOS.Application.Features.Products.Commands.CreateProduct;
using BusinessOS.Application.Features.Products.Queries.GetProductsByCategory;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.API.Endpoints;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products")
                       .WithTags("Products");

        // =========================
        // CREATE PRODUCT
        // =========================
        group.MapPost("/", async (
            CreateProductCommand command,
            ISender sender) =>
        {
            var id = await sender.Send(command);
            return Results.Ok(id);
        });

        // =========================
        // GET PRODUCTS BY CATEGORY
        // =========================
        group.MapGet("/by-category/{categoryId:guid}", async (
            Guid categoryId,
            ISender sender) =>
        {
            var result = await sender.Send(new GetProductsByCategoryQuery(categoryId));
            return Results.Ok(result);
        });
    }
}
