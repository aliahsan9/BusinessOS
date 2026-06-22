using MediatR;
using BusinessOS.Application.Features.Categories.Queries;

namespace BusinessOS.Application.Features.Categories.Queries.GetCategoryById;

public record GetCategoryByIdQuery(Guid Id) : IRequest<CategoryDto?>;
