using BusinessOS.Application.Features.Quotations.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Quotations.Commands.CreateQuotation;

public record CreateQuotationCommand(
    Guid CustomerId,
    DateTime QuotationDate,
    DateTime ExpiryDate,
    string Status,
    decimal Discount,
    decimal Tax,
    string? Notes,
    IReadOnlyList<QuotationLineItemDto> Items
) : IRequest<Guid>;
