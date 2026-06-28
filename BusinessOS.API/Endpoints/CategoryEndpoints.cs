using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Categories.Commands.CreateCategory;
using BusinessOS.Application.Features.Categories.Commands.DeleteCategory;
using BusinessOS.Application.Features.Categories.Commands.UpdateCategory;
using BusinessOS.Application.Features.Categories.Queries.GetAllCategories;
using BusinessOS.Application.Features.Categories.Queries.GetCategoryById;
using BusinessOS.API.Authorization;
using MediatR;

namespace BusinessOS.API.Endpoints;

/// <summary>
/// Category management endpoints.
/// </summary>
public static class CategoryEndpoints
{
    /// <summary>
    /// Maps category CRUD and list endpoints under <c>/api/categories</c>.
    /// </summary>
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/categories")
            .WithTags("Categories")
            .RequireAuthorization();

        group.MapPost("", CreateCategory)
            .RequirePermission(PermissionCodes.CategoryCreate)
            .WithName("CreateCategory")
            .WithSummary("Create a category")
            .WithDescription("Creates a new product category for the current tenant.")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("", GetAllCategories)
            .RequirePermission(PermissionCodes.CategoryView)
            .WithName("GetAllCategories")
            .WithSummary("List categories")
            .WithDescription("Returns a paginated, searchable, and sortable list of categories.")
            .Produces<PagedResult<Application.Features.Categories.Queries.CategoryDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetCategoryById)
            .RequirePermission(PermissionCodes.CategoryView)
            .WithName("GetCategoryById")
            .WithSummary("Get category by id")
            .WithDescription("Returns a single category by its unique identifier.")
            .Produces<Application.Features.Categories.Queries.CategoryDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateCategory)
            .RequirePermission(PermissionCodes.CategoryUpdate)
            .WithName("UpdateCategory")
            .WithSummary("Update a category")
            .WithDescription("Updates an existing category's name and description.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteCategory)
            .RequirePermission(PermissionCodes.CategoryDelete)
            .WithName("DeleteCategory")
            .WithSummary("Delete a category")
            .WithDescription("Soft-deletes a category when it has no associated products.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> CreateCategory(
        CreateCategoryCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return Results.Created($"/api/categories/{id}", new { id });
    }

    private static async Task<IResult> GetAllCategories(
        string? search,
        int page,
        int pageSize,
        string? sortBy,
        string? sortDirection,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetAllCategoriesQuery(
                search,
                page,
                pageSize,
                sortBy,
                QueryableSortingExtensions.ParseSortDirection(sortDirection)),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetCategoryById(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCategoryByIdQuery(id), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateCategory(
        Guid id,
        UpdateCategoryRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateCategoryCommand(id, request.Name, request.Description),
            cancellationToken);
        return Results.NoContent();
    }

    private sealed record UpdateCategoryRequest(string Name, string? Description);

    private static async Task<IResult> DeleteCategory(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteCategoryCommand(id), cancellationToken);
        return Results.NoContent();
    }
}
