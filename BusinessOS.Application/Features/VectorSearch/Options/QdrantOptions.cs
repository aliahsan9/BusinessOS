namespace BusinessOS.Application.Features.VectorSearch.Options;

public sealed class QdrantOptions
{
    public const string SectionName = "Qdrant";

    public bool Enabled { get; set; } = true;
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6333;
    public bool Https { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string CollectionName { get; set; } = "businessos_knowledge";
    public int VectorSize { get; set; } = 1536;
    public string Distance { get; set; } = "Cosine";
}
