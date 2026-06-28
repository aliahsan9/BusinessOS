using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Suppliers.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Suppliers.Queries.GetAllSuppliers;

public sealed class GetAllSuppliersQueryHandler
    : IRequestHandler<GetAllSuppliersQuery, PagedResult<SupplierSummaryResponse>>
{
    private static readonly Dictionary<string, System.Linq.Expressions.Expression<Func<SupplierSummaryResponse, object>>> SortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = x => x.Name,
            ["email"] = x => x.Email,
            ["phone"] = x => x.Phone,
            ["address"] = x => x.Address,
            ["contactperson"] = x => x.ContactPerson!,
            ["createdat"] = x => x.CreatedAt
        };

    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetAllSuppliersQueryHandler> _logger;

    public GetAllSuppliersQueryHandler(
        IApplicationDbContext context,
        ILogger<GetAllSuppliersQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<SupplierSummaryResponse>> Handle(
        GetAllSuppliersQuery request,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = PaginationParams.Normalize(request.Page, request.PageSize);

        var query = _context.Suppliers
            .AsNoTracking()
            .Select(SupplierProjections.ToSummary);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x =>
                x.Name.Contains(search) ||
                x.Email.Contains(search) ||
                x.Phone.Contains(search) ||
                (x.ContactPerson != null && x.ContactPerson.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .ApplySort(
                request.SortBy,
                request.SortDirection,
                SortFields,
                x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Retrieved {Count} suppliers (page {Page}, total {Total})",
            items.Count,
            page,
            totalCount);

        return new PagedResult<SupplierSummaryResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
