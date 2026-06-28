using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Invoices.Commands.CreateInvoiceFromOrder;
using BusinessOS.Application.Features.Invoices.Commands.DeleteInvoice;
using BusinessOS.Application.Features.Invoices.Commands.UpdateInvoice;
using BusinessOS.Application.Features.Invoices.Commands.UpdateInvoiceStatus;
using BusinessOS.Application.Features.Invoices.Queries;
using BusinessOS.Application.Features.Invoices.Queries.GetAllInvoices;
using BusinessOS.Application.Features.Invoices.Queries.GetInvoiceById;
using BusinessOS.Application.Features.Invoices.Queries.GetInvoicePdf;
using BusinessOS.API.Authorization;
using MediatR;

namespace BusinessOS.API.Endpoints;

/// <summary>
/// Invoice management endpoints.
/// </summary>
public static class InvoiceEndpoints
{
    /// <summary>
    /// Maps invoice endpoints under <c>/api/invoices</c>.
    /// </summary>
    public static void MapInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/invoices")
            .WithTags("Invoices")
            .RequireAuthorization();

        group.MapPost("/from-order/{orderId:guid}", CreateInvoiceFromOrder)
            .RequirePermission(PermissionCodes.InvoiceCreate)
            .WithName("CreateInvoiceFromOrder")
            .WithSummary("Create invoice from order")
            .WithDescription("Creates a new invoice from a completed order. Only one invoice per order is allowed.")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("", GetAllInvoices)
            .RequirePermission(PermissionCodes.InvoiceView)
            .WithName("GetAllInvoices")
            .WithSummary("List invoices")
            .WithDescription(
                "Returns a paginated list of invoices. " +
                "Supports ?page=1&pageSize=10&status=Draft&customerId={guid}&search=INV-001&sortBy=invoiceDate&sortOrder=desc.")
            .Produces<PagedResult<InvoiceSummaryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetInvoiceById)
            .RequirePermission(PermissionCodes.InvoiceView)
            .WithName("GetInvoiceById")
            .WithSummary("Get invoice by id")
            .WithDescription("Returns complete invoice details with payment amounts computed from order payments.")
            .Produces<InvoiceResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/pdf", GetInvoicePdf)
            .RequirePermission(PermissionCodes.InvoiceView)
            .WithName("GetInvoicePdf")
            .WithSummary("Get invoice PDF preview")
            .WithDescription("Returns a simple HTML invoice preview (placeholder for PDF generation).")
            .Produces<string>(StatusCodes.Status200OK, "text/html")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateInvoice)
            .RequirePermission(PermissionCodes.InvoiceUpdate)
            .WithName("UpdateInvoice")
            .WithSummary("Update an invoice")
            .WithDescription("Updates notes and due date. Only invoices in Draft status can be updated.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/{id:guid}/status", UpdateInvoiceStatus)
            .RequirePermission(PermissionCodes.InvoiceUpdate)
            .WithName("UpdateInvoiceStatus")
            .WithSummary("Update invoice status")
            .WithDescription(
                "Updates the invoice status. Valid transitions: " +
                "Draft → Sent/Cancelled, Sent → Paid/PartiallyPaid/Overdue/Cancelled, " +
                "PartiallyPaid → Paid/Overdue, Overdue → Paid/PartiallyPaid/Cancelled.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteInvoice)
            .RequirePermission(PermissionCodes.InvoiceDelete)
            .WithName("DeleteInvoice")
            .WithSummary("Delete an invoice")
            .WithDescription("Soft-deletes an invoice. Only Draft or Cancelled invoices can be deleted.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> CreateInvoiceFromOrder(
        Guid orderId,
        CreateInvoiceFromOrderRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var id = await sender.Send(
            new CreateInvoiceFromOrderCommand(orderId, request.DueDate, request.Notes),
            cancellationToken);

        return Results.Created($"/api/invoices/{id}", new { id });
    }

    private static async Task<IResult> GetAllInvoices(
        string? status,
        Guid? customerId,
        string? search,
        int page,
        int pageSize,
        string? sortBy,
        string? sortOrder,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetAllInvoicesQuery(
                status,
                customerId,
                search,
                page,
                pageSize,
                sortBy,
                QueryableSortingExtensions.ParseSortDirection(sortOrder)),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetInvoiceById(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetInvoiceByIdQuery(id), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetInvoicePdf(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var html = await sender.Send(new GetInvoicePdfQuery(id), cancellationToken);
        return Results.Content(html, "text/html");
    }

    private static async Task<IResult> UpdateInvoice(
        Guid id,
        UpdateInvoiceRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateInvoiceCommand(id, request.DueDate, request.Notes),
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> UpdateInvoiceStatus(
        Guid id,
        UpdateInvoiceStatusRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new UpdateInvoiceStatusCommand(id, request.Status), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteInvoice(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteInvoiceCommand(id), cancellationToken);
        return Results.NoContent();
    }
}
