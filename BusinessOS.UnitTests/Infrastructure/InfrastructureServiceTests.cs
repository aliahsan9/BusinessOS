using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Roles.DTOs;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Repositories;
using BusinessOS.Infrastructure.Services;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Security.Claims;

namespace BusinessOS.UnitTests.Infrastructure;

public class RepositoryTests
{
    [Fact]
    public async Task PermissionRepository_GetPermissions_ReturnsOrderedPermissions()
    {
        var (context, _, _) = InMemoryDbContextFactory.Create();
        context.Permissions.AddRange(
            TestDataFactory.CreatePermission(PermissionCodes.OrderView, "View Orders", "Orders"),
            TestDataFactory.CreatePermission(PermissionCodes.ProductView, "View Products", "Products"));
        await context.SaveChangesAsync();

        var repository = new PermissionRepository(context);
        var permissions = await repository.GetPermissionsAsync();

        permissions.Should().HaveCount(2);
        permissions[0].Category.Should().Be("Orders");
    }

    [Fact]
    public async Task PermissionRepository_GetPermissionByCode_ReturnsMatch()
    {
        var (context, _, _) = InMemoryDbContextFactory.Create();
        var permission = TestDataFactory.CreatePermission(PermissionCodes.ProductCreate);
        context.Permissions.Add(permission);
        await context.SaveChangesAsync();

        var repository = new PermissionRepository(context);
        var result = await repository.GetPermissionByCodeAsync(PermissionCodes.ProductCreate);

        result.Should().NotBeNull();
        result!.Id.Should().Be(permission.Id);
    }

    [Fact]
    public async Task RoleRepository_AssignPermission_IsIdempotent()
    {
        var (context, _, _) = InMemoryDbContextFactory.Create();
        var role = TestDataFactory.CreateRole("Manager");
        var permission = TestDataFactory.CreatePermission(PermissionCodes.ProductView);
        context.RbacRoles.Add(role);
        context.Permissions.Add(permission);
        await context.SaveChangesAsync();

        var repository = new RoleRepository(context);
        await repository.AssignPermissionAsync(role.Id, permission.Id);
        await repository.AssignPermissionAsync(role.Id, permission.Id);

        context.RolePermissions.Count(x => x.RoleId == role.Id).Should().Be(1);
    }

    [Fact]
    public async Task RoleRepository_GetUserPermissionCodes_ReturnsDistinctCodes()
    {
        var (context, _, _) = InMemoryDbContextFactory.Create();
        var role = TestDataFactory.CreateRole(RoleNames.Admin);
        var permission = TestDataFactory.CreatePermission(PermissionCodes.ProductView);
        context.RbacRoles.Add(role);
        context.Permissions.Add(permission);
        context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        context.RbacUserRoles.Add(new UserRole { UserId = "user-1", RoleId = role.Id, Role = role });
        await context.SaveChangesAsync();

        var repository = new RoleRepository(context);
        var codes = await repository.GetUserPermissionCodesAsync("user-1");

        codes.Should().Contain(PermissionCodes.ProductView);
    }

    [Fact]
    public async Task RoleRepository_RemovePermission_RemovesAssignment()
    {
        var (context, _, _) = InMemoryDbContextFactory.Create();
        var role = TestDataFactory.CreateRole("Editor");
        var permission = TestDataFactory.CreatePermission(PermissionCodes.CategoryView);
        context.RbacRoles.Add(role);
        context.Permissions.Add(permission);
        context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        await context.SaveChangesAsync();

        var repository = new RoleRepository(context);
        await repository.RemovePermissionAsync(role.Id, permission.Id);

        context.RolePermissions.Should().BeEmpty();
    }

    [Fact]
    public async Task RoleRepository_CreateAndGetRole_Works()
    {
        var (context, _, _) = InMemoryDbContextFactory.Create();
        var repository = new RoleRepository(context);
        var role = TestDataFactory.CreateRole("Operations");

        var created = await repository.CreateRoleAsync(role);
        var loaded = await repository.GetRoleByIdAsync(created.Id);

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Operations");
    }

    [Fact]
    public async Task InventoryRepository_GetLowStockAndOutOfStock_FiltersCorrectly()
    {
        var (context, tenantId) = await TestDataFactory.CreateCatalogContextAsync(productStock: 0, reorderLevel: 5);
        var category = context.Categories.First();
        var lowStockProduct = TestDataFactory.CreateProduct(tenantId, category.Id, "Low", "SKU-LOW", stock: 3, reorderLevel: 10);
        context.Products.Add(lowStockProduct);
        context.Inventories.Add(TestDataFactory.CreateInventoryWithProduct(tenantId, lowStockProduct, 3, 10));
        await context.SaveChangesAsync();

        var repository = new InventoryRepository(context);

        (await repository.GetOutOfStockAsync()).Should().NotBeEmpty();
        (await repository.GetLowStockAsync()).Should().NotBeEmpty();
        (await repository.GetReorderProductsAsync()).Should().HaveCountGreaterThan(1);
    }
}

public class InfrastructureServiceTests
{
    [Fact]
    public void CurrentUserService_ReadsClaimsFromHttpContext()
    {
        var tenantId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(ClaimTypes.Email, "admin@test.com"),
            new Claim("TenantId", tenantId.ToString()),
            new Claim(ClaimTypes.Role, RoleNames.Admin),
            new Claim(ClaimTypesConstants.Permissions, "Product.View,Order.Create")
        };

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
        };

        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(x => x.HttpContext).Returns(context);

        var service = new CurrentUserService(accessor.Object);

        service.UserId.Should().Be("user-1");
        service.Email.Should().Be("admin@test.com");
        service.TenantId.Should().Be(tenantId);
        service.Roles.Should().Contain(RoleNames.Admin);
        service.Permissions.Should().Contain(PermissionCodes.ProductView);
        service.HasPermission(PermissionCodes.OrderCreate).Should().BeTrue();
    }

    [Fact]
    public void CurrentUserService_WithoutHttpContext_ReturnsEmptyValues()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        var service = new CurrentUserService(accessor.Object);

        service.UserId.Should().BeNull();
        service.Roles.Should().BeEmpty();
        service.Permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task PermissionService_GetPermissionById_ThrowsWhenMissing()
    {
        var repository = new Mock<IPermissionRepository>();
        repository
            .Setup(x => x.GetPermissionByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Permission?)null);

        var service = new PermissionService(repository.Object);
        var act = () => service.GetPermissionByIdAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task PermissionService_GetPermissions_MapsDtos()
    {
        var permission = TestDataFactory.CreatePermission(PermissionCodes.InventoryView);
        var repository = new Mock<IPermissionRepository>();
        repository
            .Setup(x => x.GetPermissionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Permission> { permission });

        var service = new PermissionService(repository.Object);
        var result = await service.GetPermissionsAsync();

        result.Should().ContainSingle(x => x.Code == PermissionCodes.InventoryView);
    }

    [Fact]
    public void JwtTokenGenerator_GetTokenExpiration_UsesConfiguredMinutes()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "super-secret-development-key-1234567890",
                ["Jwt:Issuer"] = "BusinessOS",
                ["Jwt:Audience"] = "BusinessOS",
                ["Jwt:ExpiryMinutes"] = "30"
            })
            .Build();

        var generator = new JwtTokenGenerator(configuration);
        var expiration = generator.GetTokenExpiration();

        expiration.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(30), TimeSpan.FromSeconds(5));
    }
}
