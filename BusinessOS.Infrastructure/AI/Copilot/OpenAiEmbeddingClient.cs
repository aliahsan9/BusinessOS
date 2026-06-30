using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.AI.Copilot;

public sealed class OpenAiEmbeddingClient
{
    private readonly HttpClient _httpClient;
    private readonly AiOptions _options;
    private readonly ILogger<OpenAiEmbeddingClient> _logger;

    public OpenAiEmbeddingClient(
        HttpClient httpClient,
        IOptions<AiOptions> options,
        ILogger<OpenAiEmbeddingClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.OpenAiApiKey);

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return GenerateFallbackEmbedding(text);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/embeddings");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.OpenAiApiKey);
            request.Content = JsonContent.Create(new
            {
                model = _options.OpenAiEmbeddingModel,
                input = text.Length > 8000 ? text[..8000] : text
            });

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenAI embedding failed ({Status})", (int)response.StatusCode);
                return GenerateFallbackEmbedding(text);
            }

            var parsed = System.Text.Json.JsonSerializer.Deserialize<EmbeddingResponse>(body);
            return parsed?.Data?.FirstOrDefault()?.Embedding ?? GenerateFallbackEmbedding(text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Embedding request failed");
            return GenerateFallbackEmbedding(text);
        }
    }

    private static float[] GenerateFallbackEmbedding(string text)
    {
        var vector = new float[64];
        var tokens = text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            var hash = Math.Abs(token.GetHashCode()) % 64;
            vector[hash] += 1f;
        }

        var magnitude = Math.Sqrt(vector.Sum(v => v * v));
        if (magnitude > 0)
        {
            for (var i = 0; i < vector.Length; i++)
                vector[i] = (float)(vector[i] / magnitude);
        }

        return vector;
    }

    private sealed class EmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<EmbeddingData>? Data { get; init; }
    }

    private sealed class EmbeddingData
    {
        [JsonPropertyName("embedding")]
        public float[] Embedding { get; init; } = [];
    }
}
