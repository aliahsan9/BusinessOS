using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.VectorSearch;
using BusinessOS.Application.Features.VectorSearch.Models;
using BusinessOS.Application.Features.VectorSearch.Options;
using BusinessOS.Application.Features.VectorSearch.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.AI.Copilot;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.MultiTenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace BusinessOS.UnitTests.AI;

public class AiVectorRagTenantIsolationTests
{
    [Fact]
    public async Task SearchAsync_sql_fallback_never_returns_other_tenant_chunks()
    {
        var database = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<BusinessOSDbContext>()
            .UseInMemoryDatabase(database)
            .Options;

        await SeedDocumentAsync(options, tenantA, "Tenant A policy", "Refund window is 14 days for Tenant A only.");
        await SeedDocumentAsync(options, tenantB, "Tenant B secret", "Refund window is 90 days for Tenant B confidential.");

        var provider = new TenantProvider();
        provider.SetTenantId(tenantA);
        await using var db = new BusinessOSDbContext(options, provider);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantA);
        currentUser.SetupGet(x => x.UserId).Returns("user-a");

        var vector = new Mock<IVectorSearchService>();
        vector.Setup(x => x.SearchAsync(It.IsAny<VectorSearchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("force sql fallback"));

        var sut = new AiVectorRagService(
            db,
            currentUser.Object,
            vector.Object,
            Options.Create(new QdrantOptions { Enabled = true }),
            NullLogger<AiVectorRagService>.Instance);

        var hits = await sut.SearchAsync("refund window", "policy", 5);

        hits.Should().NotBeEmpty();
        hits.Should().OnlyContain(h => h.Title == "Tenant A policy");
        hits.Should().NotContain(h => (h.Excerpt ?? "").Contains("Tenant B", StringComparison.OrdinalIgnoreCase));
        hits.Should().NotContain(h => (h.Excerpt ?? "").Contains("confidential", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchAsync_passes_caller_tenant_to_qdrant()
    {
        var database = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<BusinessOSDbContext>()
            .UseInMemoryDatabase(database)
            .Options;
        var provider = new TenantProvider();
        provider.SetTenantId(tenantA);
        await using var db = new BusinessOSDbContext(options, provider);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantA);
        currentUser.SetupGet(x => x.UserId).Returns("user-a");

        Guid? capturedTenant = null;
        var vector = new Mock<IVectorSearchService>();
        vector.Setup(x => x.SearchAsync(It.IsAny<VectorSearchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<VectorSearchRequest, CancellationToken>((req, _) => capturedTenant = req.TenantId)
            .ReturnsAsync(Array.Empty<VectorSearchHit>());

        var sut = new AiVectorRagService(
            db,
            currentUser.Object,
            vector.Object,
            Options.Create(new QdrantOptions { Enabled = true }),
            NullLogger<AiVectorRagService>.Instance);

        _ = await sut.SearchAsync("company policy", null, 3);

        capturedTenant.Should().Be(tenantA);
    }

    private static async Task SeedDocumentAsync(
        DbContextOptions<BusinessOSDbContext> options,
        Guid tenantId,
        string title,
        string content)
    {
        var provider = new TenantProvider();
        provider.SetTenantId(tenantId);
        await using var db = new BusinessOSDbContext(options, provider);
        var docId = Guid.NewGuid();
        db.AiDocuments.Add(new AiDocument
        {
            Id = docId,
            TenantId = tenantId,
            Title = title,
            DocumentType = "policy",
            Content = content,
            CreatedByUserId = "seed",
            IsIndexed = true
        });
        db.AiDocumentChunks.Add(new AiDocumentChunk
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentId = docId,
            ChunkIndex = 0,
            Content = content,
            Keywords = "refund window tenant policy",
            DocumentType = "policy",
            CreatedByUserId = "seed"
        });
        await db.SaveChangesAsync();
    }
}
