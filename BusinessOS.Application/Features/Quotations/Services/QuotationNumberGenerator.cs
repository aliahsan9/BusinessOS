using BusinessOS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Quotations.Services;

public sealed class QuotationNumberGenerator : IQuotationNumberGenerator
{
    private readonly IApplicationDbContext _context;

    public QuotationNumberGenerator(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateNextAsync(CancellationToken cancellationToken = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"QUO-{year}-";

        var lastQuotationNumber = await _context.Quotations
            .AsNoTracking()
            .Where(q => q.QuotationNumber.StartsWith(prefix))
            .OrderByDescending(q => q.QuotationNumber)
            .Select(q => q.QuotationNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var nextSequence = 1;

        if (lastQuotationNumber is not null && lastQuotationNumber.Length > prefix.Length)
        {
            var suffix = lastQuotationNumber[prefix.Length..];
            if (int.TryParse(suffix, out var sequence))
            {
                nextSequence = sequence + 1;
            }
        }

        return $"{prefix}{nextSequence:D6}";
    }
}
