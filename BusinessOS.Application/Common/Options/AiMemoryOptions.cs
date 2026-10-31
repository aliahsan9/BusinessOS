namespace BusinessOS.Application.Common.Options;

/// <summary>
/// Token-efficient conversational memory settings. Bound from the <c>AiMemory</c> configuration section.
/// </summary>
public sealed class AiMemoryOptions
{
    public const string SectionName = "AiMemory";

    public bool Enabled { get; set; } = true;

    /// <summary>Maximum recent role-messages included when token counts are unavailable.</summary>
    public int RecentMessageLimit { get; set; } = 10;

    /// <summary>
    /// Approximate token budget reserved for conversation history (summary + recent turns).
    /// Does not include system prompt, tools, or the model response.
    /// </summary>
    public int MaxHistoryTokens { get; set; } = 4000;

    public bool SummarizationEnabled { get; set; } = true;

    /// <summary>Start rolling summarization once unsummarized messages exceed this count.</summary>
    public int SummarizationThreshold { get; set; } = 20;

    /// <summary>Approximate maximum size of the stored rolling summary.</summary>
    public int MaxSummaryTokens { get; set; } = 1000;

    /// <summary>
    /// Optional future Qdrant long-term semantic memory. Disabled by default and unused in the
    /// SQL-backed conversation path. Do not mix chat embeddings with business-document collections.
    /// </summary>
    public bool LongTermSemanticMemoryEnabled { get; set; }
}
