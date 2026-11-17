using System.Text.Json.Nodes;

namespace BusinessOS.Application.Features.Integrations;

public static class IntegrationProviderCatalog
{
    public const string Implemented = "Implemented";
    public const string Partial = "Partial";

    public static readonly IReadOnlyList<IntegrationProviderDefinition> All =
    [
        Provider("gmail", "Gmail", "Communication", "Read, search, draft, and send approved mail through a connected Google account.", Implemented, true, true,
            ["list", "get", "draft"], ["send"], ["https://www.googleapis.com/auth/gmail.modify"]),
        Provider("google-calendar", "Google Calendar", "Scheduling", "Read calendars and create or update events on a connected calendar.", Implemented, true, false,
            ["list", "freebusy"], ["create", "update", "delete"], ["https://www.googleapis.com/auth/calendar.events"]),
        Provider("google-drive", "Google Drive", "Documents", "List, search, and read files the company explicitly connected. Uploads require approval.", Implemented, true, false,
            ["list", "get"], ["upload"], ["https://www.googleapis.com/auth/drive.file"]),
        Provider("github", "GitHub", "Development", "Read selected repository data and create issues only after approval.", Implemented, true, true,
            ["repos", "issues", "pulls", "commits"], ["createIssue"], ["repo", "read:user"]),
        Provider("slack", "Slack", "Communication", "List channels the token can see and post a message only after approval.", Implemented, true, true,
            ["channels"], ["post"], ["channels:read", "chat:write"]),
        Provider("microsoft-teams", "Microsoft Teams", "Communication", "Read joined teams and post a channel message only after approval.", Implemented, true, false,
            ["teams"], ["post"], ["Team.ReadBasic.All", "ChannelMessage.Send"]),
        Provider("whatsapp", "WhatsApp Business", "Communication", "Send an approved template message through the WhatsApp Business Cloud API.", Partial, true, true,
            ["status"], ["sendTemplate"], ["whatsapp_business_messaging", "whatsapp_business_management"]),
        Provider("stripe", "Stripe", "Payments", "Company payment connection, separate from CompanyOS subscription billing. Refunds require approval.", Implemented, true, true,
            ["balance", "payment"], ["refund"], ["read_write"]),
        Provider("paypal", "PayPal", "Payments", "Read an order and refund only after approval. Region support follows the connected PayPal account.", Partial, true, true,
            ["order"], ["refund"], ["openid"]),
        Provider("linkedin", "LinkedIn", "Marketing", "Read authorized organization information. Scraping and unapproved messaging are not available.", Partial, true, false,
            ["organization"], [], ["r_organization_social"]),
        Provider("meta", "Meta", "Marketing", "Read authorized Facebook Page information. Publishing is not enabled.", Partial, true, false,
            ["pages"], [], ["pages_show_list", "pages_read_engagement"]),
        Provider("google-analytics", "Google Analytics", "Analytics", "Read a configured GA4 property. The property id is metadata, not a secret.", Partial, true, false,
            ["report"], [], ["https://www.googleapis.com/auth/analytics.readonly"]),
        Provider("google-ads", "Google Ads", "Advertising", "Read campaign metrics for a configured customer id. Budget and campaign changes are not enabled.", Partial, true, false,
            ["report"], [], ["https://www.googleapis.com/auth/adwords"]),
        Provider("shopify", "Shopify", "Commerce", "Read products and orders from the shop named when the company connects.", Implemented, true, true,
            ["products", "orders"], ["updateProduct"], ["read_products", "read_orders", "write_products"]),
        Provider("wordpress", "WordPress.com", "Content", "Read posts and create drafts on a WordPress.com site. Self-hosted sites are not connected.", Partial, true, false,
            ["posts"], ["draft"], ["global"])
    ];

    public static bool TryGet(string? key, out IntegrationProviderDefinition? provider)
    {
        provider = All.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        return provider is not null;
    }

    private static IntegrationProviderDefinition Provider(
        string key,
        string name,
        string category,
        string description,
        string status,
        bool oauth,
        bool webhooks,
        string[] reads,
        string[] writes,
        string[] scopes) =>
        new(key, name, category, description, status, oauth, webhooks, false, reads, writes, scopes);
}

public sealed record IntegrationProviderDefinition(
    string Key,
    string Name,
    string Category,
    string Description,
    string ImplementationStatus,
    bool SupportsOAuth,
    bool SupportsWebhooks,
    bool ProviderVerified,
    IReadOnlyList<string> ReadOperations,
    IReadOnlyList<string> WriteOperations,
    IReadOnlyList<string> Scopes);

public sealed class IntegrationAuthorizeRequest
{
    public string RedirectUri { get; set; } = "";
    public string? ShopDomain { get; set; }
    public string? MetadataJson { get; set; }
}

public sealed class IntegrationCallbackRequest
{
    public string Code { get; set; } = "";
    public string State { get; set; } = "";
}

public sealed class IntegrationExecuteRequest
{
    public string Operation { get; set; } = "";
    public string ArgumentsJson { get; set; } = "{}";
    public bool Approved { get; set; }
}

public sealed class IntegrationCallResult
{
    public bool Performed { get; init; }
    public bool ApprovalRequired { get; init; }
    public string Status { get; init; } = "";
    public string Message { get; init; } = "";
    public JsonObject Data { get; init; } = new();
}

public sealed record IntegrationProviderDto(
    string Key,
    string Name,
    string Category,
    string Description,
    string ImplementationStatus,
    bool ServerConfigured,
    bool ProviderVerified,
    bool SupportsOAuth,
    bool SupportsWebhooks,
    IReadOnlyList<string> ReadOperations,
    IReadOnlyList<string> WriteOperations,
    string? ConnectionStatus,
    Guid? ConnectionId);

public sealed record IntegrationConnectionDto(
    Guid Id,
    string ProviderKey,
    string Status,
    string? ExternalAccountLabel,
    DateTime? TokenExpiresAt,
    DateTime? LastSuccessAt,
    string? LastError,
    string OwnerUserId,
    IReadOnlyList<string> Scopes);

public sealed record IntegrationAuthorizeDto(
    bool ServerConfigured,
    string? AuthorizationUrl,
    string Message);

public sealed record IntegrationLogDto(
    Guid Id,
    string ProviderKey,
    string Operation,
    bool Success,
    int DurationMs,
    string? ErrorRedacted,
    DateTime CreatedAt);

public interface IIntegrationPlatform
{
    Task<IReadOnlyList<IntegrationProviderDto>> GetMarketplaceAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IntegrationConnectionDto>> GetConnectionsAsync(CancellationToken cancellationToken = default);
    Task<IntegrationAuthorizeDto> BeginAuthorizeAsync(string providerKey, IntegrationAuthorizeRequest request, CancellationToken cancellationToken = default);
    Task<IntegrationConnectionDto> CompleteAuthorizeAsync(string providerKey, IntegrationCallbackRequest request, CancellationToken cancellationToken = default);
    Task<IntegrationConnectionDto> DisconnectAsync(Guid connectionId, CancellationToken cancellationToken = default);
    Task<IntegrationCallResult> TestAsync(Guid connectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IntegrationLogDto>> GetLogsAsync(Guid? connectionId, CancellationToken cancellationToken = default);
    Task<IntegrationCallResult> ExecuteAsync(string providerKey, string operation, string argumentsJson, bool approved, CancellationToken cancellationToken = default);
    Task<(int StatusCode, string Message)> ReceiveWebhookAsync(string providerKey, Guid connectionId, string body, string? signature, string? eventId, CancellationToken cancellationToken = default);
}
