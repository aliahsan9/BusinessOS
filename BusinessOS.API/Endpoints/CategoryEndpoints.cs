using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Features.Categories.Commands.CreateCategory;
using BusinessOS.Application.Features.Categories.Commands.DeleteCategory;
using BusinessOS.Application.Features.Categories.Commands.UpdateCategory;
using BusinessOS.Application.Features.Categories.Queries.GetAllCategories;
using BusinessOS.Application.Features.Categories.Queries.GetCategoryById;
using MediatR;

namespace BusinessOS.API.Endpoints;

public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/categories")
            .WithTags("Categories")
            .RequireAuthorization();

        group.MapPost("", async (CreateCategoryCommand command, IMediator mediator) =>
            {
                var id = await mediator.Send(command);
                return Results.Created($"/api/categories/{id}", new { id });
            })
            .WithSummary("Create a category")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("", async (IMediator mediator) =>
            {
                var result = await mediator.Send(new GetAllCategoriesQuery());
                return Results.Ok(result);
            })
            .WithSummary("Get all categories")
            .Produces(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator) =>
            {
                var result = await mediator.Send(new GetCategoryByIdQuery(id));
                return result is null
                    ? Results.Problem(
                        title: "Category not found",
                        statusCode: StatusCodes.Status404NotFound)
                    : Results.Ok(result);
            })
            .WithSummary("Get category by id")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (Guid id, UpdateCategoryRequest request, IMediator mediator) =>
            {
                await mediator.Send(new UpdateCategoryCommand(id, request.Name, request.Description));
                return Results.NoContent();
            })
            .WithSummary("Update a category")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (Guid id, IMediator mediator) =>
            {
                await mediator.Send(new DeleteCategoryCommand(id));
                return Results.NoContent();
            })
            .WithSummary("Delete a category")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private sealed record UpdateCategoryRequest(string Name, string? Description);
}
