using BusinessOS.Application.Features.Auth.Commands.Login;
using BusinessOS.Application.Features.Auth.Commands.Register;
using BusinessOS.Application.Features.Categories.Commands.CreateCategory;
using BusinessOS.Application.Features.Categories.Commands.DeleteCategory;
using BusinessOS.Application.Features.Categories.Commands.UpdateCategory;
using BusinessOS.Application.Features.Categories.Queries.GetAllCategories;
using BusinessOS.Application.Features.Categories.Queries.GetCategoryById;
using BusinessOS.Application.Features.Products.Commands.CreateProduct;
using BusinessOS.Application.Features.Products.Commands.DeleteProduct;
using BusinessOS.Application.Features.Products.Commands.UpdateProduct;
using BusinessOS.Application.Features.Products.Queries.GetAllProducts;
using BusinessOS.Application.Features.Products.Queries.GetProductById;
using BusinessOS.Application.Features.Products.Queries.GetProductsByCategory;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace BusinessOS.UnitTests.Validators;

public class AuthValidatorTests
{
    [Fact]
    public void LoginCommandValidator_RejectsEmptyEmail()
    {
        var validator = new LoginCommandValidator();
        var result = validator.TestValidate(new LoginCommand("", "Password1!"));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void RegisterCommandValidator_RejectsShortPassword()
    {
        var validator = new RegisterCommandValidator();
        var result = validator.TestValidate(new RegisterCommand(
            "a@test.com",
            "short",
            "First",
            "Last",
            "Business"));
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }
}

public class CategoryValidatorTests
{
    [Fact]
    public void CreateCategoryValidator_RejectsEmptyName()
    {
        var validator = new CreateCategoryCommandValidator();
        var result = validator.TestValidate(new CreateCategoryCommand("", "desc"));
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void UpdateCategoryValidator_RejectsEmptyId()
    {
        var validator = new UpdateCategoryCommandValidator();
        var result = validator.TestValidate(new UpdateCategoryCommand(Guid.Empty, "Name", null));
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void DeleteCategoryValidator_RejectsEmptyId()
    {
        var validator = new DeleteCategoryCommandValidator();
        var result = validator.TestValidate(new DeleteCategoryCommand(Guid.Empty));
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void GetAllCategoriesQueryValidator_RejectsInvalidSortBy()
    {
        var validator = new GetAllCategoriesQueryValidator();
        var result = validator.TestValidate(new GetAllCategoriesQuery(SortBy: "invalid"));
        result.ShouldHaveValidationErrorFor(x => x.SortBy);
    }

    [Fact]
    public void GetCategoryByIdQueryValidator_RejectsEmptyId()
    {
        var validator = new GetCategoryByIdQueryValidator();
        var result = validator.TestValidate(new GetCategoryByIdQuery(Guid.Empty));
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }
}

public class ProductValidatorTests
{
    [Fact]
    public void CreateProductValidator_RejectsInvalidPrices()
    {
        var validator = new CreateProductCommandValidator();
        var result = validator.TestValidate(new CreateProductCommand(
            Guid.NewGuid(),
            "Item",
            "SKU",
            null,
            0,
            -1,
            0));
        result.ShouldHaveValidationErrorFor(x => x.CostPrice);
        result.ShouldHaveValidationErrorFor(x => x.SalePrice);
    }

    [Fact]
    public void UpdateProductValidator_RejectsEmptySku()
    {
        var validator = new UpdateProductCommandValidator();
        var result = validator.TestValidate(new UpdateProductCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Name",
            "",
            null,
            1,
            2,
            1,
            true));
        result.ShouldHaveValidationErrorFor(x => x.SKU);
    }

    [Fact]
    public void DeleteProductValidator_RejectsEmptyId()
    {
        var validator = new DeleteProductCommandValidator();
        var result = validator.TestValidate(new DeleteProductCommand(Guid.Empty));
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void GetAllProductsQueryValidator_RejectsOversizedPageSize()
    {
        var validator = new GetAllProductsQueryValidator();
        var result = validator.TestValidate(new GetAllProductsQuery(PageSize: 500));
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void GetProductByIdQueryValidator_RejectsEmptyId()
    {
        var validator = new GetProductByIdQueryValidator();
        var result = validator.TestValidate(new GetProductByIdQuery(Guid.Empty));
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void GetProductsByCategoryQueryValidator_RejectsEmptyCategoryId()
    {
        var validator = new GetProductsByCategoryQueryValidator();
        var result = validator.TestValidate(new GetProductsByCategoryQuery(Guid.Empty));
        result.ShouldHaveValidationErrorFor(x => x.CategoryId);
    }
}
