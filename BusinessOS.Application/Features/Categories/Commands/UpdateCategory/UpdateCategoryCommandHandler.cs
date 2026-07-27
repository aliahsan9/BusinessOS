using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Categories.Commands.UpdateCategory;

public class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;

    public UpdateCategoryCommandHandler(
        IApplicationDbContext context,
        ICacheService cache,
        ITenantProvider tenantProvider)
    {
        _context = context;
        _cache = cache;
        _tenantProvider = tenantProvider;
    }

    public async Task<Unit> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (category is null)
            throw new NotFoundException("Category not found");

        var duplicateExists = await _context.Categories
            .AnyAsync(x => x.Id != request.Id && x.Name == request.Name, cancellationToken);

        if (duplicateExists)
            throw new ConflictException($"A category named '{request.Name}' already exists.");

        category.Name = request.Name;
        category.Description = request.Description;

        await _context.SaveChangesAsync(cancellationToken);

        await EntityCacheInvalidator.InvalidateCategoryAsync(
            _cache,
            _tenantProvider.TenantId,
            category.Id,
            cancellationToken);

        return Unit.Value;
    }
}
