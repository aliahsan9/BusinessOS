using System.Text.Json.Nodes;

namespace BusinessOS.Application.Features.Integrations;

public static class IntegrationProviderCatalog
{
    public const string Implemented = "Implemented";
    public const string Partial = "Partial";

    /// <summary>Phase 8C marketplace groups shown in the UI.</summary>
    public static readonly IReadOnlyList<string> MarketplaceCategories =
    [
        "Communication",
        "Social Media and Marketing",
        "Sales and Payments",
        "Productivity"
    ];

    public static readonly IReadOnlyList<IntegrationProviderDefinition> All =
    [
        Provider("whatsapp", "WhatsApp Business", "Communication",
            "Receive inbound WhatsApp Cloud API messages, keep conversation history, and send approved templates or in-window text. Media messages are not enabled.",
            Partial, true, true,
            ["status", "conversations", "messages"], ["sendTemplate", "sendText"],
            ["whatsapp_business_messaging", "whatsapp_business_management"],
            "Configure Meta app credentials, store phoneNumberId metadata, and set the webhook verify token after connect."),
        Provider("gmail", "Gmail", "Communication",
            "Read, search, draft, sync inbound, and send approved mail through a connected Google account.",
            Implemented, true, true,
            ["list", "get", "draft", "history", "syncInbound"], ["send"],
            ["https://www.googleapis.com/auth/gmail.modify"],
            "Enable Integrations:Google and reconnect if status is NeedsReauth."),
        Provider("outlook", "Microsoft Outlook", "Communication",
            "Read mail and send approved messages through Microsoft Graph when Outlook OAuth is configured.",
            Partial, true, false,
            ["list", "get"], ["send"],
            ["Mail.Read", "Mail.Send", "offline_access"],
            "Enable Integrations:Microsoft with an Azure app that has Mail.Read and Mail.Send. Not live-verified."),
        Provider("smtp-email", "Email (SMTP)", "Communication",
            "Send approved outbound email through tenant-configured SMTP. Credentials are encrypted on the server.",
            Partial, false, false,
            ["status"], ["send"],
            ["smtp"],
            "Use Configure SMTP to store host, port, username, and password. Never paste secrets into metadata JSON."),
        Provider("meta", "Meta (Facebook Pages & Instagram)", "Social Media and Marketing",
            "Discover Facebook Pages, publish approved feed posts, read insights, and publish Instagram images when instagramBusinessAccountId metadata and scopes are present. Carousel, Reels, and Messenger are not enabled.",
            Partial, true, false,
            ["pages", "insights", "instagramAccount"], ["publish", "publishInstagram"],
            ["pages_show_list", "pages_read_engagement", "pages_manage_posts", "pages_read_user_content", "instagram_basic", "instagram_content_publish"],
            "Store pageId (and instagramBusinessAccountId for IG). Publishing requires Meta app review for production."),
        Provider("google-business-profile", "Google Business Profile", "Social Media and Marketing",
            "Connect a Google Business Profile account, list eligible locations, read location info, create supported local posts, and read available performance metrics where the API permits. Review replies are not claimed.",
            Partial, true, false,
            ["accounts", "locations", "location", "performance"], ["createPost"],
            ["https://www.googleapis.com/auth/business.manage"],
            "Enable Integrations:Google, connect, then store accountId/locationId metadata. ProviderVerified stays false until live smoke succeeds."),
        Provider("linkedin", "LinkedIn", "Social Media and Marketing",
            "Read authorized organization information. Page publish and messaging remain blocked until LinkedIn write scopes are verified.",
            Partial, true, false,
            ["organization"], [],
            ["r_organization_social"],
            "Writes are intentionally empty. Do not claim publishing."),
        Provider("google-ads", "Google Ads", "Social Media and Marketing",
            "Read campaign metrics for a configured customer id. Budget and campaign changes are not enabled.",
            Partial, true, false,
            ["report"], [],
            ["https://www.googleapis.com/auth/adwords"],
            "Store customerId in metadata. No write operations."),
        Provider("google-analytics", "Google Analytics", "Social Media and Marketing",
            "Read a configured GA4 property. The property id is metadata, not a secret.",
            Partial, true, false,
            ["report"], [],
            ["https://www.googleapis.com/auth/analytics.readonly"],
            "Store propertyId in connection metadata."),
        Provider("wordpress", "WordPress.com", "Social Media and Marketing",
            "Read posts and create drafts on a WordPress.com site. Self-hosted sites are not connected.",
            Partial, true, false,
            ["posts"], ["draft"],
            ["global"],
            "Store site in metadata. Self-hosted WordPress is unsupported."),
        Provider("stripe", "Stripe", "Sales and Payments",
            "Company payment connection (tenant customers), separate from CompanyOS subscription billing. Refunds require approval. No card data is handled.",
            Implemented, true, true,
            ["balance", "payment", "payments"], ["refund"],
            ["read_write"],
            "Company Stripe Connect only — not SaaS subscription billing."),
        Provider("paypal", "PayPal", "Sales and Payments",
            "Read an order and refund only after approval. Region support follows the connected PayPal account.",
            Partial, true, true,
            ["order"], ["refund"],
            ["openid"],
            "Configure Integrations:PayPal. Not live-verified."),
        Provider("shopify", "Shopify", "Sales and Payments",
            "Read products and orders from the shop named when the company connects.",
            Implemented, true, true,
            ["products", "orders"], ["updateProduct"],
            ["read_products", "read_orders", "write_products"],
            "Provide the Shopify shop name before Connect."),
        Provider("google-calendar", "Google Calendar", "Productivity",
            "Read calendars, check free/busy, and create or update events after approval on a connected calendar.",
            Implemented, true, false,
            ["list", "freebusy"], ["create", "update", "delete"],
            ["https://www.googleapis.com/auth/calendar.events"],
            "Enable Integrations:Google. AI create requires human approval."),
        Provider("microsoft-calendar", "Microsoft Calendar", "Productivity",
            "Read Outlook calendar events and create approved events through Microsoft Graph.",
            Partial, true, false,
            ["list"], ["create", "update", "delete"],
            ["Calendars.ReadWrite", "offline_access"],
            "Enable Integrations:Microsoft. Not live-verified."),
        Provider("google-drive", "Google Drive", "Productivity",
            "List, search, and read files the company explicitly connected. Uploads require approval.",
            Implemented, true, false,
            ["list", "get"], ["upload"],
            ["https://www.googleapis.com/auth/drive.file"],
            "Enable Integrations:Google."),
        Provider("microsoft-onedrive", "Microsoft OneDrive", "Productivity",
            "List and read files from the connected OneDrive. Uploads require approval.",
            Partial, true, false,
            ["list", "get"], ["upload"],
            ["Files.ReadWrite", "offline_access"],
            "Enable Integrations:Microsoft. Not live-verified."),
        Provider("slack", "Slack", "Communication",
            "List channels the token can see and post a message only after approval.",
            Implemented, true, true,
            ["channels"], ["post"],
            ["channels:read", "chat:write"],
            "Enable Integrations:Slack."),
        Provider("microsoft-teams", "Microsoft Teams", "Communication",
            "Read joined teams and post a channel message only after approval.",
            Implemented, true, false,
            ["teams"], ["post"],
            ["Team.ReadBasic.All", "ChannelMessage.Send"],
            "Enable Integrations:Microsoft."),
        Provider("github", "GitHub", "Productivity",
            "Read selected repository data and create issues only after approval.",
            Implemented, true, true,
            ["repos", "issues", "pulls", "commits"], ["createIssue"],
            ["repo", "read:user"],
            "Enable Integrations:GitHub.")
    ];

    public static bool TryGet(string? key, out IntegrationProviderDefinition? provider)
    {
        provider = All.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        return provider is not null;
    }

    public static string TroubleshootingHint(string key) =>
        TryGet(key, out var provider) && provider is not null
            ? provider.TroubleshootingHint
            : "Reconnect the provider or contact support. Tokens are never shown in the UI.";

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
        string[] scopes,
        string troubleshooting) =>
        new(key, name, category, description, status, oauth, webhooks, false, reads, writes, scopes, troubleshooting);
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
    IReadOnlyList<string> Scopes,
    string TroubleshootingHint = "");

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

public sealed class IntegrationWebhookSecretRequest
{
    public string Secret { get; set; } = "";
}

public sealed class IntegrationConnectionMetadataRequest
{
    public string? MetadataJson { get; set; }
}

public sealed class IntegrationSmtpConfigureRequest
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string? FromDisplayName { get; set; }
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
    Guid? ConnectionId,
    IReadOnlyList<string> RequiredPermissions,
    DateTime? LastSuccessfulSynchronization,
    string TroubleshootingHint,
    bool EntitlementEnabled,
    string? SimulatedLabel = null);

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

public sealed record IntegrationHealthItemDto(
    string ProviderKey,
    string ProviderName,
    string Category,
    string ConnectionStatus,
    Guid? ConnectionId,
    DateTime? LastSuccessfulSynchronization,
    DateTime? TokenExpiresAt,
    string? LastError,
    bool CredentialsExpired,
    bool NeedsReconnect,
    int OpenDeadLetters,
    int RecentFailures,
    int RecentWebhookFailures,
    string RecoveryInstructions);

public sealed record IntegrationHealthDashboardDto(
    int ConnectedCount,
    int DisconnectedCount,
    int NeedsReauthCount,
    int ExpiredCredentialCount,
    int OpenDeadLetterCount,
    int RecentApiErrorCount,
    int RateLimitWarningCount,
    IReadOnlyList<IntegrationHealthItemDto> Items,
    IReadOnlyList<string> PendingReconnectActions);

public interface IIntegrationPlatform
{
    Task<IReadOnlyList<IntegrationProviderDto>> GetMarketplaceAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IntegrationConnectionDto>> GetConnectionsAsync(CancellationToken cancellationToken = default);
    Task<IntegrationHealthDashboardDto> GetHealthAsync(CancellationToken cancellationToken = default);
    Task<IntegrationAuthorizeDto> BeginAuthorizeAsync(string providerKey, IntegrationAuthorizeRequest request, CancellationToken cancellationToken = default);
    Task<IntegrationConnectionDto> CompleteAuthorizeAsync(string providerKey, IntegrationCallbackRequest request, CancellationToken cancellationToken = default);
    Task<IntegrationConnectionDto> DisconnectAsync(Guid connectionId, CancellationToken cancellationToken = default);
    Task<IntegrationCallResult> TestAsync(Guid connectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IntegrationLogDto>> GetLogsAsync(Guid? connectionId, CancellationToken cancellationToken = default);
    Task<IntegrationCallResult> ExecuteAsync(string providerKey, string operation, string argumentsJson, bool approved, CancellationToken cancellationToken = default);
    Task<(int StatusCode, string Message)> ReceiveWebhookAsync(string providerKey, Guid connectionId, string body, string? signature, string? eventId, CancellationToken cancellationToken = default);
    Task<(int StatusCode, string? Challenge)> VerifyWebhookSubscriptionAsync(string providerKey, Guid connectionId, string? mode, string? verifyToken, string? challenge, CancellationToken cancellationToken = default);
    Task<IntegrationConnectionDto> SetWebhookSecretAsync(Guid connectionId, IntegrationWebhookSecretRequest request, CancellationToken cancellationToken = default);
    Task<IntegrationConnectionDto> UpdateConnectionMetadataAsync(Guid connectionId, IntegrationConnectionMetadataRequest request, CancellationToken cancellationToken = default);
    Task<IntegrationConnectionDto> ConfigureSmtpAsync(IntegrationSmtpConfigureRequest request, CancellationToken cancellationToken = default);
}
