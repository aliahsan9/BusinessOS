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

    /// <summary>Creates or updates a tenant-scoped activation override for a catalog employee.</summary>
    Task<AgentEmployeeDto?> SetEmployeeActivationAsync(
        string agentKey,
        bool isActive,
        CancellationToken cancellationToken = default);

    /// <summary>Updates tenant-owned display metadata and language without exposing the system prompt.</summary>
    Task<AgentEmployeeDto?> UpdateEmployeeConfigurationAsync(
        string agentKey,
        UpdateAgentConfigurationRequest request,
        CancellationToken cancellationToken = default);

    Task<AgentToolPermissionsDto?> GetToolPermissionsAsync(string agentKey, CancellationToken cancellationToken = default);

    Task<AgentToolPermissionsDto?> UpdateToolPermissionsAsync(string agentKey, UpdateAgentToolPermissionsRequest request, CancellationToken cancellationToken = default);

    Task<bool> IsToolAllowedAsync(string agentKey, BusinessOS.Application.Features.AI.Enums.AiToolName tool, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the effective persona for an agent key (falls back to Navla / default). The stored key remains sophia.
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
