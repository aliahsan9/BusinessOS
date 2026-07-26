using System.Text.Json;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;

namespace BusinessOS.Application.Features.Agents.Services;

/// <summary>
/// Bilingual intent parser wrapping the existing copilot intent detector.
/// </summary>
public interface IAgentIntentParser
{
    AiIntentDetectionResult Parse(
        string message,
        AiPageContextDto page,
        AiMemoryStateDto memory,
        string? language = null);
}

/// <summary>
/// Extracts strongly typed JSON arguments for a tool from natural language.
/// </summary>
public interface IAgentArgumentExtractor
{
    Task<JsonElement> ExtractAsync(
        AiToolName toolName,
        string parameterSchemaJson,
        string message,
        AgentExecutionState state,
        AiPageContextDto page,
        string language,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Permission-checked tool execution with argument extraction, logging, and one retry.
/// </summary>
public interface IAgentToolExecutor
{
    Task<AgentToolExecutionResult> ExecuteAsync(
        AgentToolExecutionRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Classifies tool failures and suggests retry or clarification.
/// </summary>
public interface IAgentSelfCorrector
{
    AgentSelfCorrectionResult Analyze(
        AiToolName toolName,
        AiToolResult result,
        Exception? exception,
        JsonElement? args,
        string language);
}

/// <summary>
/// Structured logging for every agent tool action.
/// </summary>
public interface IAgentActionLogger
{
    Task LogAsync(AgentActionLogEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>
/// Orchestrates intent → plan → sequential tool execution with live progress events.
/// </summary>
public interface IAgentRuntimeOrchestrator
{
    IAsyncEnumerable<AgentStreamChunkDto> RunStreamAsync(
        AgentChatRequest request,
        string agentKey,
        string language,
        AgentPersonaDto persona,
        CancellationToken cancellationToken = default);
}
