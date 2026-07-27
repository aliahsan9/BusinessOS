using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Categories.Commands.CreateCategory;

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<CreateCategoryCommandHandler> _logger;

    public CreateCategoryCommandHandler(
        IApplicationDbContext context,
        ICacheService cache,
        ITenantProvider tenantProvider,
        ILogger<CreateCategoryCommandHandler> logger)
    {
        _context = context;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _logger = logger;
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

        await EntityCacheInvalidator.InvalidateCategoryAsync(
            _cache,
            _tenantProvider.TenantId,
            category.Id,
            cancellationToken);

        _logger.LogInformation(
            "Category {CategoryName} ({CategoryId}) created",
            category.Name,
            category.Id);

        return category.Id;
    }
}
