using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Products.Commands.UpdateProduct;

public sealed class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public UpdateProductCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(
        UpdateProductCommand request,
        CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (product is null)
            throw new NotFoundException("Product not found");

        var categoryExists = await _context.Categories
            .AnyAsync(x => x.Id == request.CategoryId, cancellationToken);

        if (!categoryExists)
            throw new BadRequestException("Invalid CategoryId. Category does not exist.");

        product.CategoryId = request.CategoryId;
        product.Name = request.Name;
        product.SKU = request.SKU;
        product.Description = request.Description;
        product.CostPrice = request.CostPrice;
        product.SalePrice = request.SalePrice;
        product.ReorderLevel = request.ReorderLevel;
        product.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
