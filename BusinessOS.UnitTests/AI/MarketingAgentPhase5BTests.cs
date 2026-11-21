using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Application.Features.Marketing;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.MultiTenancy;
using BusinessOS.Infrastructure.Services.Marketing;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BusinessOS.UnitTests.AI;

public class MarketingAgentPhase5BTests
{
    [Fact]
    public async Task MarketingAgent_E2E_AcceptanceFlow()
    {
        var harness = Harness.Open();

        var brand = await harness.Marketing.GetOrSeedBrandProfileAsync();
        brand.BusinessName.Should().Be("Phase5B Co");
        brand.Industry.Should().Be("software-agency");
        brand.SourceNotes.Should().Contain("Tenant");
        brand.BrandColorsJson.Should().Contain("#112233");

        var strategy = await harness.Marketing.GenerateStrategyAsync(new GenerateStrategyRequest
        {
            Title = "Launch week",
            Objective = "BrandAwareness",
            AgentKey = "marketing"
        });
        strategy.StrategyBody.Should().NotBeNullOrWhiteSpace();
        strategy.StrategyApproved.Should().BeFalse();

        var calendar = await harness.Marketing.GenerateContentCalendarAsync(new GenerateContentCalendarRequest
        {
            CampaignId = strategy.Id,
            Days = 7,
            Platforms = ["LinkedIn", "Facebook"],
            AgentKey = "marketing"
        });
        calendar.Should().HaveCount(7);

        var content = await harness.Marketing.GenerateContentItemAsync(new GenerateContentItemRequest
        {
            CampaignId = strategy.Id,
            Platform = "LinkedIn",
            Brief = "Announce launch week using only brand facts",
            AgentKey = "social"
        });
        content.Platform.Should().Be("LinkedIn");
        content.ApprovalStatus.Should().Be("Draft");

        var graphic = await harness.Marketing.GenerateGraphicAsync(new GenerateGraphicRequest
        {
            ContentItemId = content.Id,
            CampaignId = strategy.Id,
            Platform = "LinkedIn",
            Brief = "Launch graphic brief"
        });
        graphic.Source.Should().BeOneOf("BriefOnly", "OpenAiImage");
        graphic.BrandCompliant.Should().BeTrue();

        content = await harness.Marketing.UpdateContentItemAsync(content.Id, new UpdateContentItemRequest
        {
            Caption = "Edited caption for approval",
            Body = content.Body + " Edited."
        });
        content.ApprovalStatus.Should().Be("Draft");

        content = await harness.Marketing.SubmitForReviewAsync(content.Id);
        content.ApprovalStatus.Should().Be("Review");
        var fingerprint = content.ApprovalFingerprint;
        fingerprint.Should().NotBeNullOrWhiteSpace();

        content = await harness.Marketing.ApproveContentAsync(content.Id, fingerprint);
        content.ApprovalStatus.Should().Be("Approved");

        var job = await harness.Marketing.ScheduleApprovedContentAsync(content.Id, new ScheduleContentRequest
        {
            RunAtUtc = DateTime.UtcNow.AddMinutes(-1),
            ConnectedAccountProvider = "sandbox",
            ConnectedAccountExternalId = "sandbox",
            IdempotencyKey = "phase5b-publish-1"
        });
        job.Status.Should().Be("Pending");
        job.Provider.Should().Be("sandbox");

        var duplicate = await harness.Marketing.ScheduleApprovedContentAsync(content.Id, new ScheduleContentRequest
        {
            RunAtUtc = DateTime.UtcNow.AddMinutes(-1),
            ConnectedAccountProvider = "sandbox",
            ConnectedAccountExternalId = "sandbox",
            IdempotencyKey = "phase5b-publish-1"
        });
        duplicate.Id.Should().Be(job.Id);

        var processed = await harness.Marketing.ProcessDuePublishJobsAsync();
        processed.Succeeded.Should().BeGreaterThan(0);

        var published = await harness.Marketing.GetContentAsync(content.Id);
        published.ApprovalStatus.Should().Be("Published");
        published.ExternalPostId.Should().StartWith("sandbox-");

        var sync = await harness.Marketing.SyncAnalyticsAsync(new SyncAnalyticsRequest
        {
            ContentItemId = published.Id,
            AllowSandbox = true
        });
        sync.DataSource.Should().Be("Sandbox");
        sync.Snapshots.Should().NotBeEmpty();
        sync.Snapshots.All(x => x.DataSource == "Sandbox").Should().BeTrue();

        var report = await harness.Marketing.GeneratePerformanceReportAsync(strategy.Id);
        report.Fabricated.Should().BeFalse();
        report.Body.Should().Contain("published");
        report.DataSourcesJson.Should().Contain("Sandbox");
    }

    [Fact]
    public async Task CrossTenant_Isolation_PreventsBrandBleed()
    {
        var a = Harness.Open(name: "mkt-a");
        var b = Harness.Open(name: "mkt-b", businessName: "Other Co");

        var brandA = await a.Marketing.GetOrSeedBrandProfileAsync();
        var brandB = await b.Marketing.GetOrSeedBrandProfileAsync();
        brandA.BusinessName.Should().Be("Phase5B Co");
        brandB.BusinessName.Should().Be("Other Co");

        var campaignA = await a.Marketing.CreateCampaignAsync(new CreateCampaignRequest { Title = "A only" });
        var listB = await b.Marketing.ListCampaignsAsync();
        listB.Should().NotContain(x => x.Id == campaignA.Id);
    }

    [Fact]
    public async Task ApprovalFingerprint_RequiresReapprovalAfterMaterialEdit()
    {
        var harness = Harness.Open();
        var content = await harness.Marketing.GenerateContentItemAsync(new GenerateContentItemRequest
        {
            Platform = "Facebook",
            Brief = "Original"
        });
        content = await harness.Marketing.SubmitForReviewAsync(content.Id);
        content = await harness.Marketing.ApproveContentAsync(content.Id, content.ApprovalFingerprint);
        content.ApprovalStatus.Should().Be("Approved");

        content = await harness.Marketing.UpdateContentItemAsync(content.Id, new UpdateContentItemRequest
        {
            Caption = "Materially changed caption"
        });
        content.ApprovalStatus.Should().Be("Draft");
        content.ApprovalFingerprint.Should().BeNull();

        var act = async () => await harness.Marketing.ScheduleApprovedContentAsync(content.Id, new ScheduleContentRequest
        {
            ConnectedAccountProvider = "sandbox",
            IdempotencyKey = "reapproval-check"
        });
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task DuplicatePublish_IdempotencyKey_ReturnsSameJob()
    {
        var harness = Harness.Open();
        var content = await harness.Marketing.GenerateContentItemAsync(new GenerateContentItemRequest { Platform = "LinkedIn", Brief = "Idempotent" });
        content = await harness.Marketing.SubmitForReviewAsync(content.Id);
        content = await harness.Marketing.ApproveContentAsync(content.Id);

        var first = await harness.Marketing.ScheduleApprovedContentAsync(content.Id, new ScheduleContentRequest
        {
            RunAtUtc = DateTime.UtcNow,
            ConnectedAccountProvider = "sandbox",
            IdempotencyKey = "dup-key-1"
        });
        var second = await harness.Marketing.ScheduleApprovedContentAsync(content.Id, new ScheduleContentRequest
        {
            RunAtUtc = DateTime.UtcNow,
            ConnectedAccountProvider = "sandbox",
            IdempotencyKey = "dup-key-1"
        });
        second.Id.Should().Be(first.Id);
        harness.Db.MarketingPublishJobs.Count(x => x.IdempotencyKey == "dup-key-1").Should().Be(1);
    }

    private sealed class Harness
    {
        public required BusinessOSDbContext Db { get; init; }
        public required IMarketingOperationsService Marketing { get; init; }
        public required TenantProvider Provider { get; init; }

        public static Harness Open(string? name = null, string businessName = "Phase5B Co")
        {
            var (db, tenantId, provider) = TenantDatabase.Open(name: name);
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = businessName,
                Slug = businessName.ToLowerInvariant().Replace(' ', '-'),
                Email = "owner@phase5b.example",
                Phone = "555",
                Address = "Office",
                Country = "Pakistan",
                BusinessType = "software-agency",
                OwnerUserId = "user-1",
                SubscriptionPlanId = Guid.NewGuid(),
                Status = TenantStatus.Active,
                IsActive = true,
                Description = "Agency marketing test tenant"
            });
            db.CompanyStorefronts.Add(new CompanyStorefront
            {
                TenantId = tenantId,
                Industry = "software-agency",
                BrandColor = "#112233",
                PublicDescription = "We build software.",
                Website = "https://phase5b.example",
                IsPublished = true
            });
            db.StorefrontOfferings.Add(new StorefrontOffering
            {
                TenantId = tenantId,
                Name = "Website package",
                Summary = "Marketing site build",
                PricingModel = "fixed",
                Price = 1500m,
                IsPublished = true
            });
            db.SaveChanges();

            var user = new Mock<ICurrentUserService>();
            user.SetupGet(x => x.UserId).Returns("user-1");
            user.SetupGet(x => x.TenantId).Returns(tenantId);
            user.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);

            IMarketingPublishTransport[] transports = [new SandboxMarketingPublishTransport()];
            var marketing = new MarketingOperationsService(
                db,
                user.Object,
                provider,
                transports,
                NullLogger<MarketingOperationsService>.Instance,
                llm: new SilentModel(),
                images: null,
                integrations: null);

            return new Harness { Db = db, Marketing = marketing, Provider = provider };
        }
    }

    private sealed class SilentModel : ILlmChatClient
    {
        public bool IsConfigured => false;
        public Task<string?> GenerateReplyAsync(Guid tenantId, string userId, string systemPrompt, string userPrompt, CancellationToken cancellationToken = default, IReadOnlyList<AiMemoryTurnDto>? conversationHistory = null) => Task.FromResult<string?>(null);
        public async IAsyncEnumerable<string> StreamReplyAsync(Guid tenantId, string userId, string systemPrompt, string userPrompt, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default, IReadOnlyList<AiMemoryTurnDto>? conversationHistory = null) { await Task.CompletedTask; yield break; }
        public Task<(string? Reply, int? TokenUsage)> GenerateWithToolsAsync(Guid tenantId, string userId, string systemPrompt, string userPrompt, IReadOnlyList<object> toolResults, CancellationToken cancellationToken = default, IReadOnlyList<AiMemoryTurnDto>? conversationHistory = null) => Task.FromResult<(string?, int?)>((null, null));
    }
}
