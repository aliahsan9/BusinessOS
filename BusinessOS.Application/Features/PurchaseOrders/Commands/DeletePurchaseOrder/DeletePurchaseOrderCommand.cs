using MediatR;

namespace BusinessOS.Application.Features.PurchaseOrders.Commands.DeletePurchaseOrder;

public record DeletePurchaseOrderCommand(Guid Id) : IRequest<Unit>;
