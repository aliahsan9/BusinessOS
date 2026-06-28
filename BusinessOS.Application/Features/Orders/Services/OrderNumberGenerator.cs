using BusinessOS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Orders.Services;

public sealed class OrderNumberGenerator : IOrderNumberGenerator
{
    private readonly IApplicationDbContext _context;

    public OrderNumberGenerator(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateNextAsync(CancellationToken cancellationToken = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"ORD-{year}-";

        var lastOrderNumber = await _context.Orders
            .AsNoTracking()
            .Where(o => o.OrderNumber.StartsWith(prefix))
            .OrderByDescending(o => o.OrderNumber)
            .Select(o => o.OrderNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var nextSequence = 1;

        if (lastOrderNumber is not null && lastOrderNumber.Length > prefix.Length)
        {
            var suffix = lastOrderNumber[prefix.Length..];
            if (int.TryParse(suffix, out var sequence))
            {
                nextSequence = sequence + 1;
            }
        }

        return $"{prefix}{nextSequence:D6}";
    }
}
