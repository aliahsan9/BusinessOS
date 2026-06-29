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
