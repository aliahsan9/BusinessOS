using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BusinessOS.Application.Features.Marketing;
using BusinessOS.Infrastructure.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.Services.Marketing;

public sealed class MarketingImageClient : IMarketingImageClient
{
    public const string HttpClientName = "MarketingOpenAiImages";

    private readonly IHttpClientFactory _http;
    private readonly AiOptions _options;
    private readonly ILogger<MarketingImageClient> _logger;

    public MarketingImageClient(IHttpClientFactory http, IOptions<AiOptions> options, ILogger<MarketingImageClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.OpenAiApiKey);

    public async Task<MarketingImageResult?> GenerateAsync(string prompt, int width, int height, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return null;

        var size = PickSize(width, height);
        var body = new JsonObject
        {
            ["model"] = "dall-e-3",
            ["prompt"] = prompt.Length > 3900 ? prompt[..3900] : prompt,
            ["n"] = 1,
            ["size"] = size,
            ["response_format"] = "b64_json"
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/images/generations")
            {
                Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.OpenAiApiKey);
            var client = _http.CreateClient(HttpClientName);
            using var response = await client.SendAsync(request, cancellationToken);
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenAI image generation failed with {Status}", (int)response.StatusCode);
                return null;
            }

            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.GetArrayLength() == 0)
                return null;
            var first = data[0];
            if (!first.TryGetProperty("b64_json", out var b64) || b64.ValueKind != JsonValueKind.String)
                return null;
            var bytes = Convert.FromBase64String(b64.GetString()!);
            return new MarketingImageResult(bytes, "image/png", "dall-e-3");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "OpenAI image generation threw.");
            return null;
        }
    }

    private static string PickSize(int width, int height)
    {
        if (width >= height * 1.3)
            return "1792x1024";
        if (height >= width * 1.3)
            return "1024x1792";
        return "1024x1024";
    }
}
