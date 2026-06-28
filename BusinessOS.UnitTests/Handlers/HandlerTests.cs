using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Categories.Commands.DeleteCategory;
using BusinessOS.Application.Features.Categories.Commands.UpdateCategory;
using BusinessOS.Application.Features.Categories.Queries.GetCategoryById;
using BusinessOS.Application.Features.Products.Commands.CreateProduct;
using BusinessOS.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace BusinessOS.UnitTests.Handlers;

public class UpdateCategoryHandlerTests
{
    [Fact]
    public async Task Handle_WithMissingCategory_ThrowsNotFoundException()
    {
        var categories = TestMockDbSet.CreateMockDbSet(new List<Category>().AsQueryable());
        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Categories).Returns(categories.Object);

        var handler = new UpdateCategoryCommandHandler(context.Object);

        var act = () => handler.Handle(
            new UpdateCategoryCommand(Guid.NewGuid(), "Name", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}

public class DeleteCategoryHandlerTests
{
    [Fact]
    public async Task Handle_WithProducts_ThrowsConflictException()
    {
        var categoryId = Guid.NewGuid();
        var categories = TestMockDbSet.CreateMockDbSet(new List<Category>
        {
            new() { Id = categoryId, Name = "Electronics", TenantId = Guid.NewGuid() }
        }.AsQueryable());

        var products = TestMockDbSet.CreateMockDbSet(new List<Product>
        {
            new() { Id = Guid.NewGuid(), CategoryId = categoryId, Name = "Phone", SKU = "SKU-1", TenantId = Guid.NewGuid() }
        }.AsQueryable());

        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Categories).Returns(categories.Object);
        context.Setup(x => x.Products).Returns(products.Object);

        var handler = new DeleteCategoryCommandHandler(context.Object);

        var act = () => handler.Handle(new DeleteCategoryCommand(categoryId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }
}

public class GetCategoryByIdHandlerTests
{
    [Fact]
    public async Task Handle_WithMissingCategory_ThrowsNotFoundException()
    {
        var categories = TestMockDbSet.CreateMockDbSet(new List<Category>().AsQueryable());
        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Categories).Returns(categories.Object);

        var handler = new GetCategoryByIdQueryHandler(
            context.Object,
            Mock.Of<ILogger<GetCategoryByIdQueryHandler>>());

        var act = () => handler.Handle(new GetCategoryByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}

public class CreateProductSuccessHandlerTests
{
    [Fact]
    public async Task Handle_WithValidCategory_ReturnsId()
    {
        var categoryId = Guid.NewGuid();
        var categories = TestMockDbSet.CreateMockDbSet(new List<Category>
        {
            new() { Id = categoryId, Name = "Cat", TenantId = Guid.NewGuid() }
        }.AsQueryable());

        var products = TestMockDbSet.CreateMockDbSet(new List<Product>().AsQueryable());

        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Categories).Returns(categories.Object);
        context.Setup(x => x.Products).Returns(products.Object);
        context.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CreateProductCommandHandler(context.Object);

        var id = await handler.Handle(
            new CreateProductCommand(categoryId, "Laptop", "SKU-1", null, 100, 200, 5),
            CancellationToken.None);

        id.Should().NotBe(Guid.Empty);
    }
}
