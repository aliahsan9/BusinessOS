using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Integrations;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Domain.Entities;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Infrastructure.Billing;
using BusinessOS.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.Integrations;

public sealed class IntegrationPlatform : IIntegrationPlatform
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly BusinessOSDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly ICurrentUserService _user;
    private readonly IHttpClientFactory _http;
    private readonly IDataProtector _protector;
    private readonly IntegrationPlatformOptions _options;
    private readonly IServiceScopeFactory _scopes;
    private readonly IIntegrationProviderRegistry _providers;
    private readonly IIntegrationWebhookIdempotencyStore _webhookIds;

    public IntegrationPlatform(
        BusinessOSDbContext db,
        ITenantProvider tenant,
        ICurrentUserService user,
        IHttpClientFactory http,
        IDataProtectionProvider dataProtection,
        IOptions<IntegrationPlatformOptions> options,
        IServiceScopeFactory scopes,
        IIntegrationProviderRegistry providers,
        IIntegrationWebhookIdempotencyStore webhookIds)
    {
        _db = db;
        _tenant = tenant;
        _user = user;
        _http = http;
        _protector = dataProtection.CreateProtector("BusinessOS.IntegrationTokens.v1");
        _options = options.Value;
        _scopes = scopes;
        _providers = providers;
        _webhookIds = webhookIds;
    }

    public async Task<IReadOnlyList<IntegrationProviderDto>> GetMarketplaceAsync(CancellationToken cancellationToken = default)
    {
        Require(PermissionCodesView());
        var connections = await _db.CompanyIntegrationConnections.AsNoTracking().ToListAsync(cancellationToken);
        return IntegrationProviderCatalog.All.Select(provider =>
        {
            var connection = connections.FirstOrDefault(item => item.ProviderKey == provider.Key && item.Status == "Connected");
            return new IntegrationProviderDto(
                provider.Key,
                provider.Name,
                provider.Category,
                provider.Description,
                provider.ImplementationStatus,
                ServerConfigured(provider.Key),
                provider.ProviderVerified,
                provider.SupportsOAuth,
                provider.SupportsWebhooks,
                provider.ReadOperations,
                provider.WriteOperations,
                connection?.Status ?? "NotConnected",
                connection?.Id);
        }).ToList();
    }

    public async Task<IReadOnlyList<IntegrationConnectionDto>> GetConnectionsAsync(CancellationToken cancellationToken = default)
    {
        Require(PermissionCodesView());
        var rows = await _db.CompanyIntegrationConnections.AsNoTracking().OrderBy(x => x.ProviderKey).ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<IntegrationAuthorizeDto> BeginAuthorizeAsync(string providerKey, IntegrationAuthorizeRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodesManage());
        if (_tenant.HasTenant())
            await CommercialGate.EnsureFeatureAsync(_db, _tenant.TenantId, FeatureFlags.Integrations, cancellationToken);
        var provider = RequireProvider(providerKey);
        var credential = _options.For(provider.Key);
        if (!credential.Enabled || string.IsNullOrWhiteSpace(credential.ClientId))
        {
            return new IntegrationAuthorizeDto(false, null, "This provider is not configured on the server. Nothing was connected.");
        }

        if (!IsAllowedRedirect(request.RedirectUri))
            throw new BadRequestException("The redirect URI is not allowed.");
        var shop = NormalizeShop(provider.Key, request.ShopDomain);
        if (provider.Key == "shopify" && shop is null)
            throw new BadRequestException("A Shopify shop name is required.");
        if (!string.IsNullOrWhiteSpace(request.MetadataJson) && NalvaGuard.RejectsArguments(request.MetadataJson, out var rejected))
            throw new BadRequestException(rejected);

        var state = Base64Url(RandomNumberGenerator.GetBytes(32));
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        _db.IntegrationOAuthStates.Add(new IntegrationOAuthState
        {
            ProviderKey = provider.Key,
            State = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state))),
            CodeVerifier = _protector.Protect(verifier),
            RedirectUri = request.RedirectUri,
            MetadataJson = MergeMetadata(request.MetadataJson, shop),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            UserId = _user.UserId ?? "user"
        });
        await _db.SaveChangesAsync(cancellationToken);

        var (authorize, _) = IntegrationOAuthEndpoints.For(provider.Key, shop);
        var url = authorize
            + "?response_type=code"
            + "&client_id=" + Uri.EscapeDataString(credential.ClientId)
            + "&redirect_uri=" + Uri.EscapeDataString(request.RedirectUri)
            + "&scope=" + Uri.EscapeDataString(string.Join(' ', provider.Scopes))
            + "&state=" + Uri.EscapeDataString(state)
            + "&code_challenge=" + Uri.EscapeDataString(challenge)
            + "&code_challenge_method=S256";
        if (provider.Key.StartsWith("google", StringComparison.Ordinal) || provider.Key == "gmail")
            url += "&access_type=offline&prompt=consent";
        return new IntegrationAuthorizeDto(true, url, "Continue in the provider window. CompanyOS will store only protected tokens.");
    }

    public async Task<IntegrationConnectionDto> CompleteAuthorizeAsync(string providerKey, IntegrationCallbackRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodesManage());
        var provider = RequireProvider(providerKey);
        var credential = _options.For(provider.Key);
        if (!credential.Enabled || string.IsNullOrWhiteSpace(credential.ClientId) || string.IsNullOrWhiteSpace(credential.ClientSecret))
            throw new BadRequestException("This provider is not configured on the server.");
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.State))
            throw new BadRequestException("The authorization response is incomplete.");

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.State)));
        var state = await _db.IntegrationOAuthStates.FirstOrDefaultAsync(
            x => x.ProviderKey == provider.Key && x.State == hash && x.ConsumedAt == null, cancellationToken)
            ?? throw new BadRequestException("The authorization state is not valid.");
        if (state.ExpiresAt < DateTime.UtcNow)
            throw new BadRequestException("The authorization state has expired.");
        state.ConsumedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var shop = ShopFromMetadata(state.MetadataJson);
        var (_, tokenUrl) = IntegrationOAuthEndpoints.For(provider.Key, shop);
        var verifier = _protector.Unprotect(state.CodeVerifier);
        var payload = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = request.Code,
            ["redirect_uri"] = state.RedirectUri,
            ["client_id"] = credential.ClientId,
            ["client_secret"] = credential.ClientSecret,
            ["code_verifier"] = verifier
        };
        using var http = _http.CreateClient("Integrations");
        using var message = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Content = new FormUrlEncodedContent(payload)
        };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await http.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode || !TryReadToken(body, out var access, out var refresh, out var expires, out var scope, out var account))
        {
            await LogAsync(null, provider.Key, "oauth.callback", false, 0, "Token exchange failed.", cancellationToken);
            throw new BadRequestException("The provider did not return an access token. Nothing was stored.");
        }

        var connection = await _db.CompanyIntegrationConnections.FirstOrDefaultAsync(x => x.ProviderKey == provider.Key, cancellationToken)
            ?? new CompanyIntegrationConnection { ProviderKey = provider.Key, OwnerUserId = _user.UserId ?? state.UserId };
        if (_db.Entry(connection).State == EntityState.Detached)
            _db.CompanyIntegrationConnections.Add(connection);
        connection.Status = "Connected";
        connection.AccessTokenProtected = _protector.Protect(access);
        connection.RefreshTokenProtected = string.IsNullOrWhiteSpace(refresh) ? connection.RefreshTokenProtected : _protector.Protect(refresh);
        connection.TokenExpiresAt = expires;
        connection.ScopesJson = JsonSerializer.Serialize((scope ?? string.Join(' ', provider.Scopes)).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        connection.ExternalAccountLabel = account;
        connection.MetadataJson = state.MetadataJson;
        connection.LastError = null;
        connection.LastSuccessAt = DateTime.UtcNow;
        connection.OwnerUserId = _user.UserId ?? connection.OwnerUserId;
        await _db.SaveChangesAsync(cancellationToken);
        await LogAsync(connection.Id, provider.Key, "oauth.callback", true, 0, null, cancellationToken);
        return Map(connection);
    }

    public async Task<IntegrationConnectionDto> DisconnectAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodesManage());
        var connection = await _db.CompanyIntegrationConnections.FirstOrDefaultAsync(x => x.Id == connectionId, cancellationToken)
            ?? throw new NotFoundException("That connection was not found.");
        connection.Status = "Revoked";
        connection.AccessTokenProtected = null;
        connection.RefreshTokenProtected = null;
        connection.WebhookSecretProtected = null;
        connection.TokenExpiresAt = null;
        connection.LastError = null;
        await _db.SaveChangesAsync(cancellationToken);
        await LogAsync(connection.Id, connection.ProviderKey, "disconnect", true, 0, null, cancellationToken);
        return Map(connection);
    }

    public async Task<IntegrationCallResult> TestAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodesManage());
        var connection = await _db.CompanyIntegrationConnections.FirstOrDefaultAsync(x => x.Id == connectionId, cancellationToken)
            ?? throw new NotFoundException("That connection was not found.");
        if (!IntegrationProviderCatalog.TryGet(connection.ProviderKey, out var provider) || provider is null)
            throw new NotFoundException("That provider was not found.");
        var operation = provider.ReadOperations.FirstOrDefault();
        if (operation is null)
            return new IntegrationCallResult { Performed = false, Status = "not_enabled", Message = "This provider has no read operation." };
        if (operation is "get" or "payment" or "order" or "report" or "status")
            return new IntegrationCallResult { Performed = false, Status = "configuration", Message = "Add the provider metadata required for a test read. No request was sent." };
        return await ExecuteAsync(connection.ProviderKey, operation, "{}", false, cancellationToken);
    }

    public async Task<IReadOnlyList<IntegrationLogDto>> GetLogsAsync(Guid? connectionId, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodesView());
        var query = _db.IntegrationActivityLogs.AsNoTracking().AsQueryable();
        if (connectionId is Guid id)
            query = query.Where(x => x.ConnectionId == id);
        var rows = await query.OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(cancellationToken);
        return rows.Select(x => new IntegrationLogDto(x.Id, x.ProviderKey, x.Operation, x.Success, x.DurationMs, x.ErrorRedacted, x.CreatedAt)).ToList();
    }

    public async Task<IntegrationCallResult> ExecuteAsync(string providerKey, string operation, string argumentsJson, bool approved, CancellationToken cancellationToken = default)
    {
        if (!_tenant.HasTenant())
            throw new BadRequestException("Company context is required.");
        if (!IntegrationProviderCatalog.TryGet(providerKey, out var provider) || provider is null)
            return new IntegrationCallResult { Performed = false, Status = "unknown_provider", Message = "That provider is not registered." };
        if (NalvaGuard.RejectsArguments(argumentsJson, out var rejected))
            return new IntegrationCallResult { Performed = false, Status = "validation", Message = rejected };

        var write = provider.WriteOperations.Contains(operation, StringComparer.OrdinalIgnoreCase);
        var read = provider.ReadOperations.Contains(operation, StringComparer.OrdinalIgnoreCase);
        if (!read && !write)
            return new IntegrationCallResult { Performed = false, Status = "not_enabled", Message = "That operation is not enabled." };
        if (write && !approved)
            return new IntegrationCallResult { Performed = false, ApprovalRequired = true, Status = "approval_required", Message = "A person must approve this external change." };

        var connection = await _db.CompanyIntegrationConnections.FirstOrDefaultAsync(
            x => x.ProviderKey == provider.Key && (x.Status == IntegrationConnectionStatuses.Connected || x.Status == IntegrationConnectionStatuses.NeedsReauth),
            cancellationToken);
        if (connection?.AccessTokenProtected is null)
            return new IntegrationCallResult { Performed = false, Status = "not_connected", Message = "This company has not connected that provider." };
        if (connection.Status == IntegrationConnectionStatuses.NeedsReauth)
            return new IntegrationCallResult { Performed = false, Status = "needs_reauth", Message = "This connection needs reauthorization before another provider call." };

        if (!_providers.TryGet(provider.Key, out var executor) || executor is null)
            return new IntegrationCallResult { Performed = false, Status = "unknown_provider", Message = "That provider is not registered." };

        var started = DateTime.UtcNow;
        try
        {
            var token = await AccessTokenAsync(connection, provider.Key, cancellationToken);
            var result = await executor.ExecuteAsync(_http.CreateClient("Integrations"), operation, token, argumentsJson, connection.MetadataJson, cancellationToken);
            connection.LastSuccessAt = result.Performed ? DateTime.UtcNow : connection.LastSuccessAt;
            connection.LastError = result.Performed ? null : NalvaGuard.Clip(result.Message, 300);
            if (!result.Performed && result.Status == "permission")
                connection.Status = IntegrationConnectionStatuses.Error;
            if (!result.Performed && result.Status is "rate_limit" or "provider_error")
                await DeadLetterAsync(connection.Id, provider.Key, operation, result.Message, argumentsJson, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await LogAsync(connection.Id, provider.Key, operation, result.Performed, (int)(DateTime.UtcNow - started).TotalMilliseconds, result.Performed ? null : result.Message, cancellationToken);
            return result;
        }
        catch (Exception ex) when (ex is InvalidOperationException or CryptographicException)
        {
            connection.LastError = "The provider request was rejected before it was sent.";
            if (ex.Message.Contains("refreshed", StringComparison.OrdinalIgnoreCase))
                connection.Status = IntegrationConnectionStatuses.NeedsReauth;
            await _db.SaveChangesAsync(cancellationToken);
            await LogAsync(connection.Id, provider.Key, operation, false, (int)(DateTime.UtcNow - started).TotalMilliseconds, ex.Message, cancellationToken);
            return new IntegrationCallResult { Performed = false, Status = connection.Status == IntegrationConnectionStatuses.NeedsReauth ? "needs_reauth" : "validation", Message = ex.Message };
        }
    }

    public async Task<(int StatusCode, string? Challenge)> VerifyWebhookSubscriptionAsync(
        string providerKey,
        Guid connectionId,
        string? mode,
        string? verifyToken,
        string? challenge,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(mode, "subscribe", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(challenge) || string.IsNullOrWhiteSpace(verifyToken))
            return (400, null);

        var connection = await _db.CompanyIntegrationConnections.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == connectionId && x.ProviderKey == providerKey && !x.IsDeleted, cancellationToken);
        if (connection is null || string.IsNullOrWhiteSpace(connection.WebhookSecretProtected))
            return (404, null);

        var secret = _protector.Unprotect(connection.WebhookSecretProtected);
        var expected = Encoding.UTF8.GetBytes(secret);
        var supplied = Encoding.UTF8.GetBytes(verifyToken);
        if (expected.Length != supplied.Length || !CryptographicOperations.FixedTimeEquals(expected, supplied))
            return (403, null);

        return (200, challenge);
    }

    public async Task<IntegrationConnectionDto> SetWebhookSecretAsync(Guid connectionId, IntegrationWebhookSecretRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodesManage());
        if (string.IsNullOrWhiteSpace(request.Secret) || request.Secret.Length > 500)
            throw new BadRequestException("A webhook secret between 1 and 500 characters is required.");
        var connection = await _db.CompanyIntegrationConnections.FirstOrDefaultAsync(x => x.Id == connectionId, cancellationToken)
            ?? throw new NotFoundException("That connection was not found.");
        if (!IntegrationProviderCatalog.TryGet(connection.ProviderKey, out var provider) || provider is null || !provider.SupportsWebhooks)
            throw new BadRequestException("That provider does not accept webhooks.");
        connection.WebhookSecretProtected = _protector.Protect(request.Secret.Trim());
        connection.LastError = null;
        await _db.SaveChangesAsync(cancellationToken);
        await LogAsync(connection.Id, connection.ProviderKey, "webhook.secret", true, 0, null, cancellationToken);
        return Map(connection);
    }

    public async Task<IntegrationConnectionDto> UpdateConnectionMetadataAsync(Guid connectionId, IntegrationConnectionMetadataRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodesManage());
        var connection = await _db.CompanyIntegrationConnections.FirstOrDefaultAsync(x => x.Id == connectionId, cancellationToken)
            ?? throw new NotFoundException("That connection was not found.");
        if (string.IsNullOrWhiteSpace(request.MetadataJson))
        {
            connection.MetadataJson = null;
        }
        else
        {
            if (NalvaGuard.RejectsArguments(request.MetadataJson, out var rejected))
                throw new BadRequestException(rejected);
            try
            {
                _ = JsonNode.Parse(request.MetadataJson);
            }
            catch (JsonException)
            {
                throw new BadRequestException("Metadata must be valid JSON.");
            }

            connection.MetadataJson = NalvaGuard.Clip(request.MetadataJson, 4000);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await LogAsync(connection.Id, connection.ProviderKey, "metadata.update", true, 0, null, cancellationToken);
        return Map(connection);
    }

    public async Task<(int StatusCode, string Message)> ReceiveWebhookAsync(string providerKey, Guid connectionId, string body, string? signature, string? eventId, CancellationToken cancellationToken)
    {
        var connection = await _db.CompanyIntegrationConnections.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == connectionId && x.ProviderKey == providerKey && !x.IsDeleted, cancellationToken);
        if (connection is null || connection.Status != IntegrationConnectionStatuses.Connected)
            return (404, "Connection was not found.");
        _tenant.SetTenantId(connection.TenantId);
        if (string.IsNullOrWhiteSpace(connection.WebhookSecretProtected))
            return (400, "A webhook secret is not stored for this connection.");
        var secret = _protector.Unprotect(connection.WebhookSecretProtected);
        if (!_providers.TryGet(providerKey, out var provider) || provider is null || !provider.VerifyWebhookSignature(secret, body, signature))
            return (401, "The webhook signature is not valid.");

        var eventKey = string.IsNullOrWhiteSpace(eventId)
            ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)))
            : eventId.Trim();
        var claim = await _webhookIds.TryBeginAsync(providerKey, eventKey, connection.TenantId, cancellationToken);
        if (claim is IntegrationWebhookClaim.Duplicate)
            return (200, "Duplicate event ignored.");
        if (claim is IntegrationWebhookClaim.Busy)
            return (409, "Event is already being processed.");
        if (claim is not IntegrationWebhookClaim.Started startedClaim)
            return (500, "Webhook claim failed.");

        try
        {
            using var scope = _scopes.CreateScope();
            var tenant = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
            tenant.SetTenantId(connection.TenantId);
            var scopedDb = scope.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
            var scopedRegistry = scope.ServiceProvider.GetRequiredService<IIntegrationProviderRegistry>();
            scopedRegistry.TryGet(providerKey, out var scopedProvider);

            var handled = scopedProvider is null
                ? null
                : await scopedProvider.HandleWebhookAsync(connection.TenantId, connection.Id, body, eventKey, connection.MetadataJson, cancellationToken);

            var outbox = scope.ServiceProvider.GetRequiredService<INalvaEventOutbox>();
            var name = providerKey == "stripe" && body.Contains("payment_intent.succeeded", StringComparison.Ordinal)
                ? NalvaEventNames.PaymentReceived
                : "company.integration_webhook";
            await outbox.EnqueueAsync(
                name,
                JsonSerializer.Serialize(new { provider = providerKey, eventId = eventKey, handled = handled?.Handled }),
                "webhook:" + providerKey + ":" + eventKey,
                cancellationToken);

            scopedDb.IntegrationActivityLogs.Add(new IntegrationActivityLog
            {
                ConnectionId = connection.Id,
                ProviderKey = providerKey,
                Operation = "webhook",
                Success = true,
                DurationMs = 0
            });
            await scopedDb.SaveChangesAsync(cancellationToken);
            await _webhookIds.CompleteAsync(startedClaim.ReceiptId, cancellationToken);
            return (200, handled?.Message ?? "Accepted.");
        }
        catch (Exception ex)
        {
            await _webhookIds.DeadLetterAsync(startedClaim.ReceiptId, ex.Message, cancellationToken);
            await DeadLetterAsync(connection.Id, providerKey, "webhook", ex.Message, body, cancellationToken);
            return (500, "Webhook processing failed and was dead-lettered.");
        }
    }

    private async Task<string> AccessTokenAsync(CompanyIntegrationConnection connection, string providerKey, CancellationToken cancellationToken)
    {
        var access = _protector.Unprotect(connection.AccessTokenProtected!);
        if (connection.TokenExpiresAt is not DateTime expires || expires > DateTime.UtcNow.AddMinutes(2) || string.IsNullOrWhiteSpace(connection.RefreshTokenProtected))
            return access;
        var credential = _options.For(providerKey);
        if (string.IsNullOrWhiteSpace(credential.ClientId) || string.IsNullOrWhiteSpace(credential.ClientSecret))
            return access;
        var refresh = _protector.Unprotect(connection.RefreshTokenProtected);
        var shop = ShopFromMetadata(connection.MetadataJson);
        var (_, tokenUrl) = IntegrationOAuthEndpoints.For(providerKey, shop);
        using var http = _http.CreateClient("Integrations");
        using var message = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refresh,
                ["client_id"] = credential.ClientId,
                ["client_secret"] = credential.ClientSecret
            })
        };
        using var response = await http.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode || !TryReadToken(body, out var next, out var nextRefresh, out var nextExpires, out _, out _))
        {
            connection.Status = IntegrationConnectionStatuses.NeedsReauth;
            connection.LastError = "Token refresh failed. Reauthorize this connection.";
            await _db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("The provider token could not be refreshed.");
        }

        connection.AccessTokenProtected = _protector.Protect(next);
        if (!string.IsNullOrWhiteSpace(nextRefresh))
            connection.RefreshTokenProtected = _protector.Protect(nextRefresh);
        connection.TokenExpiresAt = nextExpires;
        await _db.SaveChangesAsync(cancellationToken);
        return next;
    }

    private static bool TryReadToken(string body, out string access, out string? refresh, out DateTime? expires, out string? scope, out string? account)
    {
        access = "";
        refresh = null;
        expires = null;
        scope = null;
        account = null;
        try
        {
            var node = JsonNode.Parse(body) as JsonObject;
            if (node is null || node["access_token"]?.GetValue<string>() is not string token || string.IsNullOrWhiteSpace(token))
                return false;
            access = token;
            refresh = node["refresh_token"]?.GetValue<string>();
            scope = node["scope"]?.GetValue<string>();
            account = node["team"]?["name"]?.GetValue<string>() ?? node["login"]?.GetValue<string>();
            if (node["expires_in"]?.GetValue<int>() is int seconds)
                expires = DateTime.UtcNow.AddSeconds(seconds);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    private async Task DeadLetterAsync(Guid? connectionId, string provider, string operation, string error, string? payload, CancellationToken cancellationToken)
    {
        _db.IntegrationDeadLetters.Add(new IntegrationDeadLetter
        {
            ConnectionId = connectionId,
            ProviderKey = provider,
            Operation = operation,
            ErrorRedacted = NalvaGuard.Redact(NalvaGuard.Clip(error, 500)),
            PayloadRedacted = payload is null ? null : NalvaGuard.Redact(NalvaGuard.Clip(payload, 1000)),
            Attempts = 1,
            Status = "Open"
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task LogAsync(Guid? connectionId, string provider, string operation, bool success, int duration, string? error, CancellationToken cancellationToken)
    {
        _db.IntegrationActivityLogs.Add(new IntegrationActivityLog
        {
            ConnectionId = connectionId,
            ProviderKey = provider,
            Operation = operation,
            Success = success,
            DurationMs = duration,
            ErrorRedacted = error is null ? null : NalvaGuard.Redact(NalvaGuard.Clip(error, 300))
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    private bool ServerConfigured(string providerKey)
    {
        var credential = _options.For(providerKey);
        return credential.Enabled && !string.IsNullOrWhiteSpace(credential.ClientId) && !string.IsNullOrWhiteSpace(credential.ClientSecret);
    }

    private bool IsAllowedRedirect(string? redirect)
    {
        if (!Uri.TryCreate(redirect, UriKind.Absolute, out var uri))
            return false;
        var local = uri.Host is "localhost" or "127.0.0.1";
        if (uri.Scheme == "http" && !local)
            return false;
        if (uri.Scheme is not ("https" or "http"))
            return false;
        return _options.AllowedRedirectHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);
    }

    private static string? NormalizeShop(string provider, string? shop)
    {
        if (provider != "shopify")
            return null;
        if (string.IsNullOrWhiteSpace(shop))
            return null;
        var name = shop.Trim().ToLowerInvariant().Replace(".myshopify.com", "", StringComparison.Ordinal);
        return name.All(character => char.IsLetterOrDigit(character) || character == '-') && name.Length is > 0 and <= 60 ? name : null;
    }

    private static string? MergeMetadata(string? metadata, string? shop)
    {
        var node = string.IsNullOrWhiteSpace(metadata) ? new JsonObject() : JsonNode.Parse(metadata) as JsonObject ?? new JsonObject();
        if (shop is not null)
            node["shop"] = shop;
        var json = node.ToJsonString();
        return json == "{}" ? null : json;
    }

    private static string? ShopFromMetadata(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
            return null;
        try
        {
            return JsonNode.Parse(metadata)?["shop"]?.GetValue<string>();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IntegrationProviderDefinition RequireProvider(string key) =>
        IntegrationProviderCatalog.TryGet(key, out var provider) && provider is not null
            ? provider
            : throw new NotFoundException("That provider is not registered.");

    private void Require(string permission)
    {
        if (!_tenant.HasTenant())
            throw new BadRequestException("Company context is required.");
        if (!_user.HasPermission(permission))
            throw new ForbiddenException("You do not have permission for integrations.");
    }

    private static string PermissionCodesView() => BusinessOS.Application.Common.Authorization.PermissionCodes.NalvaView;
    private static string PermissionCodesManage() => BusinessOS.Application.Common.Authorization.PermissionCodes.NalvaManage;

    private static IntegrationConnectionDto Map(CompanyIntegrationConnection row)
    {
        var scopes = JsonSerializer.Deserialize<List<string>>(row.ScopesJson, JsonOptions) ?? [];
        return new IntegrationConnectionDto(row.Id, row.ProviderKey, row.Status, row.ExternalAccountLabel, row.TokenExpiresAt, row.LastSuccessAt, row.LastError, row.OwnerUserId, scopes);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
