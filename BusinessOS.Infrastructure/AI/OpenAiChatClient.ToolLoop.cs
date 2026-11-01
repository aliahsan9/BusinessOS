using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using BusinessOS.Application.Features.AI.DTOs;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI;

public readonly record struct ModelToolDefinition(string Name, string Description);

public readonly record struct ModelToolCall(string Id, string Name, string ArgumentsJson);

public sealed record ModelToolLoopResult(string? Reply, int? TokenUsage, int ToolRounds, bool Completed);

public sealed partial class OpenAiChatClient
{
    public const int MaxToolRounds = 4;

    /// <summary>
    /// OpenAI tool-calling loop. The model chooses tools; the caller executes them.
    /// Stops after <see cref="MaxToolRounds"/> rounds. Transient HTTP failures are retried twice.
    /// </summary>
    public async Task<ModelToolLoopResult?> RunToolLoopAsync(
        Guid tenantId,
        string userId,
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<ModelToolDefinition> tools,
        Func<ModelToolCall, CancellationToken, Task<string>> executeTool,
        CancellationToken cancellationToken = default,
        IReadOnlyList<AiMemoryTurnDto>? conversationHistory = null)
    {
        _ = tenantId;
        _ = userId;
        if (!IsConfigured || executeTool is null)
            return null;

        var messages = new JsonArray();
        messages.Add(ChatMessage("system", systemPrompt));
        if (conversationHistory is not null)
        {
            foreach (var turn in conversationHistory.TakeLast(8))
            {
                if (string.IsNullOrWhiteSpace(turn.Content))
                    continue;
                var role = turn.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user";
                messages.Add(ChatMessage(role, turn.Content));
            }
        }

        messages.Add(ChatMessage("user", userPrompt));

        var toolDefs = tools.Select(tool => new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["parameters"] = new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new JsonObject()
                }
            }
        }).ToArray();

        var totalTokens = 0;
        var sawUsage = false;

        for (var round = 0; round < MaxToolRounds; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var completion = await CompleteAsync(messages, toolDefs, cancellationToken);
            if (completion is null)
                return new ModelToolLoopResult(null, sawUsage ? totalTokens : null, round, false);

            if (completion.TotalTokens is int used)
            {
                totalTokens += used;
                sawUsage = true;
            }

            var calls = completion.ToolCalls;
            if (calls.Count == 0)
            {
                var text = string.IsNullOrWhiteSpace(completion.Content) ? null : completion.Content.Trim();
                return new ModelToolLoopResult(text, sawUsage ? totalTokens : null, round, text is not null);
            }

            messages.Add(AssistantToolCallMessage(completion.Content, calls));
            foreach (var call in calls)
            {
                string toolContent;
                if (!TryParseArguments(call.ArgumentsJson))
                {
                    toolContent = "The tool arguments were not valid JSON. Call the tool again with an empty object {}.";
                }
                else
                {
                    try
                    {
                        toolContent = await executeTool(call, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Tool {Tool} failed inside the model loop", call.Name);
                        toolContent = "The tool failed. Use the failure as the answer and do not claim the operation succeeded.";
                    }
                }

                messages.Add(new JsonObject
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = call.Id,
                    ["content"] = toolContent
                });
            }
        }

        return new ModelToolLoopResult(
            "I stopped after several tool calls so this request could not loop forever. Ask me to continue with one specific question.",
            sawUsage ? totalTokens : null,
            MaxToolRounds,
            true);
    }

    private async Task<CompletionPayload?> CompleteAsync(
        JsonArray messages,
        JsonObject[] tools,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.OpenAiApiKey);
                var body = new JsonObject
                {
                    ["model"] = _options.OpenAiModel,
                    ["temperature"] = 0.2,
                    ["messages"] = messages.DeepClone()
                };
                if (tools.Length > 0)
                {
                    body["tools"] = new JsonArray(tools.Select(tool => tool.DeepClone()).ToArray());
                    body["tool_choice"] = "auto";
                }

                request.Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
                using var response = await _httpClient.SendAsync(request, timeout.Token);
                var payload = await response.Content.ReadAsStringAsync(timeout.Token);
                if (response.IsSuccessStatusCode)
                    return ParseCompletion(payload);

                if (!IsTransient(response.StatusCode) || attempt == maxAttempts)
                {
                    _logger.LogWarning("OpenAI tool loop failed ({Status}) on attempt {Attempt}", (int)response.StatusCode, attempt);
                    return null;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("OpenAI tool loop timed out on attempt {Attempt}", attempt);
                if (attempt == maxAttempts)
                    return null;
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(ex, "OpenAI tool loop transport failure on attempt {Attempt}", attempt);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
        }

        return null;
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static bool TryParseArguments(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind is JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static CompletionPayload? ParseCompletion(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            int? tokens = null;
            if (root.TryGetProperty("usage", out var usage)
                && usage.TryGetProperty("total_tokens", out var total)
                && total.TryGetInt32(out var value))
            {
                tokens = value;
            }

            if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                return new CompletionPayload(null, [], tokens);

            var message = choices[0].GetProperty("message");
            var content = message.TryGetProperty("content", out var contentNode) && contentNode.ValueKind == JsonValueKind.String
                ? contentNode.GetString()
                : null;
            var calls = new List<ModelToolCall>();
            if (message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
            {
                foreach (var call in toolCalls.EnumerateArray())
                {
                    var id = call.TryGetProperty("id", out var idNode) ? idNode.GetString() : null;
                    if (!call.TryGetProperty("function", out var function))
                        continue;
                    var name = function.TryGetProperty("name", out var nameNode) ? nameNode.GetString() : null;
                    var arguments = function.TryGetProperty("arguments", out var argsNode) ? argsNode.GetString() : "{}";
                    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                        continue;
                    calls.Add(new ModelToolCall(id, name, string.IsNullOrWhiteSpace(arguments) ? "{}" : arguments));
                }
            }

            return new CompletionPayload(content, calls, tokens);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonObject ChatMessage(string role, string content) => new()
    {
        ["role"] = role,
        ["content"] = content
    };

    private static JsonObject AssistantToolCallMessage(string? content, IReadOnlyList<ModelToolCall> calls)
    {
        var message = new JsonObject
        {
            ["role"] = "assistant",
            ["content"] = content,
            ["tool_calls"] = new JsonArray(calls.Select(call => new JsonObject
            {
                ["id"] = call.Id,
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = call.Name,
                    ["arguments"] = call.ArgumentsJson
                }
            }).ToArray())
        };
        return message;
    }

    private sealed record CompletionPayload(string? Content, IReadOnlyList<ModelToolCall> ToolCalls, int? TotalTokens);
}
