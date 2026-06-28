using BusinessOS.Application.Features.Quotations.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Quotations.Commands.UpdateQuotation;

public record UpdateQuotationCommand(
    Guid Id,
    Guid CustomerId,
    DateTime QuotationDate,
    DateTime ExpiryDate,
    string Status,
    decimal Discount,
    decimal Tax,
    string? Notes,
    IReadOnlyList<QuotationLineItemDto> Items
) : IRequest<Unit>;
