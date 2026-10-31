using System.Text;
using System.Text.Json;
using BusinessOS.API.Middleware;
using BusinessOS.Application.Common.Exceptions;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BusinessOS.UnitTests.Security;

public class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task UnexpectedException_HidesMessageAndStack()
    {
        var body = await InvokeAsync(new InvalidOperationException(
            "Password=hunter2; Server=prod-sql; User ID=sa"));

        body.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        body.Text.Should().Contain("An unexpected error occurred.");
        body.Text.Should().NotContain("hunter2");
        body.Text.Should().NotContain("prod-sql");
        body.Text.Should().NotContain("StackTrace");
        body.Text.Should().NotContain("InvalidOperationException");
    }

    [Fact]
    public async Task ValidationException_ReturnsFieldErrorsWithoutStack()
    {
        var exception = new ValidationException([
            new ValidationFailure("Email", "Email must be valid"),
            new ValidationFailure("Amount", "Amount must be greater than zero")
        ]);

        var body = await InvokeAsync(exception);

        body.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        body.Text.Should().Contain("Email must be valid");
        body.Text.Should().Contain("Amount must be greater than zero");
        body.Text.Should().NotContain("at BusinessOS");
    }

    [Theory]
    [InlineData(typeof(NotFoundException), StatusCodes.Status404NotFound, "Customer not found.")]
    [InlineData(typeof(ConflictException), StatusCodes.Status409Conflict, "SKU already exists.")]
    [InlineData(typeof(BadRequestException), StatusCodes.Status400BadRequest, "Quantity must be positive.")]
    public async Task KnownExceptions_MapToProblemDetails(Type exceptionType, int statusCode, string message)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, message)!;
        var body = await InvokeAsync(exception);

        body.StatusCode.Should().Be(statusCode);
        body.Text.Should().Contain(message);
        using var document = JsonDocument.Parse(body.Text);
        document.RootElement.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UnauthorizedException_DoesNotEchoCredentials()
    {
        var body = await InvokeAsync(new UnauthorizedException("Invalid credentials."));

        body.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        body.Text.Should().Contain("Invalid credentials.");
        body.Text.Should().NotContain("Password1!");
    }

    [Fact]
    public async Task MalformedBody_ReturnsGenericInvalidRequest()
    {
        var body = await InvokeAsync(new BadHttpRequestException("Expected start of object but found junk."));

        body.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        body.Text.Should().Contain("Invalid request body");
        body.Text.Should().NotContain("StackTrace");
    }

    private static async Task<(int StatusCode, string Text)> InvokeAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/api/v1/orders";
        context.RequestServices = new ServiceCollection().BuildServiceProvider();

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw exception,
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var text = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();
        return (context.Response.StatusCode, text);
    }
}
