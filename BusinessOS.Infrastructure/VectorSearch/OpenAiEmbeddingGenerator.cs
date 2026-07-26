using BusinessOS.Application.Features.VectorSearch.Options;
using BusinessOS.Application.Features.VectorSearch.Services;
using BusinessOS.Infrastructure.AI.Copilot;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.VectorSearch;

public sealed class OpenAiEmbeddingGenerator : IEmbeddingGenerator
{
    private readonly OpenAiEmbeddingClient _client;
    private readonly QdrantOptions _options;

    public OpenAiEmbeddingGenerator(
        OpenAiEmbeddingClient client,
        IOptions<QdrantOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public int VectorSize => _options.VectorSize;

    public Task<float[]> GenerateAsync(string text, CancellationToken cancellationToken = default)
        => _client.GenerateEmbeddingAsync(text, cancellationToken, _options.VectorSize);
}
