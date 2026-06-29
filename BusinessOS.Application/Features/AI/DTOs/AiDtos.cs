namespace BusinessOS.Application.Features.AI.DTOs;

public record AiChatRequest(
    string Message,
    string? CurrentPage = null,
    string? SearchQuery = null);

public sealed class AiChatResponse
{
    public string Reply { get; init; } = default!;
    public IReadOnlyList<AiSuggestionDto> Suggestions { get; init; } = [];
    public IReadOnlyList<AiQuickActionDto> QuickActions { get; init; } = [];
    public IReadOnlyList<AiSearchResultDto> SearchResults { get; init; } = [];
}

public sealed class AiSuggestionDto
{
    public string Label { get; init; } = default!;
    public string Message { get; init; } = default!;
}

public sealed class AiQuickActionDto
{
    public string Label { get; init; } = default!;
    public string Route { get; init; } = default!;
    public string Icon { get; init; } = default!;
}

public sealed class AiSearchResultDto
{
    public string Type { get; init; } = default!;
    public string Id { get; init; } = default!;
    public string Title { get; init; } = default!;
    public string? Subtitle { get; init; }
    public string Route { get; init; } = default!;
}
