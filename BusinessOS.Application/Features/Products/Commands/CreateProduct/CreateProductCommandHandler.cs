using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Inventory.Services;
using BusinessOS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Products.Commands.CreateProduct;

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IInventoryService _inventoryService;

    public CreateProductCommandHandler(
        IApplicationDbContext context,
        IInventoryService inventoryService)
    {
        _context = context;
        _inventoryService = inventoryService;
    }

    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var categoryExists = await _context.Categories
            .AnyAsync(x => x.Id == request.CategoryId, cancellationToken);

        if (!categoryExists)
            throw new BadRequestException("Invalid CategoryId. Category does not exist.");

        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = request.CategoryId,
            Name = request.Name,
            SKU = request.SKU,
            Description = request.Description,
            CostPrice = request.CostPrice,
            SalePrice = request.SalePrice,
            ReorderLevel = request.ReorderLevel
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);

        await _inventoryService.CreateInventoryForProductAsync(product, cancellationToken);

        return product.Id;
    }
}
