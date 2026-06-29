using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Auth.Services;
using BusinessOS.Application.Features.Categories.Commands.CreateCategory;
using BusinessOS.Application.Features.Inventory.Services;
using BusinessOS.Application.Features.Products.Commands.CreateProduct;
using BusinessOS.Domain.Entities;
using BusinessOS.UnitTests.Handlers;
using BusinessOS.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Moq;

namespace BusinessOS.UnitTests;

public class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_WithInvalidCredentials_ThrowsUnauthorizedException()
    {
        var identityService = new Mock<IIdentityService>();
        identityService
            .Setup(x => x.FindByEmailAsync("a@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAuthResult?)null);

        var sut = CreateAuthService(identityService.Object);

        var act = () => sut.LoginAsync("a@test.com", "Password1!", CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsToken()
    {
        var user = new UserAuthResult("user-1", "a@test.com", Guid.NewGuid());
        var identityService = new Mock<IIdentityService>();
        identityService
            .Setup(x => x.FindByEmailAsync("a@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        identityService
            .Setup(x => x.ValidatePasswordAsync(user, "Password1!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        identityService
            .Setup(x => x.GetRolesAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Admin" });

        var jwt = new Mock<IJwtTokenGenerator>();
        jwt.Setup(x => x.GenerateToken(
                user.Id,
                user.Email,
                user.TenantId,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<string>>()))
            .Returns("token");
        jwt.Setup(x => x.GetTokenExpiration()).Returns(DateTime.UtcNow.AddHours(1));

        var roleRepository = new Mock<IRoleRepository>();
        roleRepository
            .Setup(x => x.GetUserRoleNamesAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Admin" });
        roleRepository
            .Setup(x => x.GetUserPermissionCodesAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Product.View" });

        var sut = CreateAuthService(identityService.Object, jwt.Object, roleRepository.Object);
        var result = await sut.LoginAsync("a@test.com", "Password1!", CancellationToken.None);

        result.Token.Should().Be("token");
        result.Email.Should().Be("a@test.com");
    }

    private static AuthService CreateAuthService(
        IIdentityService identityService,
        IJwtTokenGenerator? jwtTokenGenerator = null,
        IRoleRepository? roleRepository = null)
    {
        jwtTokenGenerator ??= Mock.Of<IJwtTokenGenerator>();
        roleRepository ??= Mock.Of<IRoleRepository>();
        var tenantProvider = new Mock<ITenantProvider>();
        var tenantRegistration = Mock.Of<ITenantRegistrationService>();
        return new AuthService(
            identityService,
            tenantRegistration,
            jwtTokenGenerator,
            tenantProvider.Object,
            Mock.Of<IDbContextFactory<BusinessOS.Infrastructure.Data.BusinessOSDbContext>>(),
            roleRepository,
            Mock.Of<IRbacAuditService>());
    }
}

public class CategoryHandlerTests
{
    [Fact]
    public async Task CreateCategory_WithDuplicateName_ThrowsConflictException()
    {
        var context = CreateContextWithCategory("Electronics");
        var handler = new CreateCategoryCommandHandler(context.Object);

        var act = () => handler.Handle(
            new CreateCategoryCommand("Electronics", "Desc"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task CreateCategory_WithValidData_ReturnsId()
    {
        var context = CreateEmptyContext();
        var handler = new CreateCategoryCommandHandler(context.Object);

        var id = await handler.Handle(
            new CreateCategoryCommand("Books", "Reading"),
            CancellationToken.None);

        id.Should().NotBe(Guid.Empty);
        context.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<IApplicationDbContext> CreateEmptyContext()
    {
        var categories = new List<Category>().AsQueryable();
        var mockSet = TestMockDbSet.CreateMockDbSet(categories);

        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Categories).Returns(mockSet.Object);
        context.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return context;
    }

    private static Mock<IApplicationDbContext> CreateContextWithCategory(string name)
    {
        var categories = new List<Category>
        {
            new() { Id = Guid.NewGuid(), Name = name, TenantId = Guid.NewGuid() }
        }.AsQueryable();

        var mockSet = TestMockDbSet.CreateMockDbSet(categories);
        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Categories).Returns(mockSet.Object);
        return context;
    }

    private static Mock<DbSet<T>> CreateMockDbSet<T>(IQueryable<T> data) where T : class =>
        TestMockDbSet.CreateMockDbSet(data);
}

public class ProductHandlerTests
{
    [Fact]
    public async Task CreateProduct_WithMissingCategory_ThrowsBadRequestException()
    {
        var categories = TestMockDbSet.CreateMockDbSet(new List<Category>().AsQueryable());
        var products = TestMockDbSet.CreateMockDbSet(new List<Product>().AsQueryable());

        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Categories).Returns(categories.Object);
        context.Setup(x => x.Products).Returns(products.Object);

        var handler = new CreateProductCommandHandler(
            context.Object,
            InventoryServiceTestHelper.CreateMock().Object);

        var act = () => handler.Handle(
            new CreateProductCommand(Guid.NewGuid(), "Item", "SKU", null, 1, 2, 1),
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>();
    }
}

internal sealed class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;

    internal TestAsyncQueryProvider(IQueryProvider inner) => _inner = inner;

    public IQueryable CreateQuery(System.Linq.Expressions.Expression expression) =>
        new TestAsyncEnumerable<TEntity>(expression);

    public IQueryable<TElement> CreateQuery<TElement>(System.Linq.Expressions.Expression expression) =>
        new TestAsyncEnumerable<TElement>(expression);

    public object? Execute(System.Linq.Expressions.Expression expression) =>
        _inner.Execute(expression);

    public TResult Execute<TResult>(System.Linq.Expressions.Expression expression) =>
        _inner.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(
        System.Linq.Expressions.Expression expression,
        CancellationToken cancellationToken = default)
    {
        var expected = typeof(TResult);
        if (expected.IsGenericType && expected.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var innerType = expected.GetGenericArguments()[0];
            var result = _inner.Execute(expression);
            return (TResult)typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(innerType)
                .Invoke(null, [result])!;
        }

        return _inner.Execute<TResult>(expression);
    }
}

internal sealed class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable)
    {
    }

    public TestAsyncEnumerable(System.Linq.Expressions.Expression expression) : base(expression)
    {
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());

    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
}

internal sealed class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _inner;

    public TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;

    public T Current => _inner.Current;

    public ValueTask DisposeAsync()
    {
        _inner.Dispose();
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> MoveNextAsync() => new(_inner.MoveNext());
}

internal static class TestMockDbSet
{
    public static Mock<DbSet<T>> CreateMockDbSet<T>(IQueryable<T> data) where T : class
    {
        var mockSet = new Mock<DbSet<T>>();
        mockSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(new TestAsyncQueryProvider<T>(data.Provider));
        mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(data.Expression);
        mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(data.ElementType);
        mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(data.GetEnumerator());
        return mockSet;
    }
}
