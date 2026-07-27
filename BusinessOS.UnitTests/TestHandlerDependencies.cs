using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Activities.DTOs;
using BusinessOS.Application.Features.Audit.Services;
using BusinessOS.Application.Features.Notifications.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace BusinessOS.UnitTests;

public static class TestHandlerDependencies
{
    public static IBusinessEventService CreateBusinessEvents() =>
        Mock.Of<IBusinessEventService>();

    public static IEntityAuditService CreateEntityAudit() =>
        Mock.Of<IEntityAuditService>();

    public static ILogger<T> CreateLogger<T>() =>
        Mock.Of<ILogger<T>>();

    public static ICacheService CreateCache() =>
        new CacheService(
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new CacheSettings()),
            Mock.Of<ILogger<CacheService>>());

    public static IOptions<CacheSettings> CreateCacheSettings() =>
        Options.Create(new CacheSettings());
}
