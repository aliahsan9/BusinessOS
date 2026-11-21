using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Integrations;
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

public class Phase6BIntegrationTests
{
    [Fact]
    public void GmailMime_UsesBase64UrlWithoutPadding()
    {
        var raw = GmailMime.EncodeRawMessage("a@example.com", "Hello", "Body");
        raw.Should().NotContain("+").And.NotContain("/").And.NotContain("=");
        Convert.FromBase64String(Pad(raw)).Should().NotBeEmpty();
    }

    [Fact]
    public async Task CatalogProvider_RetriesOn429_ThenSucceeds()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("{\"error\":\"rate\"}") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"msg-1\"}") });
        var http = new HttpClient(handler);
        var provider = new CatalogIntegrationProvider("gmail");
        var result = await provider.ExecuteAsync(http, "list", "token", "{}", null);
        result.Performed.Should().BeTrue();
        result.Data["attempts"]!.GetValue<int>().Should().Be(2);
    }

    [Fact]
    public async Task WebhookIdempotency_RejectsDuplicates()
    {
        await using var harness = await Harness.CreateAsync();
        var store = new IntegrationWebhookIdempotencyStore(harness.Db);
        var first = await store.TryBeginAsync("stripe", "evt_1", harness.TenantId);
        first.Should().BeOfType<IntegrationWebhookClaim.Started>();
        await store.CompleteAsync(((IntegrationWebhookClaim.Started)first).ReceiptId);
        var second = await store.TryBeginAsync("stripe", "evt_1", harness.TenantId);
        second.Should().BeOfType<IntegrationWebhookClaim.Duplicate>();
    }

    [Fact]
    public async Task Execute_WriteWithoutApproval_RequiresApproval()
    {
        await using var harness = await Harness.CreateAsync();
        var platform = harness.CreatePlatform(new SequenceHandler());
        var result = await platform.ExecuteAsync("gmail", "send", "{\"to\":\"a@b.com\",\"subject\":\"x\",\"body\":\"y\"}", false);
        result.ApprovalRequired.Should().BeTrue();
        result.Status.Should().Be("approval_required");
        result.Performed.Should().BeFalse();
    }

    [Fact]
    public async Task Execute_TenantIsolation_UsesOnlyCurrentTenantConnection()
    {
        var database = Guid.NewGuid().ToString("N");
        await using var other = await Harness.CreateAsync(database);
        other.Db.CompanyIntegrationConnections.Add(new CompanyIntegrationConnection
        {
            TenantId = other.TenantId,
            ProviderKey = "gmail",
            Status = IntegrationConnectionStatuses.Connected,
            AccessTokenProtected = other.Protector.Protect("other-token"),
            OwnerUserId = "other"
        });
        await other.Db.SaveChangesAsync();

        await using var harness = await Harness.CreateAsync(database);
        var platform = harness.CreatePlatform(new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"messages\":[]}") }));
        var result = await platform.ExecuteAsync("gmail", "list", "{}", false);
        result.Status.Should().Be("not_connected");
    }

    [Fact]
    public async Task TokenRefreshFailure_SetsNeedsReauth()
    {
        await using var harness = await Harness.CreateAsync();
        harness.Db.CompanyIntegrationConnections.Add(new CompanyIntegrationConnection
        {
            TenantId = harness.TenantId,
            ProviderKey = "gmail",
            Status = IntegrationConnectionStatuses.Connected,
            AccessTokenProtected = harness.Protector.Protect("access"),
            RefreshTokenProtected = harness.Protector.Protect("refresh"),
            TokenExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            OwnerUserId = "user"
        });
        await harness.Db.SaveChangesAsync();

        var platform = harness.CreatePlatform(new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{\"error\":\"invalid_grant\"}") }));
        var result = await platform.ExecuteAsync("gmail", "list", "{}", false);
        result.Status.Should().Be("needs_reauth");
        var connection = await harness.Db.CompanyIntegrationConnections.SingleAsync();
        connection.Status.Should().Be(IntegrationConnectionStatuses.NeedsReauth);
    }

    [Fact]
    public async Task Webhook_InvalidSignature_IsRejected()
    {
        await using var harness = await Harness.CreateAsync();
        var connection = new CompanyIntegrationConnection
        {
            TenantId = harness.TenantId,
            ProviderKey = "github",
            Status = IntegrationConnectionStatuses.Connected,
            AccessTokenProtected = harness.Protector.Protect("access"),
            WebhookSecretProtected = harness.Protector.Protect("whsec"),
            OwnerUserId = "user"
        };
        harness.Db.CompanyIntegrationConnections.Add(connection);
        await harness.Db.SaveChangesAsync();

        var platform = harness.CreatePlatform(new SequenceHandler());
        var response = await platform.ReceiveWebhookAsync("github", connection.Id, "{\"ok\":true}", "sha256=deadbeef", "evt-1");
        response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Webhook_ValidSignature_IsIdempotent()
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
        var first = await platform.ReceiveWebhookAsync("github", connection.Id, body, signature, "evt-dup");
        first.StatusCode.Should().Be(200);
        var second = await platform.ReceiveWebhookAsync("github", connection.Id, body, signature, "evt-dup");
        second.StatusCode.Should().Be(200);
        second.Message.Should().Contain("Duplicate");
    }

    [Fact]
    public async Task WhatsApp_SendText_OutsideWindow_IsBlocked()
    {
        await using var harness = await Harness.CreateAsync();
        var provider = new WhatsAppIntegrationProvider(harness.Db);
        var result = await provider.ExecuteAsync(
            new HttpClient(new SequenceHandler()),
            "sendText",
            "token",
            "{\"to\":\"15551234567\",\"body\":\"hi\"}",
            "{\"phoneNumberId\":\"123\"}");
        result.Performed.Should().BeFalse();
        result.Status.Should().Be("policy");
    }

    [Fact]
    public async Task Meta_PublishInstagram_RequiresMetadata()
    {
        var provider = new MetaIntegrationProvider();
        var result = await provider.ExecuteAsync(
            new HttpClient(new SequenceHandler()),
            "publishInstagram",
            "token",
            "{\"imageUrl\":\"https://example.com/a.jpg\",\"caption\":\"hi\"}",
            "{}");
        result.Performed.Should().BeFalse();
        result.Status.Should().Be("configuration");
    }

    [Fact]
    public async Task StripeWebhook_UpsertsCompanyPayment()
    {
        await using var harness = await Harness.CreateAsync();
        var provider = new StripeConnectIntegrationProvider(harness.Db);
        var body = """
            {"type":"payment_intent.succeeded","data":{"object":{"id":"pi_123","status":"succeeded","amount":5000,"currency":"usd","customer":"cus_1"}}}
            """;
        var handled = await provider.HandleWebhookAsync(harness.TenantId, Guid.NewGuid(), body, "evt_pay_1", null);
        handled!.Handled.Should().BeTrue();
        var row = await harness.Db.CompanyPaymentRecords.SingleAsync();
        row.ExternalPaymentId.Should().Be("pi_123");
        row.Status.Should().Be("succeeded");
        row.AmountMinor.Should().Be(5000);
    }

    private static string Pad(string base64Url)
    {
        var s = base64Url.Replace('-', '+').Replace('_', '/');
        return s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
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
                new StripeConnectIntegrationProvider(Db)
            ]);

            var scopes = new Mock<IServiceScopeFactory>();
            var scope = new Mock<IServiceScope>();
            var scoped = new ServiceCollection();
            scoped.AddSingleton(Tenant);
            scoped.AddSingleton(Db);
            scoped.AddSingleton<IIntegrationProviderRegistry>(providers);
            var outbox = new Mock<BusinessOS.Application.Features.Nalva.INalvaEventOutbox>();
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
                    Google = new ProviderCredential { Enabled = true, ClientId = "id", ClientSecret = "secret" }
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
