using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Invoices.Queries.GetInvoiceSummary;

public sealed record GetInvoiceSummaryQuery : IRequest<InvoicePortfolioSummaryResponse>;

public sealed class InvoicePortfolioSummaryResponse
{
    public int Total { get; init; }
    public int Paid { get; init; }
    public int Outstanding { get; init; }
    public int Overdue { get; init; }
    public decimal TotalOutstandingAmount { get; init; }
}

public sealed class GetInvoiceSummaryQueryHandler
    : IRequestHandler<GetInvoiceSummaryQuery, InvoicePortfolioSummaryResponse>
{
    private readonly IApplicationDbContext _context;

    public GetInvoiceSummaryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<InvoicePortfolioSummaryResponse> Handle(
        GetInvoiceSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var invoices = _context.Invoices.AsNoTracking();

        var total = await invoices.CountAsync(cancellationToken);
        var paid = await invoices.CountAsync(
            invoice => invoice.Status == InvoiceStatusNames.Paid,
            cancellationToken);
        var outstandingCount = await invoices.CountAsync(
            invoice => invoice.OutstandingAmount > 0
                && invoice.Status != InvoiceStatusNames.Cancelled
                && invoice.Status != InvoiceStatusNames.Draft,
            cancellationToken);
        var overdue = await invoices.CountAsync(
            invoice => invoice.OutstandingAmount > 0
                && invoice.DueDate < now
                && invoice.Status != InvoiceStatusNames.Paid
                && invoice.Status != InvoiceStatusNames.Cancelled
                && invoice.Status != InvoiceStatusNames.Draft,
            cancellationToken);
        var outstandingAmount = await invoices
            .Where(invoice => invoice.OutstandingAmount > 0
                && invoice.Status != InvoiceStatusNames.Cancelled
                && invoice.Status != InvoiceStatusNames.Draft)
            .SumAsync(invoice => (decimal?)invoice.OutstandingAmount, cancellationToken) ?? 0m;

        return new InvoicePortfolioSummaryResponse
        {
            Total = total,
            Paid = paid,
            Outstanding = outstandingCount,
            Overdue = overdue,
            TotalOutstandingAmount = Math.Round(outstandingAmount, 2, MidpointRounding.AwayFromZero)
        };
    }
}
