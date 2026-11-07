using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.AI;

/// <summary>
/// OpenAI Chat Completions API — primary LLM for RAG when OpenAiApiKey is configured.
/// </summary>
public sealed partial class OpenAiChatClient : ILlmChatClient
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

    /// <summary>Last provider failure for this client. Safe to show. Never contains the API key.</summary>
    public OpenAiFailure? LastFailure { get; private set; }

    public async Task<string?> GenerateReplyAsync(
        Guid tenantId,
        string userId,
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default,
        IReadOnlyList<AiMemoryTurnDto>? conversationHistory = null)
    {
        if (!IsConfigured)
            return null;

        LastTotalTokens = null;
        LastFailure = null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.OpenAiApiKey);
            request.Content = JsonContent.Create(new
            {
                model = _options.OpenAiModel,
                temperature = 0.3,
                messages = LlmHistoryFormatter.ToOpenAiMessages(systemPrompt, conversationHistory, userPrompt)
            });

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                LastFailure = OpenAiHttp.Describe(response);
                OpenAiHttp.LogFailure(_logger, "chat", LastFailure);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = System.Text.Json.JsonSerializer.Deserialize<ChatCompletionResponse>(body);
            var content = parsed?.Choices?.FirstOrDefault()?.Message?.Content;
            LastTotalTokens = parsed?.Usage?.TotalTokens;
            return string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LastFailure = OpenAiFailure.Transport();
            _logger.LogWarning("OpenAI chat request failed ({ExceptionType})", ex.GetType().Name);
            return null;
        }
    }

    public async IAsyncEnumerable<string> StreamReplyAsync(
        Guid tenantId,
        string userId,
        string systemPrompt,
        string userPrompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default,
        IReadOnlyList<AiMemoryTurnDto>? conversationHistory = null)
    {
        if (!IsConfigured)
            yield break;

        LastFailure = null;
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.OpenAiApiKey);
        request.Content = JsonContent.Create(new
        {
            model = _options.OpenAiModel,
            temperature = 0.3,
            stream = true,
            messages = LlmHistoryFormatter.ToOpenAiMessages(systemPrompt, conversationHistory, userPrompt)
        });

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            LastFailure = OpenAiHttp.Describe(response);
            OpenAiHttp.LogFailure(_logger, "chat stream", LastFailure);
            yield return LastFailure.ClientMessage;
            yield break;
        }

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
        CancellationToken cancellationToken = default,
        IReadOnlyList<AiMemoryTurnDto>? conversationHistory = null)
    {
        var enrichedPrompt = userPrompt + "\n\nLive tool results (current business data):\n" + System.Text.Json.JsonSerializer.Serialize(toolResults);
        var reply = await GenerateReplyAsync(tenantId, userId, systemPrompt, enrichedPrompt, cancellationToken, conversationHistory);
        return (reply, LastTotalTokens);
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

    private int? LastTotalTokens { get; set; }

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; init; }

        [JsonPropertyName("usage")]
        public UsageBody? Usage { get; init; }
    }

    private sealed class UsageBody
    {
        [JsonPropertyName("total_tokens")]
        public int? TotalTokens { get; init; }
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
