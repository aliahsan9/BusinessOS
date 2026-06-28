using MediatR;

namespace BusinessOS.Application.Features.PurchaseOrders.Commands.ReceivePurchaseOrder;

public record ReceivePurchaseOrderCommand(Guid Id) : IRequest<Unit>;
