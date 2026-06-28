using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Quotations.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Quotations.Queries.GetAllQuotations;

public record GetAllQuotationsQuery(
    string? Status,
    Guid? CustomerId,
    string? Search,
    int Page,
    int PageSize,
    string? SortBy,
    SortDirection SortDirection
) : IRequest<PagedResult<QuotationSummaryResponse>>;
