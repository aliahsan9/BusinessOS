using BusinessOS.Application.Features.Auth.Google;
using FluentAssertions;

namespace BusinessOS.UnitTests.Services;

public class GoogleTokenFailureClassifierTests
{
    [Theory]
    [InlineData("JWT has expired.", GoogleTokenFailure.Expired)]
    [InlineData("JWT contains untrusted 'aud' claim.", GoogleTokenFailure.WrongAudience)]
    [InlineData("JWT contains untrusted audience.", GoogleTokenFailure.WrongAudience)]
    [InlineData("JWT contains untrusted 'iss' claim.", GoogleTokenFailure.UntrustedIssuer)]
    [InlineData("The issuer is not trusted.", GoogleTokenFailure.UntrustedIssuer)]
    [InlineData("malformed", GoogleTokenFailure.Invalid)]
    [InlineData(null, GoogleTokenFailure.Invalid)]
    public void Classify_MapsLibraryMessagesWithoutEchoingTokens(string? message, GoogleTokenFailure expected)
    {
        GoogleTokenFailureClassifier.Classify(message).Should().Be(expected);
    }
}
