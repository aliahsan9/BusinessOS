using BusinessOS.Application.Features.Auth;
using FluentAssertions;

namespace BusinessOS.UnitTests.Services;

public class PasswordResetLinkTests
{
    [Fact]
    public void Build_UsesConfiguredOriginAndEncodesTokenOnce()
    {
        var token = "abc+def/ghi=";
        var link = PasswordResetLink.Build("https://app.example.com/", "owner@acme.com", token);

        link.Should().StartWith("https://app.example.com/auth/reset-password?");
        link.Should().Contain("email=owner%40acme.com");
        link.Should().Contain("token=abc%2Bdef%2Fghi%3D");
        link.Should().NotContain(token);
        Uri.UnescapeDataString(new Uri(link).Query.Split("token=")[1]).Should().Be(token);
    }

    [Theory]
    [InlineData("")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/relative")]
    public void Build_RejectsUntrustedBaseUrls(string baseUrl)
    {
        var act = () => PasswordResetLink.Build(baseUrl, "a@b.com", "token");
        act.Should().Throw<InvalidOperationException>();
    }
}
