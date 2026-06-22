using MediatR;
using BusinessOS.Application.Features.Categories.Commands.CreateCategory;
using BusinessOS.Application.Features.Categories.Commands.UpdateCategory;
using BusinessOS.Application.Features.Categories.Commands.DeleteCategory;
using BusinessOS.Application.Features.Categories.Queries.GetAllCategories;
using BusinessOS.Application.Features.Categories.Queries.GetCategoryById;

namespace BusinessOS.API.Endpoints;

public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/categories")
                        .WithTags("Categories");

        // CREATE CATEGORY
        group.MapPost("/", async (CreateCategoryCommand command, IMediator mediator) =>
        {
            var id = await mediator.Send(command);
            return Results.Ok(id);
        });

        // GET ALL
        group.MapGet("/", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetAllCategoriesQuery());
            return Results.Ok(result);
        });

        // GET BY ID
        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetCategoryByIdQuery(id));
            return Results.Ok(result);
        });

        // UPDATE
        group.MapPut("/", async (UpdateCategoryCommand command, IMediator mediator) =>
        {
            await mediator.Send(command);
            return Results.Ok();
        });

        // DELETE
        group.MapDelete("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            await mediator.Send(new DeleteCategoryCommand(id));
            return Results.Ok();
        });
    }
}
