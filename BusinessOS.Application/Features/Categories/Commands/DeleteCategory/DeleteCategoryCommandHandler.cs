using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Categories.Commands.DeleteCategory;

public class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public DeleteCategoryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (category is null)
            throw new NotFoundException("Category not found");

        var hasProducts = await _context.Products
            .AnyAsync(x => x.CategoryId == request.Id, cancellationToken);

        if (hasProducts)
            throw new ConflictException("Cannot delete category that has associated products.");

        _context.Categories.Remove(category);

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
