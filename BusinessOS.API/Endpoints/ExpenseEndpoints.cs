using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Expenses.Commands.CreateExpense;
using BusinessOS.Application.Features.Expenses.Commands.CreateExpenseCategory;
using BusinessOS.Application.Features.Expenses.Commands.DeleteExpense;
using BusinessOS.Application.Features.Expenses.Commands.DeleteExpenseCategory;
using BusinessOS.Application.Features.Expenses.Commands.UpdateExpense;
using BusinessOS.Application.Features.Expenses.Commands.UpdateExpenseCategory;
using BusinessOS.Application.Features.Expenses.Queries;
using BusinessOS.Application.Features.Expenses.Queries.GetAllExpenseCategories;
using BusinessOS.Application.Features.Expenses.Queries.GetAllExpenses;
using BusinessOS.Application.Features.Expenses.Queries.GetExpenseAnalytics;
using BusinessOS.Application.Features.Expenses.Queries.GetExpenseById;
using BusinessOS.Application.Features.Expenses.Queries.GetExpenseCategoryById;
using BusinessOS.API.Authorization;
using MediatR;

namespace BusinessOS.API.Endpoints;

public static class ExpenseEndpoints
{
    public static void MapExpenseEndpoints(this IEndpointRouteBuilder app)
    {
        var expenses = app.MapGroup("/api/expenses")
            .WithTags("Expenses")
            .RequireAuthorization();

        expenses.MapPost("", CreateExpense)
            .RequirePermission(PermissionCodes.ExpenseCreate)
            .WithName("CreateExpense")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        expenses.MapGet("", GetAllExpenses)
            .RequirePermission(PermissionCodes.ExpenseView)
            .WithName("GetAllExpenses")
            .Produces<PagedResult<ExpenseSummaryResponse>>(StatusCodes.Status200OK);

        expenses.MapGet("/analytics", GetExpenseAnalytics)
            .RequirePermission(PermissionCodes.ExpenseView)
            .WithName("GetExpenseAnalytics")
            .Produces<ExpenseAnalyticsResponse>(StatusCodes.Status200OK);

        expenses.MapGet("/{id:guid}", GetExpenseById)
            .RequirePermission(PermissionCodes.ExpenseView)
            .WithName("GetExpenseById")
            .Produces<ExpenseResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        expenses.MapPut("/{id:guid}", UpdateExpense)
            .RequirePermission(PermissionCodes.ExpenseUpdate)
            .WithName("UpdateExpense")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        expenses.MapDelete("/{id:guid}", DeleteExpense)
            .RequirePermission(PermissionCodes.ExpenseDelete)
            .WithName("DeleteExpense")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var categories = app.MapGroup("/api/expense-categories")
            .WithTags("Expense Categories")
            .RequireAuthorization();

        categories.MapPost("", CreateExpenseCategory)
            .RequirePermission(PermissionCodes.ExpenseCategoryCreate)
            .WithName("CreateExpenseCategory")
            .Produces(StatusCodes.Status201Created);

        categories.MapGet("", GetAllExpenseCategories)
            .RequirePermission(PermissionCodes.ExpenseCategoryView)
            .WithName("GetAllExpenseCategories")
            .Produces<IReadOnlyList<ExpenseCategoryResponse>>(StatusCodes.Status200OK);

        categories.MapGet("/{id:guid}", GetExpenseCategoryById)
            .RequirePermission(PermissionCodes.ExpenseCategoryView)
            .WithName("GetExpenseCategoryById")
            .Produces<ExpenseCategoryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        categories.MapPut("/{id:guid}", UpdateExpenseCategory)
            .RequirePermission(PermissionCodes.ExpenseCategoryUpdate)
            .WithName("UpdateExpenseCategory")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        categories.MapDelete("/{id:guid}", DeleteExpenseCategory)
            .RequirePermission(PermissionCodes.ExpenseCategoryDelete)
            .WithName("DeleteExpenseCategory")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateExpense(
        CreateExpenseRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var id = await sender.Send(
            new CreateExpenseCommand(
                request.Title,
                request.Amount,
                request.ExpenseDate,
                request.ExpenseCategoryId,
                request.PaymentMethod,
                request.Vendor,
                request.ReferenceNumber,
                request.Description,
                request.ReceiptUrl,
                request.Status,
                request.IsRecurring,
                request.RecurrencePattern),
            cancellationToken);

        return Results.Created($"/api/expenses/{id}", new { id });
    }

    private static async Task<IResult> GetAllExpenses(
        Guid? categoryId,
        string? status,
        string? search,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page,
        int pageSize,
        string? sortBy,
        string? sortOrder,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetAllExpensesQuery(
                categoryId,
                status,
                search,
                dateFrom,
                dateTo,
                page,
                pageSize,
                sortBy,
                QueryableSortingExtensions.ParseSortDirection(sortOrder)),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetExpenseAnalytics(
        DateTime? dateFrom,
        DateTime? dateTo,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetExpenseAnalyticsQuery(dateFrom, dateTo),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetExpenseById(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetExpenseByIdQuery(id), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateExpense(
        Guid id,
        UpdateExpenseRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateExpenseCommand(
                id,
                request.Title,
                request.Amount,
                request.ExpenseDate,
                request.ExpenseCategoryId,
                request.PaymentMethod,
                request.Vendor,
                request.ReferenceNumber,
                request.Description,
                request.ReceiptUrl,
                request.Status,
                request.IsRecurring,
                request.RecurrencePattern),
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteExpense(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteExpenseCommand(id), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> CreateExpenseCategory(
        CreateExpenseCategoryRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var id = await sender.Send(
            new CreateExpenseCategoryCommand(request.Name, request.Description),
            cancellationToken);

        return Results.Created($"/api/expense-categories/{id}", new { id });
    }

    private static async Task<IResult> GetAllExpenseCategories(
        bool? activeOnly,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetAllExpenseCategoriesQuery(activeOnly),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetExpenseCategoryById(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetExpenseCategoryByIdQuery(id), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateExpenseCategory(
        Guid id,
        UpdateExpenseCategoryRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateExpenseCategoryCommand(id, request.Name, request.Description, request.IsActive),
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteExpenseCategory(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteExpenseCategoryCommand(id), cancellationToken);
        return Results.NoContent();
    }
}
