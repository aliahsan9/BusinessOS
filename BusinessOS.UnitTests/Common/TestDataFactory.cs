using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;

namespace BusinessOS.UnitTests.Common;

internal static class TestDataFactory
{
    public static Category CreateCategory(Guid tenantId, string name = "Test Category") =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Description = $"{name} description",
            CreatedAt = DateTime.UtcNow
        };

    public static Product CreateProduct(
        Guid tenantId,
        Guid categoryId,
        string name = "Test Product",
        string sku = "SKU-001",
        decimal costPrice = 10,
        decimal salePrice = 20,
        decimal stock = 100,
        decimal reorderLevel = 5) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CategoryId = categoryId,
            Name = name,
            SKU = sku,
            CostPrice = costPrice,
            SalePrice = salePrice,
            CurrentStock = stock,
            ReorderLevel = reorderLevel,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

    public static Inventory CreateInventory(
        Guid tenantId,
        Guid productId,
        decimal stock,
        decimal reorderLevel = 5) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            CurrentStock = stock,
            ReorderLevel = reorderLevel,
            MinimumStockLevel = 0,
            MaximumStockLevel = 100,
            LastUpdated = DateTime.UtcNow
        };

    public static Inventory CreateInventoryWithProduct(
        Guid tenantId,
        Product product,
        decimal stock,
        decimal reorderLevel = 5) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = product.Id,
            CurrentStock = stock,
            ReorderLevel = reorderLevel,
            MinimumStockLevel = 0,
            MaximumStockLevel = 100,
            LastUpdated = DateTime.UtcNow,
            Product = product
        };

    public static Customer CreateCustomer(
        Guid tenantId,
        string email = "customer@test.com",
        string firstName = "Ali",
        string lastName = "Ahsan") =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PhoneNumber = "1234567890",
            Address = "123 Main St",
            City = "Lahore",
            Country = "Pakistan",
            PostalCode = "54000",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

    public static Order CreateOrder(
        Guid tenantId,
        Guid customerId,
        string status = OrderStatusNames.Pending,
        decimal grandTotal = 100,
        string orderNumber = "ORD-2026-000001") =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerId = customerId,
            OrderNumber = orderNumber,
            OrderDate = DateTime.UtcNow,
            Status = status,
            GrandTotal = grandTotal,
            CreatedAt = DateTime.UtcNow
        };

    public static OrderItem CreateOrderItem(
        Guid orderId,
        Guid productId,
        decimal quantity = 1,
        decimal unitPrice = 20) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = productId,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Total = quantity * unitPrice
        };

    public static Permission CreatePermission(
        string code = "Product.View",
        string name = "View Products",
        string category = "Products") =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Code = code,
            Description = name,
            Category = category
        };

    public static Role CreateRole(string name = "CustomRole") =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = $"{name} role",
            IsActive = true
        };

    public static StockTransaction CreateStockTransaction(
        Guid tenantId,
        Guid productId,
        string type = StockTransactionTypeNames.Purchase,
        decimal quantity = 10,
        decimal previousStock = 0,
        decimal newStock = 10,
        Product? product = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            TransactionType = type,
            Quantity = quantity,
            PreviousStock = previousStock,
            NewStock = newStock,
            CreatedAt = DateTime.UtcNow,
            Product = product
        };

    public static async Task<(BusinessOSDbContext Context, Guid TenantId)> CreateCatalogContextAsync(
        decimal productStock = 10,
        decimal reorderLevel = 5)
    {
        var (context, tenantId, _) = InMemoryDbContextFactory.Create();
        var category = CreateCategory(tenantId);
        var product = CreateProduct(tenantId, category.Id, stock: productStock, reorderLevel: reorderLevel);
        var inventory = CreateInventoryWithProduct(tenantId, product, productStock, reorderLevel);

        context.Categories.Add(category);
        context.Products.Add(product);
        context.Inventories.Add(inventory);
        await context.SaveChangesAsync();

        return (context, tenantId);
    }
}
