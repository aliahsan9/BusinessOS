using MediatR;

namespace BusinessOS.Application.Features.PurchaseOrders.Commands.UpdatePurchaseOrderStatus;

public record UpdatePurchaseOrderStatusCommand(Guid Id, string Status) : IRequest<Unit>;
