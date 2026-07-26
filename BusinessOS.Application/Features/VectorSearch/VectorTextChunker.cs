using System.Text;
using System.Text.RegularExpressions;
using BusinessOS.Application.Features.VectorSearch.Models;

namespace BusinessOS.Application.Features.VectorSearch;

public static class VectorTextChunker
{
    public const int DefaultChunkSize = 800;
    public const int DefaultChunkOverlap = 100;

    public static IReadOnlyList<string> Chunk(
        string content,
        int chunkSize = DefaultChunkSize,
        int chunkOverlap = DefaultChunkOverlap)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(content))
            return chunks;

        var text = content.Trim();
        for (var start = 0; start < text.Length; start += chunkSize - chunkOverlap)
        {
            var length = Math.Min(chunkSize, text.Length - start);
            chunks.Add(text.Substring(start, length));
            if (start + length >= text.Length)
                break;
        }

        return chunks;
    }

    public static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..max] + "…";

    public static string NormalizeKeywords(string text)
        => Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9\s]", " ");
}
