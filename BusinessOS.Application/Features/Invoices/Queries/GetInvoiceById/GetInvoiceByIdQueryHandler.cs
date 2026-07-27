using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Invoices.Queries;
using BusinessOS.Application.Features.Invoices.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BusinessOS.Application.Features.Invoices.Queries.GetInvoiceById;

public sealed class GetInvoiceByIdQueryHandler : IRequestHandler<GetInvoiceByIdQuery, InvoiceResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly CacheSettings _cacheSettings;

    public GetInvoiceByIdQueryHandler(
        IApplicationDbContext context,
        ICacheService cache,
        ITenantProvider tenantProvider,
        IOptions<CacheSettings> cacheSettings)
    {
        _context = context;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _cacheSettings = cacheSettings.Value;
    }

    public async Task<InvoiceResponse> Handle(
        GetInvoiceByIdQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId;
        var key = CacheKeys.InvoiceById(tenantId, request.Id);

        return await _cache.GetOrSetAsync(
            key,
            async ct =>
            {
                var invoice = await _context.Invoices
                    .AsNoTracking()
                    .Where(x => x.Id == request.Id)
                    .Select(InvoiceProjections.ToDetail)
                    .FirstOrDefaultAsync(ct);

                if (invoice is null)
                    throw new NotFoundException("Invoice not found.");

                var amountPaidByOrderId = await InvoicePaymentCalculator.GetAmountPaidByOrderIdsAsync(
                    _context,
                    [invoice.OrderId],
                    ct);

                InvoicePaymentCalculator.ApplyPaymentAmounts(invoice, amountPaidByOrderId);

                return invoice;
            },
            absoluteExpiration: _cacheSettings.DefaultExpiration,
            cancellationToken: cancellationToken);
    }
}
