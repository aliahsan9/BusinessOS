using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Categories.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Categories.Queries.GetAllCategories;

public sealed class GetAllCategoriesQueryHandler
    : IRequestHandler<GetAllCategoriesQuery, PagedResult<CategoryDto>>
{
    private static readonly Dictionary<string, System.Linq.Expressions.Expression<Func<CategoryDto, object>>> SortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = x => x.Name,
            ["description"] = x => x.Description!
        };

    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetAllCategoriesQueryHandler> _logger;

    public GetAllCategoriesQueryHandler(
        IApplicationDbContext context,
        ILogger<GetAllCategoriesQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<CategoryDto>> Handle(
        GetAllCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = PaginationParams.Normalize(request.Page, request.PageSize);

        var query = _context.Categories
            .AsNoTracking()
            .Select(x => new CategoryDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description
            });

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x =>
                x.Name.Contains(search) ||
                (x.Description != null && x.Description.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .ApplySort(
                request.SortBy,
                request.SortDirection,
                SortFields,
                x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Retrieved {Count} categories (page {Page}, total {Total})",
            items.Count,
            page,
            totalCount);

        return new PagedResult<CategoryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
