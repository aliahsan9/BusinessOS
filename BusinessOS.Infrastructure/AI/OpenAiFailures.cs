using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI;

public enum OpenAiFailureKind
{
    Unauthorized,
    Forbidden,
    RateLimited,
    Unavailable,
    Failed
}

/// <summary>
/// Provider failure that is safe to show to a user. It never carries the API key or response headers.
/// </summary>
public sealed record OpenAiFailure(
    OpenAiFailureKind Kind,
    int StatusCode,
    string ClientMessage,
    string? CorrelationId)
{
    public bool IsCredentialFailure =>
        Kind is OpenAiFailureKind.Unauthorized or OpenAiFailureKind.Forbidden;

    public int ClientStatusCode => Kind switch
    {
        OpenAiFailureKind.Unauthorized => 401,
        OpenAiFailureKind.Forbidden => 403,
        OpenAiFailureKind.RateLimited => 429,
        _ => 503
    };

    public static OpenAiFailure Transport() => new(
        OpenAiFailureKind.Unavailable,
        0,
        "OpenAI could not be reached. Try again shortly.",
        null);
}

public sealed class OpenAiProviderException : InvalidOperationException
{
    public OpenAiProviderException(OpenAiFailure failure)
        : base(failure.ClientMessage)
    {
        Failure = failure;
    }

    public OpenAiFailure Failure { get; }
}

public static partial class OpenAiHttp
{
    public static OpenAiFailure Describe(HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        var kind = status switch
        {
            401 => OpenAiFailureKind.Unauthorized,
            403 => OpenAiFailureKind.Forbidden,
            429 => OpenAiFailureKind.RateLimited,
            500 or 502 or 503 or 504 => OpenAiFailureKind.Unavailable,
            _ => OpenAiFailureKind.Failed
        };

        var message = kind switch
        {
            OpenAiFailureKind.Unauthorized =>
                "OpenAI rejected the API key. It may be invalid, expired, or revoked. Update Ai:OpenAiApiKey and try again.",
            OpenAiFailureKind.Forbidden =>
                "OpenAI refused this request. The API key is not allowed to use this model.",
            OpenAiFailureKind.RateLimited =>
                "OpenAI is rate limiting requests. Wait a moment and try again.",
            OpenAiFailureKind.Unavailable =>
                "OpenAI is temporarily unavailable. Try again shortly.",
            _ => "The OpenAI request failed. Try again."
        };

        return new OpenAiFailure(kind, status, message, ReadCorrelationId(response));
    }

    public static void LogFailure(ILogger logger, string operation, OpenAiFailure failure)
    {
        logger.LogWarning(
            "OpenAI {Operation} failed with status {StatusCode}. CorrelationId={CorrelationId}",
            operation,
            failure.StatusCode,
            string.IsNullOrWhiteSpace(failure.CorrelationId) ? "none" : failure.CorrelationId);
    }

    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return SecretPattern().Replace(value, "[redacted]");
    }

    private static string? ReadCorrelationId(HttpResponseMessage response)
    {
        foreach (var name in new[] { "x-request-id", "openai-request-id", "x-openai-request-id" })
        {
            if (!response.Headers.TryGetValues(name, out var values) &&
                (response.Content is null || !response.Content.Headers.TryGetValues(name, out values)))
            {
                continue;
            }

            var id = values.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var redacted = Redact(id.Trim());
            return redacted.Length > 128 ? redacted[..128] : redacted;
        }

        return null;
    }

    [GeneratedRegex(@"(?i)(sk-[a-z0-9_\-]{8,}|bearer\s+[a-z0-9_\-\.]{8,})", RegexOptions.CultureInvariant)]
    private static partial Regex SecretPattern();
}
