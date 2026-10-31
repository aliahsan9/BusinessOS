namespace BusinessOS.Application.Features.AI.ConversationMemory;

/// <summary>
/// Conservative character-based token estimate. Not a tokenizer: assumes ~4 characters per token
/// plus a small per-message overhead. Over-estimates slightly so the context window stays safe.
/// </summary>
public static class ConversationTokenEstimator
{
    public const int CharsPerToken = 4;
    public const int MessageOverheadTokens = 6;

    public static int EstimateTokens(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        return (int)Math.Ceiling(text.Length / (double)CharsPerToken);
    }

    public static int EstimateMessageTokens(string? role, string? content) =>
        MessageOverheadTokens + EstimateTokens(role) + EstimateTokens(content);
}
