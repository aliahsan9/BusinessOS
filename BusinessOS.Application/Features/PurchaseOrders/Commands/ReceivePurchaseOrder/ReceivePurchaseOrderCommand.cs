using MediatR;

namespace BusinessOS.Application.Features.PurchaseOrders.Commands.ReceivePurchaseOrder;

public record ReceivePurchaseOrderLineDto(
    Guid PurchaseItemId,
    decimal ReceivedQuantity,
    decimal DamagedQuantity = 0,
    string? DiscrepancyNotes = null);

/// <summary>
/// Verified receiving. When <see cref="Lines"/> is null, remaining quantities on all lines are received in full.
/// Stock only increases from verified quantities — never from a supplier delivery claim alone.
/// </summary>
public record ReceivePurchaseOrderCommand(
    Guid Id,
    IReadOnlyList<ReceivePurchaseOrderLineDto>? Lines = null,
    string? Notes = null) : IRequest<Unit>;
