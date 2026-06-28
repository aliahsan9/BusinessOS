using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Invoices.Queries;
using BusinessOS.Application.Features.Invoices.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Invoices.Queries.GetInvoiceById;

public sealed class GetInvoiceByIdQueryHandler : IRequestHandler<GetInvoiceByIdQuery, InvoiceResponse>
{
    private readonly IApplicationDbContext _context;

    public GetInvoiceByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<InvoiceResponse> Handle(
        GetInvoiceByIdQuery request,
        CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
            .Select(InvoiceProjections.ToDetail)
            .FirstOrDefaultAsync(cancellationToken);

        if (invoice is null)
            throw new NotFoundException("Invoice not found.");

        var amountPaidByOrderId = await InvoicePaymentCalculator.GetAmountPaidByOrderIdsAsync(
            _context,
            [invoice.OrderId],
            cancellationToken);

        InvoicePaymentCalculator.ApplyPaymentAmounts(invoice, amountPaidByOrderId);

        return invoice;
    }
}
