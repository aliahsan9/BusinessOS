namespace BusinessOS.Infrastructure.AI;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Cursor Cloud Agents API key (crsr_...).</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.cursor.com/";

    public string ModelId { get; set; } = "composer-2";

    /// <summary>OpenAI API key (sk-...). Preferred for RAG when set.</summary>
    public string OpenAiApiKey { get; set; } = string.Empty;

    public string OpenAiModel { get; set; } = "gpt-4o-mini";

    public string OpenAiEmbeddingModel { get; set; } = "text-embedding-3-small";

    public string OpenAiBaseUrl { get; set; } = "https://api.openai.com/";

    /// <summary>Max seconds to wait for a Cursor agent run to finish.</summary>
    public int RunTimeoutSeconds { get; set; } = 120;

    public int PollIntervalMs { get; set; } = 2000;
}
