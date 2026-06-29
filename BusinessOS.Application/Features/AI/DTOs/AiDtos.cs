namespace BusinessOS.Application.Features.AI.DTOs;

public record AiChatRequest(
    string Message,
    string? CurrentPage = null,
    string? SearchQuery = null,
    Guid? CustomerId = null,
    Guid? OrderId = null,
    Guid? InvoiceId = null,
    Guid? ProjectId = null);

public sealed class AiChatResponse
{
    public string Reply { get; init; } = default!;
    public IReadOnlyList<AiSuggestionDto> Suggestions { get; init; } = [];
    public IReadOnlyList<AiQuickActionDto> QuickActions { get; init; } = [];
    public IReadOnlyList<AiSearchResultDto> SearchResults { get; init; } = [];
    public AiRetrievedSourcesDto Sources { get; init; } = new();
    public AiActionResultDto? ActionResult { get; init; }
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

public sealed class AiPageContextDto
{
    public string Url { get; init; } = default!;
    public string Module { get; init; } = default!;
    public Guid? CustomerId { get; init; }
    public Guid? OrderId { get; init; }
    public Guid? InvoiceId { get; init; }
    public Guid? ProjectId { get; init; }
}

public sealed class AiUserContextDto
{
    public string UserId { get; init; } = default!;
    public string? Email { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
}

public sealed class AiContextDto
{
    public AiUserContextDto User { get; init; } = default!;
    public AiPageContextDto Page { get; init; } = default!;
    public object? Customer { get; init; }
    public IReadOnlyList<object> Invoices { get; init; } = [];
    public IReadOnlyList<object> Orders { get; init; } = [];
    public IReadOnlyList<object> Projects { get; init; } = [];
    public object? Analytics { get; init; }
}

public sealed class AiRetrievedSourcesDto
{
    public int Customers { get; init; }
    public int Orders { get; init; }
    public int Invoices { get; init; }
    public int Projects { get; init; }
}

public sealed class AiActionResultDto
{
    public string Action { get; init; } = default!;
    public bool Success { get; init; }
    public string Message { get; init; } = default!;
    public string? EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public string? Route { get; init; }
}

public enum AiRetrievalScope
{
    None = 0,
    CurrentCustomer = 1,
    CurrentOrder = 2,
    CurrentInvoice = 3,
    CurrentProject = 4,
    CustomerBundle = 5,
    OverdueInvoices = 6,
    RevenueRanking = 7,
    ProjectProgress = 8,
    PageDefaults = 9
}
