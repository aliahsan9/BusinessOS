using BusinessOS.Application.Behaviors;
using BusinessOS.Application.Features.Categories.Commands.CreateCategory;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Moq;

namespace BusinessOS.UnitTests.Behaviors;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_WithValidRequest_InvokesNext()
    {
        var validator = new Mock<IValidator<CreateCategoryCommand>>();
        validator
            .Setup(x => x.ValidateAsync(It.IsAny<ValidationContext<CreateCategoryCommand>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var behavior = new ValidationBehavior<CreateCategoryCommand, Guid>(
            new[] { validator.Object });

        var invoked = false;
        RequestHandlerDelegate<Guid> next = _ =>
        {
            invoked = true;
            return Task.FromResult(Guid.NewGuid());
        };

        await behavior.Handle(new CreateCategoryCommand("Books", null), next, CancellationToken.None);

        invoked.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithInvalidRequest_ThrowsValidationException()
    {
        var validator = new Mock<IValidator<CreateCategoryCommand>>();
        validator
            .Setup(x => x.ValidateAsync(It.IsAny<ValidationContext<CreateCategoryCommand>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([
                new ValidationFailure("Name", "Name is required")
            ]));

        var behavior = new ValidationBehavior<CreateCategoryCommand, Guid>(
            new[] { validator.Object });

        var act = () => behavior.Handle(
            new CreateCategoryCommand("", null),
            _ => Task.FromResult(Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }
}
