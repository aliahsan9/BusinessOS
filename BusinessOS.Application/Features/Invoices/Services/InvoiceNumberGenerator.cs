using BusinessOS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Invoices.Services;

public sealed class InvoiceNumberGenerator : IInvoiceNumberGenerator
{
    private readonly IApplicationDbContext _context;

    public InvoiceNumberGenerator(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateNextAsync(CancellationToken cancellationToken = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"INV-{year}-";

        var lastInvoiceNumber = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(i => i.InvoiceNumber)
            .Select(i => i.InvoiceNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var nextSequence = 1;

        if (lastInvoiceNumber is not null && lastInvoiceNumber.Length > prefix.Length)
        {
            var suffix = lastInvoiceNumber[prefix.Length..];
            if (int.TryParse(suffix, out var sequence))
            {
                nextSequence = sequence + 1;
            }
        }

        return $"{prefix}{nextSequence:D6}";
    }
}
