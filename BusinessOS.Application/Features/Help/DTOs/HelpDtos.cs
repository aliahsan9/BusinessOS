namespace BusinessOS.Application.Features.Help.DTOs;

public sealed class HelpFaqDto
{
    public string Id { get; init; } = default!;
    public string Category { get; init; } = default!;
    public string Question { get; init; } = default!;
    public string Answer { get; init; } = default!;
}

public sealed class HelpCenterDto
{
    public IReadOnlyList<HelpFaqDto> Faqs { get; init; } = [];
    public IReadOnlyList<HelpDocSectionDto> Documentation { get; init; } = [];
}

public sealed class HelpDocSectionDto
{
    public string Title { get; init; } = default!;
    public string Description { get; init; } = default!;
    public IReadOnlyList<string> Topics { get; init; } = [];
}
