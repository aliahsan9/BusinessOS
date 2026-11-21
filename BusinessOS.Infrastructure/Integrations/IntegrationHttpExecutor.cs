using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using BusinessOS.Application.Features.Integrations;
using BusinessOS.Application.Features.Nalva;

namespace BusinessOS.Infrastructure.Integrations;

/// <summary>
/// Shared HTTP execution for providers with lightweight retries on 408/429/5xx.
/// </summary>
public static class IntegrationHttpExecutor
{
    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(800),
        TimeSpan.FromMilliseconds(2000)
    ];

    public static async Task<IntegrationCallResult> SendAsync(
        HttpClient http,
        string providerKey,
        string operation,
        HttpMethod method,
        string url,
        string accessToken,
        JsonObject? body,
        CancellationToken cancellationToken,
        Action<HttpRequestMessage>? configure = null)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt <= Backoff.Length; attempt++)
        {
            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (providerKey == "github")
                request.Headers.UserAgent.ParseAdd("CompanyOS");
            if (body is not null)
                request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            configure?.Invoke(request);

            try
            {
                using var response = await http.SendAsync(request, cancellationToken);
                var text = await response.Content.ReadAsStringAsync(cancellationToken);
                var redacted = NalvaGuard.Redact(NalvaGuard.Clip(text, 2000));
                var code = (int)response.StatusCode;

                if (ShouldRetry(response.StatusCode) && attempt < Backoff.Length)
                {
                    var delay = ReadRetryAfter(response) ?? Backoff[attempt];
                    await Task.Delay(delay, cancellationToken);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorClass = code == 429
                        ? "rate_limit"
                        : code is 401 or 403 ? "permission" : "provider_error";
                    return new IntegrationCallResult
                    {
                        Performed = false,
                        Status = errorClass,
                        Message = $"The provider returned {code}.",
                        Data = new JsonObject { ["statusCode"] = code, ["body"] = redacted, ["attempts"] = attempt + 1 }
                    };
                }

                JsonNode? payload = null;
                try
                {
                    payload = JsonNode.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
                }
                catch (System.Text.Json.JsonException)
                {
                    payload = new JsonObject { ["body"] = redacted };
                }

                return new IntegrationCallResult
                {
                    Performed = true,
                    Status = "completed",
                    Message = "The provider accepted the request.",
                    Data = new JsonObject
                    {
                        ["operation"] = operation,
                        ["provider"] = providerKey,
                        ["response"] = payload,
                        ["attempts"] = attempt + 1
                    }
                };
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastError = ex;
                if (attempt >= Backoff.Length)
                    break;
                await Task.Delay(Backoff[attempt], cancellationToken);
            }
        }

        return new IntegrationCallResult
        {
            Performed = false,
            Status = "provider_error",
            Message = NalvaGuard.Clip(lastError?.Message ?? "The provider request failed after retries.", 300)
        };
    }

    private static bool ShouldRetry(HttpStatusCode code) =>
        code is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is TimeSpan delta && delta > TimeSpan.Zero && delta < TimeSpan.FromSeconds(30))
            return delta;
        return null;
    }
}
