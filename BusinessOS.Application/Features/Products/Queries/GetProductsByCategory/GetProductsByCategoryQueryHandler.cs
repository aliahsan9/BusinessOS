using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Products.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Products.Queries.GetProductsByCategory;

public sealed class GetProductsByCategoryQueryHandler
    : IRequestHandler<GetProductsByCategoryQuery, PagedResult<ProductDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetProductsByCategoryQueryHandler> _logger;
    private readonly ISender _sender;

    public GetProductsByCategoryQueryHandler(
        IApplicationDbContext context,
        ILogger<GetProductsByCategoryQueryHandler> logger,
        ISender sender)
    {
        _context = context;
        _logger = logger;
        _sender = sender;
    }

    public async Task<PagedResult<ProductDto>> Handle(
        GetProductsByCategoryQuery request,
        CancellationToken cancellationToken)
    {
        var categoryExists = await _context.Categories
            .AnyAsync(x => x.Id == request.CategoryId, cancellationToken);

        if (!categoryExists)
        {
            _logger.LogWarning("Category {CategoryId} not found for product listing", request.CategoryId);
            throw new NotFoundException("Category not found");
        }

        return await _sender.Send(
            new Queries.GetAllProducts.GetAllProductsQuery(
                CategoryId: request.CategoryId,
                Search: request.Search,
                IsActive: null,
                Page: request.Page,
                PageSize: request.PageSize,
                SortBy: request.SortBy,
                SortDirection: request.SortDirection),
            cancellationToken);
    }
}
