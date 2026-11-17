namespace BusinessOS.Infrastructure.AI.Agents.Runtime;

/// <summary>
/// Business impact classification used by server-side AI employee execution policies.
/// </summary>
public enum AgentToolRiskLevel
{
    ReadOnly = 0,
    Low = 1,
    High = 2,
    Critical = 3
}
