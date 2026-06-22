using MediatR;
using BusinessOS.Application.Features.Categories.Queries;

namespace BusinessOS.Application.Features.Categories.Queries.GetAllCategories;

public record GetAllCategoriesQuery() : IRequest<List<CategoryDto>>;
