namespace BusinessOS.Domain.Entities;

/// <summary>
/// Canonical chat-message roles persisted in <see cref="AiMessage.Role"/>.
/// </summary>
public static class AiMessageRoles
{
    public const string System = "system";
    public const string User = "user";
    public const string Assistant = "assistant";
    public const string Tool = "tool";

    public static bool IsKnown(string? role) =>
        role is System or User or Assistant or Tool;

    public static bool IsHistorySafe(string? role) =>
        role is User or Assistant or Tool;
}
