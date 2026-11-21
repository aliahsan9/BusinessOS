using System.Net.Http.Headers;
using BusinessOS.Infrastructure.AI;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.Diagnostics;

/// <summary>
/// Readiness probe for the OpenAI provider. Missing credentials report Degraded, not Unhealthy,
/// so the API can still serve non-AI workloads. Failure details never include the API key.
/// </summary>
public sealed class OpenAiHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiOptions _options;
    private readonly ILogger<OpenAiHealthCheck> _logger;

    public OpenAiHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptions<AiOptions> options,
        ILogger<OpenAiHealthCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.OpenAiApiKey))
        {
            return HealthCheckResult.Degraded(
                "OpenAI API key is not configured. AI features use fallback behaviour.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient(OpenAiStartupCheck.HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, "v1/models");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.OpenAiApiKey);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("OpenAI is reachable.");
            }

            _logger.LogWarning(
                "OpenAI health check returned {StatusCode}.",
                (int)response.StatusCode);
            return HealthCheckResult.Degraded("OpenAI rejected the configured credentials or is unavailable.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "OpenAI health check failed ({ExceptionType}).",
                ex.GetType().Name);
            return HealthCheckResult.Degraded("OpenAI is unreachable.");
        }
    }
}
