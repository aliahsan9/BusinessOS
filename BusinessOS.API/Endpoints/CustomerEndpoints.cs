using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Customers.Commands.CreateCustomer;
using BusinessOS.Application.Features.Customers.Commands.DeleteCustomer;
using BusinessOS.Application.Features.Customers.Commands.UpdateCustomer;
using BusinessOS.Application.Features.Customers.Queries;
using BusinessOS.Application.Features.Customers.Queries.GetAllCustomers;
using BusinessOS.Application.Features.Customers.Queries.GetCustomerAnalytics;
using BusinessOS.Application.Features.Customers.Queries.GetCustomerById;
using BusinessOS.Application.Features.Customers.Queries.GetCustomerOrders;
using MediatR;

namespace BusinessOS.API.Endpoints;

/// <summary>
/// Customer management endpoints.
/// </summary>
public static class CustomerEndpoints
{
    /// <summary>
    /// Maps customer CRUD, search, orders, and analytics endpoints under <c>/api/customers</c>.
    /// </summary>
    public static void MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers")
            .WithTags("Customers")
            .RequireAuthorization();

        group.MapPost("", CreateCustomer)
            .WithName("CreateCustomer")
            .WithSummary("Create a customer")
            .WithDescription(
                "Creates a new customer for the current tenant. " +
                "Email must be unique within the tenant.")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("", GetAllCustomers)
            .WithName("GetAllCustomers")
            .WithSummary("List customers")
            .WithDescription(
                "Returns a paginated, searchable, filterable, and sortable list of customers. " +
                "Supports ?page=1&pageSize=10&search=Ali&city=Lahore&country=Pakistan&sortBy=createdAt&sortOrder=desc.")
            .Produces<PagedResult<CustomerSummaryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetCustomerById)
            .WithName("GetCustomerById")
            .WithSummary("Get customer by id")
            .WithDescription("Returns complete customer details by unique identifier.")
            .Produces<CustomerResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateCustomer)
            .WithName("UpdateCustomer")
            .WithSummary("Update a customer")
            .WithDescription("Updates an existing customer's profile and active status.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteCustomer)
            .WithName("DeleteCustomer")
            .WithSummary("Delete a customer")
            .WithDescription(
                "Soft-deletes a customer. Customers with existing orders are soft-deleted " +
                "to preserve order history.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/orders", GetCustomerOrders)
            .WithName("GetCustomerOrders")
            .WithSummary("Get customer orders")
            .WithDescription("Returns a paginated list of orders for the specified customer.")
            .Produces<PagedResult<CustomerOrderResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/analytics", GetCustomerAnalytics)
            .WithName("GetCustomerAnalytics")
            .WithSummary("Get customer analytics")
            .WithDescription(
                "Returns order analytics for a customer including total orders, spending, " +
                "average order value, last order date, and completed order count.")
            .Produces<CustomerAnalyticsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateCustomer(
        CreateCustomerRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var id = await sender.Send(
            new CreateCustomerCommand(
                request.FirstName,
                request.LastName,
                request.Email,
                request.PhoneNumber,
                request.Address,
                request.City,
                request.Country,
                request.PostalCode),
            cancellationToken);

        return Results.Created($"/api/customers/{id}", new { id });
    }

    private static async Task<IResult> GetAllCustomers(
        string? search,
        string? city,
        string? country,
        int page,
        int pageSize,
        string? sortBy,
        string? sortOrder,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetAllCustomersQuery(
                search,
                city,
                country,
                page,
                pageSize,
                sortBy,
                QueryableSortingExtensions.ParseSortDirection(sortOrder)),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetCustomerById(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCustomerByIdQuery(id), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateCustomer(
        Guid id,
        UpdateCustomerRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateCustomerCommand(
                id,
                request.FirstName,
                request.LastName,
                request.Email,
                request.PhoneNumber,
                request.Address,
                request.City,
                request.Country,
                request.PostalCode,
                request.IsActive),
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteCustomer(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteCustomerCommand(id), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetCustomerOrders(
        Guid id,
        int page,
        int pageSize,
        string? sortBy,
        string? sortOrder,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetCustomerOrdersQuery(
                id,
                page,
                pageSize,
                sortBy,
                QueryableSortingExtensions.ParseSortDirection(sortOrder)),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetCustomerAnalytics(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCustomerAnalyticsQuery(id), cancellationToken);
        return Results.Ok(result);
    }
}
