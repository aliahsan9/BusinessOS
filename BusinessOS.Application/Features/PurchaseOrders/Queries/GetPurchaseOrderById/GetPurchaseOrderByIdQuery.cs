using BusinessOS.Application.Features.PurchaseOrders.Queries;
using MediatR;

namespace BusinessOS.Application.Features.PurchaseOrders.Queries.GetPurchaseOrderById;

public record GetPurchaseOrderByIdQuery(Guid Id) : IRequest<PurchaseOrderResponse>;
