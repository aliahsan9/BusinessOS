using BusinessOS.Application.Features.PurchaseOrders.Queries;
using MediatR;

namespace BusinessOS.Application.Features.PurchaseOrders.Commands.CreatePurchaseOrder;

public record CreatePurchaseOrderCommand(
    Guid SupplierId,
    DateTime PurchaseDate,
    string Status,
    string? ReferenceNumber,
    string? Notes,
    IReadOnlyList<CreatePurchaseOrderLineItemDto> Items
) : IRequest<Guid>;
