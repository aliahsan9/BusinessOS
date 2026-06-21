using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Domain.Entities;
using MediatR;

namespace BusinessOS.Application.Products.Commands.CreateProduct;

public class CreateProductCommandHandler
    : IRequestHandler<CreateProductCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public CreateProductCommandHandler(
        IApplicationDbContext db,
        ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    public async Task<Guid> Handle(
    CreateProductCommand request,
    CancellationToken cancellationToken)
    {
        var product = new Product
        {
            TenantId = _tenantProvider.GetTenantId(),
            CategoryId = request.CategoryId,

            Name = request.Name,
            SKU = request.SKU,
            Description = request.Description,

            CostPrice = request.CostPrice,
            SalePrice = request.SalePrice,

            CurrentStock = 0,
            ReorderLevel = request.ReorderLevel,

            IsActive = true
        };

        _db.Products.Add(product);

        await _db.SaveChangesAsync(cancellationToken);

        return product.Id;
    }
}
