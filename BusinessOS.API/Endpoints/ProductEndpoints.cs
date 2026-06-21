using BusinessOS.Application.Products.Commands.CreateProduct;
using MediatR;

namespace BusinessOS.API.Endpoints;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/products",
            async (
                CreateProductCommand command,
                ISender sender) =>
            {
                var id = await sender.Send(command);

                return Results.Ok(id);
            })
            .RequireAuthorization();
    }
}
