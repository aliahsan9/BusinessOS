using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BusinessOS.Application.Features.AI.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.AI;

public sealed class CursorLlmChatClient : ILlmChatClient
{
    private readonly HttpClient _httpClient;
    private readonly AiOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CursorLlmChatClient> _logger;

    public CursorLlmChatClient(
        HttpClient httpClient,
        IOptions<AiOptions> options,
        IMemoryCache cache,
        ILogger<CursorLlmChatClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _cache = cache;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<string?> GenerateReplyAsync(
        Guid tenantId,
        string userId,
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return null;
        }

        var prompt = $"""
            {systemPrompt}

            {userPrompt}
            """;
        var cacheKey = $"cursor-agent:{tenantId}:{userId}";

        string agentId;
        string runId;

        if (_cache.TryGetValue(cacheKey, out string? cachedAgentId) && !string.IsNullOrWhiteSpace(cachedAgentId))
        {
            agentId = cachedAgentId;
            runId = await SendFollowUpAsync(agentId, prompt, cancellationToken);
        }
        else
        {
            (agentId, runId) = await CreateAgentAsync(prompt, cancellationToken);
            _cache.Set(cacheKey, agentId, TimeSpan.FromHours(4));
        }

        var result = await WaitForRunResultAsync(agentId, runId, cancellationToken);
        return string.IsNullOrWhiteSpace(result) ? null : result.Trim();
    }

    private async Task<(string AgentId, string RunId)> CreateAgentAsync(
        string prompt,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "v1/agents");
        request.Content = JsonContent.Create(new
        {
            prompt = new { text = prompt },
            model = new { id = _options.ModelId },
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ParseError(body, response.StatusCode));
        }

        var created = System.Text.Json.JsonSerializer.Deserialize<CreateAgentResponse>(body)
            ?? throw new InvalidOperationException("Cursor returned an empty agent response.");

        if (string.IsNullOrWhiteSpace(created.Agent?.Id) || string.IsNullOrWhiteSpace(created.Run?.Id))
        {
            throw new InvalidOperationException("Cursor did not return agent/run identifiers.");
        }

        return (created.Agent.Id, created.Run.Id);
    }

    private async Task<string> SendFollowUpAsync(
        string agentId,
        string prompt,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, $"v1/agents/{agentId}/runs");
        request.Content = JsonContent.Create(new
        {
            prompt = new { text = prompt },
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogWarning("Cursor agent {AgentId} was busy; creating a fresh agent", agentId);
            var created = await CreateAgentAsync(prompt, cancellationToken);
            return created.RunId;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ParseError(body, response.StatusCode));
        }

        var followUp = System.Text.Json.JsonSerializer.Deserialize<FollowUpRunResponse>(body)
            ?? throw new InvalidOperationException("Cursor returned an empty follow-up response.");

        if (string.IsNullOrWhiteSpace(followUp.Run?.Id))
        {
            throw new InvalidOperationException("Cursor did not return a follow-up run id.");
        }

        return followUp.Run.Id;
    }

    private async Task<string?> WaitForRunResultAsync(
        string agentId,
        string runId,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(Math.Max(10, _options.RunTimeoutSeconds));
        var pollDelay = TimeSpan.FromMilliseconds(Math.Max(500, _options.PollIntervalMs));

        while (DateTime.UtcNow < deadline)
        {
            using var request = CreateRequest(HttpMethod.Get, $"v1/agents/{agentId}/runs/{runId}");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(ParseError(body, response.StatusCode));
            }

            var run = System.Text.Json.JsonSerializer.Deserialize<RunStatusResponse>(body)
                ?? throw new InvalidOperationException("Cursor returned an empty run status response.");

            if (run.Status is "ERROR" or "CANCELLED" or "EXPIRED")
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(run.Result)
                        ? $"Cursor agent run ended with status {run.Status}."
                        : run.Result);
            }

            if (run.Status == "FINISHED")
            {
                return run.Result;
            }

            await Task.Delay(pollDelay, cancellationToken);
        }

        throw new TimeoutException("Cursor AI took too long to respond. Please try again.");
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl)
    {
        var request = new HttpRequestMessage(method, relativeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static string ParseError(string body, System.Net.HttpStatusCode statusCode)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return $"Cursor API request failed ({(int)statusCode}).";
        }

        try
        {
            var problem = System.Text.Json.JsonSerializer.Deserialize<CursorErrorResponse>(body);
            if (!string.IsNullOrWhiteSpace(problem?.Message))
            {
                return problem.Message;
            }

            if (!string.IsNullOrWhiteSpace(problem?.Error))
            {
                return problem.Error;
            }
        }
        catch
        {
            // Fall through to raw body.
        }

        return body.Length > 240 ? body[..240] + "…" : body;
    }

    private sealed class CreateAgentResponse
    {
        [JsonPropertyName("agent")]
        public AgentRef? Agent { get; init; }

        [JsonPropertyName("run")]
        public RunRef? Run { get; init; }
    }

    private sealed class FollowUpRunResponse
    {
        [JsonPropertyName("run")]
        public RunRef? Run { get; init; }
    }

    private sealed class AgentRef
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }
    }

    private sealed class RunRef
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }
    }

    private sealed class RunStatusResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("result")]
        public string? Result { get; init; }
    }

    private sealed class CursorErrorResponse
    {
        [JsonPropertyName("error")]
        public string? Error { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }
}
