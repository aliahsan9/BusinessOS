using BusinessOS.Application.Features.PurchaseOrders.Queries;
using MediatR;

namespace BusinessOS.Application.Features.PurchaseOrders.Commands.UpdatePurchaseOrder;

public record UpdatePurchaseOrderCommand(
    Guid Id,
    Guid SupplierId,
    DateTime PurchaseDate,
    string Status,
    string? ReferenceNumber,
    string? Notes,
    IReadOnlyList<CreatePurchaseOrderLineItemDto> Items
) : IRequest<Unit>;
