using MediatR;

namespace BusinessOS.Application.Features.Categories.Commands.UpdateCategory;

public record UpdateCategoryCommand(
    Guid Id,
    string Name,
    string? Description
) : IRequest<Unit>;
