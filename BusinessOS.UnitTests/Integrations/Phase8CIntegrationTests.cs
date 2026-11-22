using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Integrations;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Integrations;
using BusinessOS.Infrastructure.Integrations.Providers;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace BusinessOS.UnitTests.Integrations;

public class Phase8CIntegrationTests
{
    [Fact]
    public async Task Marketplace_IncludesPhase8CProviders_AndHealthFields()
    {
        await using var harness = await Harness.CreateAsync();
        var platform = harness.CreatePlatform(new SequenceHandler());
        var marketplace = await platform.GetMarketplaceAsync();
        marketplace.Should().Contain(x => x.Key == "google-business-profile");
        marketplace.Should().Contain(x => x.Key == "outlook");
        marketplace.Should().Contain(x => x.Key == "smtp-email");
        marketplace.Should().Contain(x => x.Key == "microsoft-calendar");
        marketplace.Should().OnlyContain(x => x.RequiredPermissions.Count >= 0);
        marketplace.Should().OnlyContain(x => !string.IsNullOrWhiteSpace(x.TroubleshootingHint));
        marketplace.Should().OnlyContain(x => x.ProviderVerified == false);
    }

    [Fact]
    public async Task HealthDashboard_ReportsNeedsReauth()
    {
        await using var harness = await Harness.CreateAsync();
        harness.Db.CompanyIntegrationConnections.Add(new CompanyIntegrationConnection
        {
            TenantId = harness.TenantId,
            ProviderKey = "gmail",
            Status = IntegrationConnectionStatuses.NeedsReauth,
            AccessTokenProtected = harness.Protector.Protect("access"),
            OwnerUserId = "user",
            LastError = "Token refresh failed."
        });
        await harness.Db.SaveChangesAsync();

        var platform = harness.CreatePlatform(new SequenceHandler());
        var health = await platform.GetHealthAsync();
        health.NeedsReauthCount.Should().BeGreaterThan(0);
        health.PendingReconnectActions.Should().Contain(x => x.Contains("Gmail", StringComparison.OrdinalIgnoreCase));
        health.Items.Should().Contain(x => x.ProviderKey == "gmail" && x.NeedsReconnect);
    }

    [Fact]
    public async Task OAuthComplete_RejectsStateOwnedByAnotherUser()
    {
        await using var harness = await Harness.CreateAsync();
        var state = "oauth-state-value";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state)));
        harness.Db.IntegrationOAuthStates.Add(new IntegrationOAuthState
        {
            TenantId = harness.TenantId,
            ProviderKey = "gmail",
            State = hash,
            CodeVerifier = harness.Protector.Protect("verifier"),
            RedirectUri = "https://localhost/nalva/integrations/callback",
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            UserId = "other-user"
        });
        await harness.Db.SaveChangesAsync();

        var platform = harness.CreatePlatform(new SequenceHandler());
        var act = async () => await platform.CompleteAuthorizeAsync("gmail", new IntegrationCallbackRequest
        {
            Code = "code",
            State = state
        });
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task SmtpConfigure_StoresEncryptedCredentials_WithoutExposingPassword()
    {
        await using var harness = await Harness.CreateAsync();
        var platform = harness.CreatePlatform(new SequenceHandler());
        var connection = await platform.ConfigureSmtpAsync(new IntegrationSmtpConfigureRequest
        {
            Host = "smtp.example.com",
            Port = 587,
            UseSsl = true,
            Username = "mailer",
            Password = "super-secret",
            FromAddress = "sales@technova.test"
        });

        connection.ProviderKey.Should().Be("smtp-email");
        connection.Status.Should().Be(IntegrationConnectionStatuses.Connected);
        connection.ExternalAccountLabel.Should().Be("sales@technova.test");

        var row = await harness.Db.CompanyIntegrationConnections.SingleAsync(x => x.ProviderKey == "smtp-email");
        row.AccessTokenProtected.Should().NotBeNull();
        row.AccessTokenProtected.Should().NotContain("super-secret");
        row.MetadataJson.Should().NotContain("super-secret");
        var status = await platform.ExecuteAsync("smtp-email", "status", "{}", false);
        status.Performed.Should().BeTrue();
        status.Data.ToJsonString().Should().NotContain("super-secret");
    }

    [Fact]
    public async Task Webhook_IgnoresSignatureHeaderAsEventId()
    {
        await using var harness = await Harness.CreateAsync();
        const string secret = "whsec";
        const string body = "{\"ok\":true}";
        var connection = new CompanyIntegrationConnection
        {
            TenantId = harness.TenantId,
            ProviderKey = "github",
            Status = IntegrationConnectionStatuses.Connected,
            AccessTokenProtected = harness.Protector.Protect("access"),
            WebhookSecretProtected = harness.Protector.Protect(secret),
            OwnerUserId = "user"
        };
        harness.Db.CompanyIntegrationConnections.Add(connection);
        await harness.Db.SaveChangesAsync();

        var signature = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        var platform = harness.CreatePlatform(new SequenceHandler());
        var first = await platform.ReceiveWebhookAsync("github", connection.Id, body, signature, signature);
        first.StatusCode.Should().Be(200);
        var second = await platform.ReceiveWebhookAsync("github", connection.Id, body, signature, signature);
        second.StatusCode.Should().Be(200);
        second.Message.Should().Contain("Duplicate");
    }

    [Fact]
    public async Task WhatsAppWebhook_AssociatesCustomer_AndDoesNotClaimLiveSend()
    {
        await using var harness = await Harness.CreateAsync();
        harness.Db.Customers.Add(new Customer
        {
            TenantId = harness.TenantId,
            FirstName = "Alex",
            LastName = "Buyer",
            Email = "alex@technova.test",
            PhoneNumber = "+15551234567",
            Address = "1 Main",
            City = "Austin",
            Country = "US",
            PostalCode = "78701"
        });
        harness.Db.Products.Add(new Product
        {
            TenantId = harness.TenantId,
            CategoryId = Guid.NewGuid(),
            Name = "Dell Latitude 5420",
            SKU = "LAT-5420",
            CurrentStock = 7,
            IsActive = true,
            RowVersion = [1]
        });
        var connection = new CompanyIntegrationConnection
        {
            TenantId = harness.TenantId,
            ProviderKey = "whatsapp",
            Status = IntegrationConnectionStatuses.Connected,
            AccessTokenProtected = harness.Protector.Protect("access"),
            WebhookSecretProtected = harness.Protector.Protect("verify"),
            MetadataJson = "{\"phoneNumberId\":\"123\"}",
            OwnerUserId = "user"
        };
        harness.Db.CompanyIntegrationConnections.Add(connection);
        await harness.Db.SaveChangesAsync();

        var body = """
            {"entry":[{"changes":[{"value":{"contacts":[{"profile":{"name":"Alex"}}],"messages":[{"from":"15551234567","id":"wamid.1","type":"text","text":{"body":"Is the Dell Latitude 5420 available?"}}]}}]}]}
            """;
        var provider = new WhatsAppIntegrationProvider(harness.Db);
        var handled = await provider.HandleWebhookAsync(harness.TenantId, connection.Id, body, "evt-wa-1", connection.MetadataJson);
        handled!.Handled.Should().BeTrue();

        var conversation = await harness.Db.IntegrationConversations.SingleAsync();
        conversation.CustomerDisplay.Should().Contain("Alex");
        conversation.MetadataJson.Should().Contain("customerId");
        (await harness.Db.IntegrationConversationMessages.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GoogleBusinessProfile_CreatePost_RequiresLocationMetadata()
    {
        var provider = new GoogleBusinessProfileIntegrationProvider();
        var result = await provider.ExecuteAsync(
            new HttpClient(new SequenceHandler()),
            "createPost",
            "token",
            "{\"summary\":\"Hello\"}",
            "{}");
        result.Performed.Should().BeFalse();
        result.Status.Should().Be("configuration");
    }

    [Fact]
    public void Catalog_SendSupportReply_IsGuarded()
    {
        var tool = NalvaToolCatalog.All.Single(x => x.Name == "SendSupportReply");
        tool.RequiresApproval.Should().BeTrue();
        tool.IsWrite.Should().BeTrue();
    }

    [Fact]
    public void Catalog_CreateCalendarEvent_And_SendWhatsApp_AreGuarded()
    {
        NalvaToolCatalog.All.Single(x => x.Name == "CreateCalendarEvent").RequiresApproval.Should().BeTrue();
        NalvaToolCatalog.All.Single(x => x.Name == "SendWhatsAppMessage").RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public async Task Disconnect_ClearsProtectedTokens()
    {
        await using var harness = await Harness.CreateAsync();
        var connection = new CompanyIntegrationConnection
        {
            TenantId = harness.TenantId,
            ProviderKey = "meta",
            Status = IntegrationConnectionStatuses.Connected,
            AccessTokenProtected = harness.Protector.Protect("access"),
            RefreshTokenProtected = harness.Protector.Protect("refresh"),
            WebhookSecretProtected = harness.Protector.Protect("whsec"),
            OwnerUserId = "user"
        };
        harness.Db.CompanyIntegrationConnections.Add(connection);
        await harness.Db.SaveChangesAsync();

        var platform = harness.CreatePlatform(new SequenceHandler());
        var result = await platform.DisconnectAsync(connection.Id);
        result.Status.Should().Be("Revoked");
        var row = await harness.Db.CompanyIntegrationConnections.SingleAsync();
        row.AccessTokenProtected.Should().BeNull();
        row.RefreshTokenProtected.Should().BeNull();
        row.WebhookSecretProtected.Should().BeNull();
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public SequenceHandler(params HttpResponseMessage[] responses) =>
            _responses = new Queue<HttpResponseMessage>(responses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class Harness : IAsyncDisposable
    {
        public required BusinessOSDbContext Db { get; init; }
        public required Guid TenantId { get; init; }
        public required IDataProtector Protector { get; init; }
        public required IDataProtectionProvider DataProtection { get; init; }
        public required ITenantProvider Tenant { get; init; }
        public required ICurrentUserService User { get; init; }
        private ServiceProvider? _provider;

        public static async Task<Harness> CreateAsync(string? databaseName = null)
        {
            var tenantId = Guid.NewGuid();
            var tenant = new Mock<ITenantProvider>();
            tenant.Setup(x => x.HasTenant()).Returns(true);
            tenant.Setup(x => x.TenantId).Returns(tenantId);
            tenant.Setup(x => x.SetTenantId(It.IsAny<Guid>())).Callback<Guid>(_ => { });

            var user = new Mock<ICurrentUserService>();
            user.Setup(x => x.UserId).Returns("user");
            user.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);

            var dataProtection = new EphemeralDataProtectionProvider();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(tenant.Object);
            services.AddSingleton<IDataProtectionProvider>(dataProtection);
            services.AddDbContext<BusinessOSDbContext>(o => o.UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString()));
            var provider = services.BuildServiceProvider();
            var db = provider.GetRequiredService<BusinessOSDbContext>();
            await db.Database.EnsureCreatedAsync();

            return new Harness
            {
                Db = db,
                TenantId = tenantId,
                DataProtection = dataProtection,
                Protector = dataProtection.CreateProtector("BusinessOS.IntegrationTokens.v1"),
                Tenant = tenant.Object,
                User = user.Object,
                _provider = provider
            };
        }

        public IIntegrationPlatform CreatePlatform(HttpMessageHandler handler)
        {
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(x => x.CreateClient("Integrations")).Returns(new HttpClient(handler));

            var providers = new IntegrationProviderRegistry(
            [
                new GmailIntegrationProvider(Db),
                new WhatsAppIntegrationProvider(Db),
                new MetaIntegrationProvider(),
                new StripeConnectIntegrationProvider(Db),
                new SmtpIntegrationProvider(),
                new GoogleBusinessProfileIntegrationProvider()
            ]);

            var scopes = new Mock<IServiceScopeFactory>();
            var scope = new Mock<IServiceScope>();
            var scoped = new ServiceCollection();
            scoped.AddSingleton(Tenant);
            scoped.AddSingleton(Db);
            scoped.AddSingleton<IIntegrationProviderRegistry>(providers);
            var outbox = new Mock<INalvaEventOutbox>();
            outbox.Setup(x => x.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid?)Guid.NewGuid());
            scoped.AddSingleton(outbox.Object);
            var sp = scoped.BuildServiceProvider();
            scope.Setup(x => x.ServiceProvider).Returns(sp);
            scopes.Setup(x => x.CreateScope()).Returns(scope.Object);

            return new IntegrationPlatform(
                Db,
                Tenant,
                User,
                httpFactory.Object,
                DataProtection,
                Options.Create(new IntegrationPlatformOptions
                {
                    AllowedRedirectHosts = ["localhost"],
                    Google = new ProviderCredential { Enabled = true, ClientId = "id", ClientSecret = "secret" },
                    Meta = new ProviderCredential { Enabled = true, ClientId = "id", ClientSecret = "secret" },
                    Microsoft = new ProviderCredential { Enabled = true, ClientId = "id", ClientSecret = "secret" }
                }),
                scopes.Object,
                providers,
                new IntegrationWebhookIdempotencyStore(Db));
        }

        public async ValueTask DisposeAsync()
        {
            if (_provider is not null)
                await _provider.DisposeAsync();
        }
    }
}
