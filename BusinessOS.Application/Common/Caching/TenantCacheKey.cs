namespace BusinessOS.Application.Common.Caching;

public static class TenantCacheKey
{
    public static string For(Guid tenantId, string area, string key)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("A company id is required for a tenant cache key.");

        if (string.IsNullOrWhiteSpace(area) || string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Cache area and key are required.");

        return $"t:{tenantId:N}:{area.Trim()}:{key.Trim()}";
    }
}
