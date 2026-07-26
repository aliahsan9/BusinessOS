using BusinessOS.Application.Features.VectorSearch;
using FluentAssertions;

namespace BusinessOS.UnitTests.AI;

public class VectorTextChunkerTests
{
    [Fact]
    public void Chunk_RespectsOverlapAndSize()
    {
        var content = string.Join(' ', Enumerable.Repeat("business sales revenue product inventory customer invoice", 80));
        var chunks = VectorTextChunker.Chunk(content, chunkSize: 120, chunkOverlap: 20);

        chunks.Should().NotBeEmpty();
        chunks.Should().OnlyContain(c => c.Length <= 120);
        chunks.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public void NormalizeKeywords_LowercasesAndStripsPunctuation()
    {
        var keywords = VectorTextChunker.NormalizeKeywords("Best-Selling Products!");

        keywords.Should().Contain("best");
        keywords.Should().Contain("selling");
        keywords.Should().Contain("products");
        keywords.Should().NotContain("-");
        keywords.Should().NotContain("!");
    }

    [Fact]
    public void Truncate_AddsEllipsisWhenTooLong()
    {
        var truncated = VectorTextChunker.Truncate(new string('a', 50), 20);

        truncated.Length.Should().Be(21); // 20 chars + ellipsis
        truncated.Should().EndWith("…");
    }
}
