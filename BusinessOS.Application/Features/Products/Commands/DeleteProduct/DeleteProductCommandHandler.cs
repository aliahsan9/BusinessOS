using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Products.Commands.DeleteProduct;

public sealed class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<DeleteProductCommandHandler> _logger;

    public DeleteProductCommandHandler(
        IApplicationDbContext context,
        ICacheService cache,
        ITenantProvider tenantProvider,
        ILogger<DeleteProductCommandHandler> logger)
    {
        _context = context;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<Unit> Handle(
        DeleteProductCommand request,
        CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (product is null)
            throw new NotFoundException("Product not found");

        var productName = product.Name;
        var productId = product.Id;

        var hasOrderHistory = await _context.OrderItems
            .AnyAsync(x => x.ProductId == productId, cancellationToken);
        var hasStockHistory = await _context.StockTransactions
            .AnyAsync(x => x.ProductId == productId, cancellationToken);

        if (hasOrderHistory || hasStockHistory)
        {
            // Keep the row for historical joins; deactivate instead of soft-deleting.
            product.IsActive = false;
            await _context.SaveChangesAsync(cancellationToken);

            await EntityCacheInvalidator.InvalidateProductAsync(
                _cache,
                _tenantProvider.TenantId,
                productId,
                cancellationToken);

            _logger.LogInformation(
                "Product {ProductName} ({ProductId}) deactivated because it has order or stock history",
                productName,
                productId);

            return Unit.Value;
        }

        // Free the tenant SKU unique index so the SKU can be reused after soft-delete.
        product.IsActive = false;
        var suffix = $"__deleted__{productId:N}";
        var maxBaseLength = Math.Max(0, 100 - suffix.Length);
        var baseSku = product.SKU.Length > maxBaseLength
            ? product.SKU[..maxBaseLength]
            : product.SKU;
        product.SKU = baseSku + suffix;
        _context.Products.Remove(product);
        await _context.SaveChangesAsync(cancellationToken);

        await EntityCacheInvalidator.InvalidateProductAsync(
            _cache,
            _tenantProvider.TenantId,
            productId,
            cancellationToken);

        _logger.LogInformation(
            "Product {ProductName} ({ProductId}) soft-deleted",
            productName,
            productId);

        return Unit.Value;
    }
}
