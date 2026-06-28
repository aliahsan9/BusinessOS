using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Data.Configurations;
using BusinessOS.Infrastructure.MultiTenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BusinessOS.UnitTests.Infrastructure;

public class DatabaseConfigurationTests
{
    [Fact]
    public void ProductConfiguration_DefinesUniqueSkuPerTenant()
    {
        var index = GetIndex<Product>(nameof(Product.SKU));

        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
        index.Properties.Select(x => x.Name).Should().Contain(nameof(Product.TenantId));
        index.Properties.Select(x => x.Name).Should().Contain(nameof(Product.SKU));
    }

    [Fact]
    public void CustomerConfiguration_DefinesUniqueEmailPerTenant()
    {
        var index = GetIndex<Customer>(nameof(Customer.Email));

        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
        index.Properties.Select(x => x.Name).Should().Contain(nameof(Customer.TenantId));
        index.Properties.Select(x => x.Name).Should().Contain(nameof(Customer.Email));
    }

    [Fact]
    public void CategoryConfiguration_DefinesUniqueNamePerTenantWithSoftDeleteFilter()
    {
        var index = GetIndex<Category>(nameof(Category.Name));

        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
        index.GetFilter().Should().Contain("IsDeleted");
    }

    [Fact]
    public void OrderConfiguration_RestrictsCustomerDelete()
    {
        var foreignKey = GetForeignKey<Order>(nameof(Order.CustomerId));

        foreignKey.Should().NotBeNull();
        foreignKey!.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
        foreignKey.PrincipalEntityType.ClrType.Should().Be(typeof(Customer));
    }

    [Fact]
    public void CategoryConfiguration_RestrictsDeleteWhenProductsExist()
    {
        var foreignKey = GetForeignKey<Product>(nameof(Product.CategoryId));

        foreignKey.Should().NotBeNull();
        foreignKey!.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
    }

    private static IIndex? GetIndex<TEntity>(string propertyName)
    {
        var entityType = CreateModel().FindEntityType(typeof(TEntity));
        return entityType?.GetIndexes()
            .FirstOrDefault(x => x.Properties.Any(p => p.Name == propertyName));
    }

    private static IForeignKey? GetForeignKey<TEntity>(string propertyName)
    {
        var entityType = CreateModel().FindEntityType(typeof(TEntity));
        return entityType?.GetForeignKeys()
            .FirstOrDefault(x => x.Properties.Any(p => p.Name == propertyName));
    }

    private static IModel CreateModel()
    {
        var tenantProvider = new TenantProvider();
        var options = new DbContextOptionsBuilder<BusinessOSDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new BusinessOSDbContext(options, tenantProvider);
        return context.Model;
    }
}
