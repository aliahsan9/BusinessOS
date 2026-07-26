using BusinessOS.Application.Features.Agents.DTOs;

namespace BusinessOS.Application.Features.Agents.Services;

/// <summary>
/// Resolves AI employee persona prompts and catalog metadata.
/// </summary>
public interface IAgentPersonaService
{
    /// <summary>
    /// Lists active employees for the current tenant (tenant overrides win over system defaults).
    /// </summary>
    Task<IReadOnlyList<AgentEmployeeDto>> ListEmployeesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the effective persona for an agent key (falls back to Sophia / default).
    /// </summary>
    Task<AgentPersonaDto> ResolvePersonaAsync(
        string? agentKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the system default agent key for the current tenant context.
    /// </summary>
    Task<string> GetDefaultAgentKeyAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the full system prompt with optional language and tone adjustments.
    /// </summary>
    Task<string> BuildSystemPromptAsync(
        string? agentKey,
        string? language,
        bool preferEmployeeTone = true,
        CancellationToken cancellationToken = default);
}
