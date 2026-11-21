using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Enterprise;
using BusinessOS.Application.Features.Roles.Services;
using BusinessOS.Application.Features.Tenant.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace BusinessOS.Infrastructure.Enterprise;

public sealed class EnterpriseSsoService : IEnterpriseSsoService
{
    private readonly BusinessOSDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly IEnterpriseEntitlementGuard _guard;
    private readonly IEnterpriseAuditService _audit;
    private readonly ICurrentUserService _currentUser;
    private readonly IDataProtector _protector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuthService _authService;
    private readonly IRoleService _roleService;
    private readonly ITenantLimitService _limits;

    public EnterpriseSsoService(
        BusinessOSDbContext db,
        ITenantProvider tenant,
        IEnterpriseEntitlementGuard guard,
        IEnterpriseAuditService audit,
        ICurrentUserService currentUser,
        IDataProtectionProvider dataProtection,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager,
        IAuthService authService,
        IRoleService roleService,
        ITenantLimitService limits)
    {
        _db = db;
        _tenant = tenant;
        _guard = guard;
        _audit = audit;
        _currentUser = currentUser;
        _protector = dataProtection.CreateProtector("BusinessOS.Enterprise.SsoSecrets");
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _userManager = userManager;
        _authService = authService;
        _roleService = roleService;
        _limits = limits;
    }

    public async Task<IReadOnlyList<TenantSsoConfigurationDto>> GetConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        await _guard.EnsureAdvancedSecurityAsync(cancellationToken);
        return await _db.TenantSsoConfigurations.AsNoTracking()
            .OrderBy(x => x.Provider)
            .Select(x => Map(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<TenantSsoConfigurationDto> SaveConfigurationAsync(Guid? id, TenantSsoWriteRequest request, CancellationToken cancellationToken = default)
    {
        await _guard.EnsureAdvancedSecurityAsync(cancellationToken);
        var protocol = request.Protocol.Trim();
        if (protocol is not ("Oidc" or "Saml"))
            throw new BadRequestException("Protocol must be Oidc or Saml.");
        if (string.IsNullOrWhiteSpace(request.Provider))
            throw new BadRequestException("Provider is required.");

        if (protocol == "Oidc" && (string.IsNullOrWhiteSpace(request.Authority) || string.IsNullOrWhiteSpace(request.ClientId)))
            throw new BadRequestException("OIDC requires Authority and ClientId.");
        if (protocol == "Saml" && string.IsNullOrWhiteSpace(request.MetadataUrl) && string.IsNullOrWhiteSpace(request.IdpMetadataXml))
            throw new BadRequestException("SAML requires MetadataUrl or IdpMetadataXml.");

        TenantSsoConfiguration entity;
        if (id is Guid existingId)
        {
            entity = await _db.TenantSsoConfigurations.FirstOrDefaultAsync(x => x.Id == existingId, cancellationToken)
                ?? throw new NotFoundException("SSO configuration was not found.");
        }
        else
        {
            entity = new TenantSsoConfiguration { TenantId = _tenant.TenantId };
            _db.TenantSsoConfigurations.Add(entity);
        }

        entity.Protocol = protocol;
        entity.Provider = request.Provider.Trim();
        entity.IsEnabled = request.IsEnabled;
        entity.DisplayName = request.DisplayName;
        entity.Authority = request.Authority?.TrimEnd('/');
        entity.ClientId = request.ClientId;
        if (!string.IsNullOrWhiteSpace(request.ClientSecret))
            entity.ClientSecretProtected = _protector.Protect(request.ClientSecret);
        entity.AllowedEmailDomains = request.AllowedEmailDomains;
        entity.MetadataUrl = request.MetadataUrl;
        entity.IdpEntityId = request.IdpEntityId;
        entity.IdpMetadataXml = request.IdpMetadataXml;
        entity.SpEntityId = request.SpEntityId ?? $"{GetApiBase()}/api/v1/enterprise/sso/saml/{_tenant.TenantId}/metadata";
        entity.DefaultRoleName = request.DefaultRoleName ?? "Employee";
        entity.GroupRoleMapJson = request.GroupRoleMapJson;
        entity.JitProvision = request.JitProvision;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(new EnterpriseAuditWriteRequest
        {
            Category = EnterpriseAuditCategories.Administrative,
            Action = id is null ? "SsoConfigCreated" : "SsoConfigUpdated",
            ActorUserId = _currentUser.UserId,
            ActorType = "User",
            EntityType = nameof(TenantSsoConfiguration),
            EntityId = entity.Id.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { entity.Provider, entity.Protocol, entity.IsEnabled })
        }, cancellationToken);

        return Map(entity);
    }

    public async Task<EnterpriseSecuritySettingsDto> GetSecuritySettingsAsync(CancellationToken cancellationToken = default)
    {
        await _guard.EnsureAdvancedSecurityAsync(cancellationToken);
        var settings = await GetOrCreateSettingsAsync(cancellationToken);
        return new EnterpriseSecuritySettingsDto(
            settings.RequireMfa,
            settings.SessionIdleMinutes,
            settings.MaxConcurrentSessions,
            settings.AuditRetentionDays,
            settings.EnforceOrgScopeOnQueries);
    }

    public async Task<EnterpriseSecuritySettingsDto> SaveSecuritySettingsAsync(EnterpriseSecuritySettingsWriteRequest request, CancellationToken cancellationToken = default)
    {
        await _guard.EnsureAdvancedSecurityAsync(cancellationToken);
        var settings = await GetOrCreateSettingsAsync(cancellationToken);
        settings.RequireMfa = request.RequireMfa;
        settings.SessionIdleMinutes = Math.Clamp(request.SessionIdleMinutes, 5, 1440);
        settings.MaxConcurrentSessions = Math.Clamp(request.MaxConcurrentSessions, 1, 100);
        settings.AuditRetentionDays = Math.Clamp(request.AuditRetentionDays, 30, 3650);
        settings.EnforceOrgScopeOnQueries = request.EnforceOrgScopeOnQueries;
        settings.UpdatedAt = DateTime.UtcNow;

        var tenantSettings = await _db.TenantSettings.FirstOrDefaultAsync(cancellationToken);
        if (tenantSettings is not null)
        {
            tenantSettings.RequireMfa = request.RequireMfa;
            tenantSettings.SessionIdleMinutes = settings.SessionIdleMinutes;
            tenantSettings.MaxConcurrentSessions = settings.MaxConcurrentSessions;
            tenantSettings.AuditRetentionDays = settings.AuditRetentionDays;
            tenantSettings.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await GetSecuritySettingsAsync(cancellationToken);
    }

    public async Task<SsoChallengeResult> BeginOidcAsync(Guid configurationId, string returnUrl, CancellationToken cancellationToken = default)
    {
        var config = await LoadEnabledConfig(configurationId, "Oidc", cancellationToken);
        var discovery = await GetOidcConfigAsync(config.Authority!, cancellationToken);
        var state = CreateState(configurationId, returnUrl, out var codeVerifier);
        var challenge = new
        {
            codeVerifier,
            returnUrl,
            configurationId,
            tenantId = config.TenantId
        };
        // State blob is signed via DataProtection so callbacks cannot be forged.
        var protectedState = _protector.Protect(JsonSerializer.Serialize(challenge));

        if (string.IsNullOrWhiteSpace(discovery.AuthorizationEndpoint))
            throw new BadRequestException("OIDC discovery did not return an authorization endpoint.");

        var redirect = QueryHelpers.AddQueryString(discovery.AuthorizationEndpoint, new Dictionary<string, string?>
        {
            ["client_id"] = config.ClientId,
            ["response_type"] = "code",
            ["scope"] = "openid profile email",
            ["redirect_uri"] = GetOidcCallback(),
            ["state"] = protectedState,
            ["code_challenge"] = Base64Url(Sha256(codeVerifier)),
            ["code_challenge_method"] = "S256"
        });
        return new SsoChallengeResult(redirect, protectedState);

    }

    public async Task<AuthResponse> CompleteOidcAsync(SsoOidcCallbackRequest request, CancellationToken cancellationToken = default)
    {
        var challengeJson = _protector.Unprotect(request.State);
        using var challengeDoc = JsonDocument.Parse(challengeJson);
        var configurationId = challengeDoc.RootElement.GetProperty("configurationId").GetGuid();
        var tenantId = challengeDoc.RootElement.GetProperty("tenantId").GetGuid();
        var codeVerifier = challengeDoc.RootElement.GetProperty("codeVerifier").GetString()
            ?? throw new BadRequestException("Invalid SSO state.");

        _tenant.SetTenantId(tenantId);
        var config = await LoadEnabledConfig(configurationId, "Oidc", cancellationToken);
        var discovery = await GetOidcConfigAsync(config.Authority!, cancellationToken);
        var secret = string.IsNullOrWhiteSpace(config.ClientSecretProtected)
            ? null
            : _protector.Unprotect(config.ClientSecretProtected);

        var client = _httpClientFactory.CreateClient("enterprise-sso");
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = request.Code,
            ["redirect_uri"] = GetOidcCallback(),
            ["client_id"] = config.ClientId!,
            ["code_verifier"] = codeVerifier,
            ["client_secret"] = secret ?? string.Empty
        });
        using var tokenResponse = await client.PostAsync(discovery.TokenEndpoint, form, cancellationToken);
        var payload = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!tokenResponse.IsSuccessStatusCode)
            throw new UnauthorizedException("OIDC token exchange failed.");

        using var tokenDoc = JsonDocument.Parse(payload);
        var idToken = tokenDoc.RootElement.GetProperty("id_token").GetString()
            ?? throw new UnauthorizedException("OIDC response missing id_token.");

        var principal = await ValidateIdTokenAsync(idToken, config, discovery, cancellationToken);
        var email = principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
            ?? principal.FindFirst("email")?.Value
            ?? throw new UnauthorizedException("OIDC identity did not include an email claim.");

        EnsureEmailDomainAllowed(config, email);
        var user = await FindOrProvisionAsync(config, email, principal.FindFirst("given_name")?.Value, principal.FindFirst("family_name")?.Value, cancellationToken);
        var session = await _authService.CreateSessionForUserAsync(
            new UserAuthResult(user.Id, user.Email ?? email, user.TenantId),
            cancellationToken,
            "SsoOidcLogin");

        await _audit.LogAsync(new EnterpriseAuditWriteRequest
        {
            Category = EnterpriseAuditCategories.Authentication,
            Action = "SsoOidcLogin",
            ActorUserId = user.Id,
            ActorType = "User",
            EntityType = "User",
            EntityId = user.Id,
            DetailsJson = JsonSerializer.Serialize(new { config.Provider, email })
        }, cancellationToken);

        return session;
    }

    public async Task<SsoChallengeResult> BeginSamlAsync(Guid configurationId, string returnUrl, CancellationToken cancellationToken = default)
    {
        var config = await LoadEnabledConfig(configurationId, "Saml", cancellationToken);
        var metadata = await ResolveSamlMetadataAsync(config, cancellationToken);
        if (string.IsNullOrWhiteSpace(metadata.SsoUrl))
            throw new BadRequestException("SAML metadata did not include a SingleSignOnService URL.");

        var state = _protector.Protect(JsonSerializer.Serialize(new { configurationId, returnUrl, tenantId = config.TenantId }));
        var samlRequest = BuildAuthnRequest(config.SpEntityId!, GetSamlAcs(config.TenantId), metadata.SsoUrl);
        var redirect = QueryHelpers.AddQueryString(metadata.SsoUrl!, new Dictionary<string, string?>
        {
            ["SAMLRequest"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(samlRequest)),
            ["RelayState"] = state
        });
        return new SsoChallengeResult(redirect, state);
    }

    public async Task<AuthResponse> CompleteSamlAsync(SsoSamlCallbackRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RelayState))
            throw new BadRequestException("SAML RelayState is required.");
        var challengeJson = _protector.Unprotect(request.RelayState);
        using var challengeDoc = JsonDocument.Parse(challengeJson);
        var configurationId = challengeDoc.RootElement.GetProperty("configurationId").GetGuid();
        var tenantId = challengeDoc.RootElement.GetProperty("tenantId").GetGuid();
        _tenant.SetTenantId(tenantId);

        var config = await LoadEnabledConfig(configurationId, "Saml", cancellationToken);
        var xml = Encoding.UTF8.GetString(Convert.FromBase64String(request.SamlResponse));
        var assertion = ParseSamlAssertion(xml);
        EnsureEmailDomainAllowed(config, assertion.Email);

        // Validate issuer / audience against configured IdP and SP entity ids.
        if (!string.IsNullOrWhiteSpace(config.IdpEntityId) &&
            !string.Equals(assertion.Issuer, config.IdpEntityId, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedException("SAML issuer mismatch.");
        if (!string.IsNullOrWhiteSpace(config.SpEntityId) &&
            !string.IsNullOrWhiteSpace(assertion.Audience) &&
            !string.Equals(assertion.Audience, config.SpEntityId, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedException("SAML audience mismatch.");
        if (assertion.NotOnOrAfter is DateTime expiry && expiry < DateTime.UtcNow)
            throw new UnauthorizedException("SAML assertion has expired.");

        var user = await FindOrProvisionAsync(config, assertion.Email, assertion.FirstName, assertion.LastName, cancellationToken);
        var session = await _authService.CreateSessionForUserAsync(
            new UserAuthResult(user.Id, user.Email ?? assertion.Email, user.TenantId),
            cancellationToken,
            "SsoSamlLogin");

        await _audit.LogAsync(new EnterpriseAuditWriteRequest
        {
            Category = EnterpriseAuditCategories.Authentication,
            Action = "SsoSamlLogin",
            ActorUserId = user.Id,
            ActorType = "User",
            EntityType = "User",
            EntityId = user.Id,
            DetailsJson = JsonSerializer.Serialize(new { config.Provider, assertion.Email })
        }, cancellationToken);

        return session;
    }

    private async Task<TenantSsoConfiguration> LoadEnabledConfig(Guid id, string protocol, CancellationToken cancellationToken)
    {
        var config = await _db.TenantSsoConfigurations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("SSO configuration was not found.");
        _tenant.SetTenantId(config.TenantId);
        await _guard.EnsureAdvancedSecurityAsync(cancellationToken);
        if (!config.IsEnabled || !string.Equals(config.Protocol, protocol, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException($"SSO {protocol} configuration is not enabled.");
        return config;
    }

    private async Task<TenantEnterpriseSecuritySettings> GetOrCreateSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await _db.TenantEnterpriseSecuritySettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is not null)
            return settings;
        settings = new TenantEnterpriseSecuritySettings { TenantId = _tenant.TenantId };
        _db.TenantEnterpriseSecuritySettings.Add(settings);
        await _db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private async Task<OpenIdConnectConfiguration> GetOidcConfigAsync(string authority, CancellationToken cancellationToken)
    {
        var mgr = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{authority.TrimEnd('/')}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever(_httpClientFactory.CreateClient("enterprise-sso")));
        return await mgr.GetConfigurationAsync(cancellationToken);
    }

    private async Task<System.Security.Claims.ClaimsPrincipal> ValidateIdTokenAsync(
        string idToken,
        TenantSsoConfiguration config,
        OpenIdConnectConfiguration discovery,
        CancellationToken cancellationToken)
    {
        var parameters = new TokenValidationParameters
        {
            ValidIssuer = discovery.Issuer,
            ValidAudience = config.ClientId,
            IssuerSigningKeys = discovery.SigningKeys,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
        var handler = new JwtSecurityTokenHandler();
        return handler.ValidateToken(idToken, parameters, out _);
    }

    private async Task<ApplicationUser> FindOrProvisionAsync(
        TenantSsoConfiguration config,
        string email,
        string? firstName,
        string? lastName,
        CancellationToken cancellationToken)
    {
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (existing.TenantId != config.TenantId)
                throw new ForbiddenException("This account belongs to another company.");
            return existing;
        }

        if (!config.JitProvision)
            throw new UnauthorizedException("JIT provisioning is disabled for this SSO configuration.");

        await _limits.EnsureWithinLimitAsync("Users", cancellationToken);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            TenantId = config.TenantId,
            FirstName = firstName ?? email.Split('@')[0],
            LastName = lastName ?? "User",
            IsActive = true,
            SecurityVersion = 1
        };
        var created = await _userManager.CreateAsync(user);
        if (!created.Succeeded)
            throw new BadRequestException(string.Join(", ", created.Errors.Select(x => x.Description)));

        var roleName = string.IsNullOrWhiteSpace(config.DefaultRoleName) ? "Employee" : config.DefaultRoleName!;
        var role = await _db.RbacRoles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Name == roleName && (x.IsSystem || x.TenantId == config.TenantId), cancellationToken);
        if (role is not null)
            await _roleService.AssignUserRoleAsync(user.Id, role.Id, cancellationToken);

        return user;
    }

    private static void EnsureEmailDomainAllowed(TenantSsoConfiguration config, string email)
    {
        if (string.IsNullOrWhiteSpace(config.AllowedEmailDomains))
            return;
        var domain = email.Split('@').LastOrDefault()?.Trim().ToLowerInvariant();
        var allowed = config.AllowedEmailDomains.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant());
        if (domain is null || !allowed.Contains(domain))
            throw new ForbiddenException("Email domain is not allowed for this SSO configuration.");
    }

    private async Task<SamlMetadata> ResolveSamlMetadataAsync(TenantSsoConfiguration config, CancellationToken cancellationToken)
    {
        string xml;
        if (!string.IsNullOrWhiteSpace(config.IdpMetadataXml))
            xml = config.IdpMetadataXml!;
        else
        {
            var client = _httpClientFactory.CreateClient("enterprise-sso");
            xml = await client.GetStringAsync(config.MetadataUrl, cancellationToken);
        }

        var doc = new System.Xml.XmlDocument { XmlResolver = null };
        doc.LoadXml(xml);
        var nsmgr = new System.Xml.XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("md", "urn:oasis:names:tc:SAML:2.0:metadata");
        var sso = doc.SelectSingleNode("//md:IDPSSODescriptor/md:SingleSignOnService[@Binding='urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect']/@Location", nsmgr)
            ?? doc.SelectSingleNode("//md:IDPSSODescriptor/md:SingleSignOnService/@Location", nsmgr);
        var entity = doc.DocumentElement?.GetAttribute("entityID");
        if (!string.IsNullOrWhiteSpace(entity) && string.IsNullOrWhiteSpace(config.IdpEntityId))
            config.IdpEntityId = entity;
        return new SamlMetadata(sso?.Value, entity);
    }

    private static string BuildAuthnRequest(string spEntityId, string acsUrl, string destination)
    {
        var id = "_" + Guid.NewGuid().ToString("N");
        var issueInstant = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        return $"""
            <samlp:AuthnRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion" ID="{id}" Version="2.0" IssueInstant="{issueInstant}" Destination="{System.Security.SecurityElement.Escape(destination)}" AssertionConsumerServiceURL="{System.Security.SecurityElement.Escape(acsUrl)}" ProtocolBinding="urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST">
              <saml:Issuer>{System.Security.SecurityElement.Escape(spEntityId)}</saml:Issuer>
            </samlp:AuthnRequest>
            """;
    }

    private static SamlAssertion ParseSamlAssertion(string xml)
    {
        var doc = new System.Xml.XmlDocument { XmlResolver = null };
        doc.LoadXml(xml);
        var nsmgr = new System.Xml.XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("saml", "urn:oasis:names:tc:SAML:2.0:assertion");
        nsmgr.AddNamespace("samlp", "urn:oasis:names:tc:SAML:2.0:protocol");

        var issuer = doc.SelectSingleNode("//saml:Issuer", nsmgr)?.InnerText;
        var audience = doc.SelectSingleNode("//saml:Audience", nsmgr)?.InnerText;
        var notOnOrAfterText = doc.SelectSingleNode("//saml:Conditions/@NotOnOrAfter", nsmgr)?.Value;
        DateTime? notOnOrAfter = DateTime.TryParse(notOnOrAfterText, out var parsed) ? parsed.ToUniversalTime() : null;
        var email = doc.SelectSingleNode("//saml:Attribute[@Name='email' or @Name='http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress']/saml:AttributeValue", nsmgr)?.InnerText
            ?? doc.SelectSingleNode("//saml:NameID", nsmgr)?.InnerText
            ?? throw new UnauthorizedException("SAML assertion missing email/NameID.");
        var first = doc.SelectSingleNode("//saml:Attribute[@Name='given_name' or @Name='http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname']/saml:AttributeValue", nsmgr)?.InnerText;
        var last = doc.SelectSingleNode("//saml:Attribute[@Name='family_name' or @Name='http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname']/saml:AttributeValue", nsmgr)?.InnerText;
        return new SamlAssertion(email, first, last, issuer, audience, notOnOrAfter);
    }

    private string GetApiBase() =>
        (_configuration["App:ApiBaseUrl"] ?? _configuration["App:FrontendBaseUrl"] ?? "https://localhost").TrimEnd('/');

    private string GetOidcCallback() => $"{GetApiBase()}/api/v1/enterprise/sso/oidc/callback";
    private string GetSamlAcs(Guid tenantId) => $"{GetApiBase()}/api/v1/enterprise/sso/saml/{tenantId}/acs";

    private static string CreateState(Guid configurationId, string returnUrl, out string codeVerifier)
    {
        codeVerifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        return configurationId.ToString("N");
    }

    private static byte[] Sha256(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static TenantSsoConfigurationDto Map(TenantSsoConfiguration x) =>
        new(x.Id, x.Protocol, x.Provider, x.IsEnabled, x.DisplayName, x.Authority, x.ClientId, x.AllowedEmailDomains,
            x.MetadataUrl, x.IdpEntityId, x.SpEntityId, x.DefaultRoleName, x.JitProvision,
            !string.IsNullOrWhiteSpace(x.ClientSecretProtected));

    private sealed record SamlMetadata(string? SsoUrl, string? EntityId);
    private sealed record SamlAssertion(string Email, string? FirstName, string? LastName, string? Issuer, string? Audience, DateTime? NotOnOrAfter);
}

public sealed class EnterpriseMfaService : IEnterpriseMfaService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthService _authService;
    private readonly IDataProtector _protector;
    private readonly BusinessOSDbContext _db;
    private readonly IEnterpriseAuditService _audit;
    private readonly ITenantProvider _tenant;

    public EnterpriseMfaService(
        UserManager<ApplicationUser> userManager,
        ICurrentUserService currentUser,
        IAuthService authService,
        IDataProtectionProvider dataProtection,
        BusinessOSDbContext db,
        IEnterpriseAuditService audit,
        ITenantProvider tenant)
    {
        _userManager = userManager;
        _currentUser = currentUser;
        _authService = authService;
        _protector = dataProtection.CreateProtector("BusinessOS.Enterprise.MfaChallenge");
        _db = db;
        _audit = audit;
        _tenant = tenant;
    }

    public async Task<MfaSetupDto> BeginSetupAsync(CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync();
        var key = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            key = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        var email = user.Email ?? user.UserName ?? user.Id;
        var uri = $"otpauth://totp/BusinessOS:{Uri.EscapeDataString(email)}?secret={key}&issuer=BusinessOS&digits=6";
        return new MfaSetupDto(key!, uri);
    }

    public async Task EnableAsync(string code, CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync();
        var valid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider,
            code);
        if (!valid)
            throw new BadRequestException("Invalid authenticator code.");

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        await _audit.LogAsync(new EnterpriseAuditWriteRequest
        {
            Category = EnterpriseAuditCategories.Authentication,
            Action = "MfaEnabled",
            ActorUserId = user.Id,
            ActorType = "User",
            EntityType = "User",
            EntityId = user.Id
        }, cancellationToken);
    }

    public async Task DisableAsync(string code, CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync();
        var valid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider,
            code);
        if (!valid)
            throw new BadRequestException("Invalid authenticator code.");
        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _audit.LogAsync(new EnterpriseAuditWriteRequest
        {
            Category = EnterpriseAuditCategories.Authentication,
            Action = "MfaDisabled",
            ActorUserId = user.Id,
            ActorType = "User",
            EntityType = "User",
            EntityId = user.Id
        }, cancellationToken);
    }

    public async Task<AuthResponse> VerifyLoginAsync(string mfaToken, string code, CancellationToken cancellationToken = default)
    {
        var json = _protector.Unprotect(mfaToken);
        using var doc = JsonDocument.Parse(json);
        var userId = doc.RootElement.GetProperty("userId").GetString()
            ?? throw new UnauthorizedException("Invalid MFA token.");
        var expires = doc.RootElement.GetProperty("expires").GetDateTime();
        if (expires < DateTime.UtcNow)
            throw new UnauthorizedException("MFA challenge expired.");

        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new UnauthorizedException("Invalid MFA token.");
        _tenant.SetTenantId(user.TenantId);

        var valid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider,
            code);
        if (!valid)
            throw new UnauthorizedException("Invalid authenticator code.");

        return await _authService.CreateSessionForUserAsync(
            new UserAuthResult(user.Id, user.Email ?? string.Empty, user.TenantId),
            cancellationToken,
            "MfaLogin");
    }

    public string CreateChallengeToken(ApplicationUser user)
    {
        var payload = JsonSerializer.Serialize(new
        {
            userId = user.Id,
            expires = DateTime.UtcNow.AddMinutes(5)
        });
        return _protector.Protect(payload);
    }

    private async Task<ApplicationUser> RequireUserAsync()
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException("Authentication required.");
        return await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("User was not found.");
    }
}

public sealed class EnterpriseAgentGovernanceService : IEnterpriseAgentGovernanceService
{
    private readonly BusinessOSDbContext _db;
    private readonly IEnterpriseEntitlementGuard _guard;
    private readonly IEnterpriseAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public EnterpriseAgentGovernanceService(
        BusinessOSDbContext db,
        IEnterpriseEntitlementGuard guard,
        IEnterpriseAuditService audit,
        ICurrentUserService currentUser)
    {
        _db = db;
        _guard = guard;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<AgentGovernanceDto> GetAsync(string agentKey, CancellationToken cancellationToken = default)
    {
        await _guard.EnsureAdvancedSecurityAsync(cancellationToken);
        var agent = await _db.NalvaCompanyAgents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.AgentKey == agentKey, cancellationToken)
            ?? throw new NotFoundException("Agent was not found.");
        return Map(agent);
    }

    public async Task<AgentGovernanceDto> SaveAsync(string agentKey, AgentGovernanceWriteRequest request, CancellationToken cancellationToken = default)
    {
        await _guard.EnsureAdvancedSecurityAsync(cancellationToken);
        var agent = await _db.NalvaCompanyAgents.FirstOrDefaultAsync(x => x.AgentKey == agentKey, cancellationToken)
            ?? throw new NotFoundException("Agent was not found.");

        agent.DepartmentId = request.DepartmentId;
        agent.BranchId = request.BranchId;
        agent.DeniedToolsJson = JsonSerializer.Serialize(request.DeniedTools ?? []);
        agent.SensitiveDataRestrictionsJson = JsonSerializer.Serialize(request.SensitiveDataRestrictions ?? []);
        agent.RequireHumanSupervision = request.RequireHumanSupervision;
        if (request.DailyExecutionLimit is int daily)
            agent.DailyExecutionLimit = Math.Max(0, daily);
        if (request.MonthlyTokenBudget is int monthly)
            agent.MonthlyTokenBudget = Math.Max(0, monthly);
        agent.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(new EnterpriseAuditWriteRequest
        {
            Category = EnterpriseAuditCategories.Agent,
            Action = "AgentGovernanceUpdated",
            ActorUserId = _currentUser.UserId,
            ActorType = "User",
            EntityType = nameof(NalvaCompanyAgent),
            EntityId = agent.Id.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { agent.AgentKey, request.DeniedTools, request.RequireHumanSupervision })
        }, cancellationToken);

        return Map(agent);
    }

    public async Task EnsureToolAllowedAsync(string agentKey, string toolName, Guid? departmentId, CancellationToken cancellationToken = default)
    {
        var agent = await _db.NalvaCompanyAgents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.AgentKey == agentKey, cancellationToken);
        if (agent is null)
            return;

        if (agent.DepartmentId is Guid allowedDept && departmentId is Guid requested && allowedDept != requested)
            throw new ForbiddenException("Agent is restricted to another department.");

        var denied = ParseList(agent.DeniedToolsJson);
        if (denied.Contains(toolName, StringComparer.OrdinalIgnoreCase))
            throw new ForbiddenException($"Tool '{toolName}' is denied by enterprise agent governance.");

        if (agent.RequireHumanSupervision)
        {
            // Supervision is enforced by requiring approval policies / Nalva approval path for write tools.
        }

        await _audit.LogAsync(new EnterpriseAuditWriteRequest
        {
            Category = EnterpriseAuditCategories.Agent,
            Action = "AgentToolAuthorized",
            ActorType = "Agent",
            EntityType = "Tool",
            EntityId = toolName,
            DetailsJson = JsonSerializer.Serialize(new { agentKey, toolName, departmentId })
        }, cancellationToken);
    }

    private static AgentGovernanceDto Map(NalvaCompanyAgent agent) =>
        new(agent.AgentKey, agent.DepartmentId, agent.BranchId, ParseList(agent.DeniedToolsJson),
            ParseList(agent.SensitiveDataRestrictionsJson), agent.RequireHumanSupervision,
            agent.DailyExecutionLimit, agent.MonthlyTokenBudget);

    private static IReadOnlyList<string> ParseList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
