using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Categories.Commands.CreateCategory;

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateCategoryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var duplicateExists = await _context.Categories
            .AnyAsync(x => x.Name == request.Name, cancellationToken);

        if (duplicateExists)
            throw new ConflictException($"A category named '{request.Name}' already exists.");

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description
        };

        _context.Categories.Add(category);

        await _context.SaveChangesAsync(cancellationToken);

        return category.Id;
    }
}
