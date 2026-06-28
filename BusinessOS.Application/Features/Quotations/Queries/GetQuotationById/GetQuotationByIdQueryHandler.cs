using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Quotations.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Quotations.Queries.GetQuotationById;

public sealed class GetQuotationByIdQueryHandler
    : IRequestHandler<GetQuotationByIdQuery, QuotationResponse>
{
    private readonly IApplicationDbContext _context;

    public GetQuotationByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<QuotationResponse> Handle(
        GetQuotationByIdQuery request,
        CancellationToken cancellationToken)
    {
        var quotation = await _context.Quotations
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
            .Select(QuotationProjections.ToDetail)
            .FirstOrDefaultAsync(cancellationToken);

        if (quotation is null)
            throw new NotFoundException("Quotation not found.");

        return quotation;
    }
}
