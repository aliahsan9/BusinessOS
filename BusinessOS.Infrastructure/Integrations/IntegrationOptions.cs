namespace BusinessOS.Infrastructure.Integrations;

public sealed class IntegrationPlatformOptions
{
    public const string SectionName = "Integrations";

    public string[] AllowedRedirectHosts { get; set; } = ["localhost", "127.0.0.1"];
    public ProviderCredential Google { get; set; } = new();
    public ProviderCredential GitHub { get; set; } = new();
    public ProviderCredential Slack { get; set; } = new();
    public ProviderCredential Microsoft { get; set; } = new();
    public ProviderCredential Meta { get; set; } = new();
    public ProviderCredential Stripe { get; set; } = new();
    public ProviderCredential PayPal { get; set; } = new();
    public ProviderCredential LinkedIn { get; set; } = new();
    public ProviderCredential Shopify { get; set; } = new();
    public ProviderCredential WordPress { get; set; } = new();

    public ProviderCredential For(string providerKey) => providerKey switch
    {
        "gmail" or "google-calendar" or "google-drive" or "google-analytics" or "google-ads" => Google,
        "github" => GitHub,
        "slack" => Slack,
        "microsoft-teams" => Microsoft,
        "whatsapp" or "meta" => Meta,
        "stripe" => Stripe,
        "paypal" => PayPal,
        "linkedin" => LinkedIn,
        "shopify" => Shopify,
        "wordpress" => WordPress,
        _ => new ProviderCredential()
    };
}

public sealed class ProviderCredential
{
    public bool Enabled { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}

public static class IntegrationOAuthEndpoints
{
    public static (string Authorize, string Token) For(string provider, string? shop) => provider switch
    {
        "gmail" or "google-calendar" or "google-drive" or "google-analytics" or "google-ads" =>
            ("https://accounts.google.com/o/oauth2/v2/auth", "https://oauth2.googleapis.com/token"),
        "github" => ("https://github.com/login/oauth/authorize", "https://github.com/login/oauth/access_token"),
        "slack" => ("https://slack.com/oauth/v2/authorize", "https://slack.com/api/oauth.v2.access"),
        "microsoft-teams" =>
            ("https://login.microsoftonline.com/common/oauth2/v2.0/authorize", "https://login.microsoftonline.com/common/oauth2/v2.0/token"),
        "whatsapp" or "meta" =>
            ("https://www.facebook.com/v21.0/dialog/oauth", "https://graph.facebook.com/v21.0/oauth/access_token"),
        "stripe" => ("https://connect.stripe.com/oauth/authorize", "https://connect.stripe.com/oauth/token"),
        "paypal" => ("https://www.paypal.com/signin/authorize", "https://api-m.paypal.com/v1/oauth2/token"),
        "linkedin" => ("https://www.linkedin.com/oauth/v2/authorization", "https://www.linkedin.com/oauth/v2/accessToken"),
        "shopify" => ($"https://{shop}.myshopify.com/admin/oauth/authorize", $"https://{shop}.myshopify.com/admin/oauth/access_token"),
        "wordpress" => ("https://public-api.wordpress.com/oauth2/authorize", "https://public-api.wordpress.com/oauth2/token"),
        _ => ("", "")
    };
}
