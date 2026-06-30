using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BusinessOS.Application.Features.AI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.AI;

/// <summary>
/// OpenAI Chat Completions API — primary LLM for RAG when OpenAiApiKey is configured.
/// </summary>
public sealed class OpenAiChatClient : ILlmChatClient
{
    private readonly HttpClient _httpClient;
    private readonly AiOptions _options;
    private readonly ILogger<OpenAiChatClient> _logger;

    public OpenAiChatClient(
        HttpClient httpClient,
        IOptions<AiOptions> options,
        ILogger<OpenAiChatClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.OpenAiApiKey);

    public async Task<string?> GenerateReplyAsync(
        Guid tenantId,
        string userId,
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.OpenAiApiKey);
            request.Content = JsonContent.Create(new
            {
                model = _options.OpenAiModel,
                temperature = 0.3,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                }
            });

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenAI request failed ({Status}): {Body}", (int)response.StatusCode, Truncate(body, 300));
                return null;
            }

            var parsed = System.Text.Json.JsonSerializer.Deserialize<ChatCompletionResponse>(body);
            var content = parsed?.Choices?.FirstOrDefault()?.Message?.Content;
            return string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI chat request failed");
            return null;
        }
    }

    public async IAsyncEnumerable<string> StreamReplyAsync(
        Guid tenantId,
        string userId,
        string systemPrompt,
        string userPrompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            yield break;

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.OpenAiApiKey);
        request.Content = JsonContent.Create(new
        {
            model = _options.OpenAiModel,
            temperature = 0.3,
            stream = true,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        });

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            yield break;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                break;
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ", StringComparison.Ordinal))
                continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]")
                break;

            var delta = ParseStreamDelta(data);
            if (!string.IsNullOrEmpty(delta))
                yield return delta;
        }
    }

    public async Task<(string? Reply, int? TokenUsage)> GenerateWithToolsAsync(
        Guid tenantId,
        string userId,
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<object> toolResults,
        CancellationToken cancellationToken = default)
    {
        var enrichedPrompt = userPrompt + "\n\nTool results:\n" + System.Text.Json.JsonSerializer.Serialize(toolResults);
        var reply = await GenerateReplyAsync(tenantId, userId, systemPrompt, enrichedPrompt, cancellationToken);
        return (reply, null);
    }

    private static string? ParseStreamDelta(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("delta")
                .TryGetProperty("content", out var content)
                ? content.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; init; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")]
        public Message? Message { get; init; }
    }

    private sealed class Message
    {
        [JsonPropertyName("content")]
        public string? Content { get; init; }
    }
}
