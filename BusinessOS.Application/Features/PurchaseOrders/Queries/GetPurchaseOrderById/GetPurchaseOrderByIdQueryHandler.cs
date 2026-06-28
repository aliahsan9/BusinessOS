using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.PurchaseOrders.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.PurchaseOrders.Queries.GetPurchaseOrderById;

public sealed class GetPurchaseOrderByIdQueryHandler
    : IRequestHandler<GetPurchaseOrderByIdQuery, PurchaseOrderResponse>
{
    private readonly IApplicationDbContext _context;

    public GetPurchaseOrderByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PurchaseOrderResponse> Handle(
        GetPurchaseOrderByIdQuery request,
        CancellationToken cancellationToken)
    {
        var purchase = await _context.Purchases
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
            .Select(PurchaseOrderProjections.ToDetail)
            .FirstOrDefaultAsync(cancellationToken);

        if (purchase is null)
            throw new NotFoundException("Purchase order not found.");

        return purchase;
    }
}
