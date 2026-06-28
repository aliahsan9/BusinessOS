using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Categories.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Categories.Queries.GetAllCategories;

public sealed record GetAllCategoriesQuery(
    string? Search = null,
    int Page = PaginationParams.DefaultPage,
    int PageSize = PaginationParams.DefaultPageSize,
    string? SortBy = null,
    SortDirection SortDirection = SortDirection.Asc) : IRequest<PagedResult<CategoryDto>>;
