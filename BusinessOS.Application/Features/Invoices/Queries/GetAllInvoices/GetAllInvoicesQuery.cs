using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Invoices.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Invoices.Queries.GetAllInvoices;

public record GetAllInvoicesQuery(
    string? Status,
    Guid? CustomerId,
    string? Search,
    int Page,
    int PageSize,
    string? SortBy,
    SortDirection SortDirection
) : IRequest<PagedResult<InvoiceSummaryResponse>>;
