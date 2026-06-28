using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Payments.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Payments.Queries.GetAllPayments;

public record GetAllPaymentsQuery(
    Guid? CustomerId,
    Guid? OrderId,
    string? PaymentMethod,
    DateTime? DateFrom,
    DateTime? DateTo,
    int Page,
    int PageSize,
    string? SortBy,
    SortDirection SortDirection
) : IRequest<PagedResult<PaymentSummaryResponse>>;
