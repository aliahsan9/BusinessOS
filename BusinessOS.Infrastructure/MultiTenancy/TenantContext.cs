namespace BusinessOS.Infrastructure.MultiTenancy;

/// <summary>
/// Static tenant holder for EF Core global query filters.
/// Set at request boundary via middleware; design-time tools use <see cref="Guid.Empty"/>.
/// </summary>
public static class TenantContext
{
    private static readonly AsyncLocal<Guid> _currentTenantId = new();

    public static Guid CurrentTenantId => _currentTenantId.Value;

    public static void SetTenantId(Guid tenantId) => _currentTenantId.Value = tenantId;

    public static void Clear() => _currentTenantId.Value = Guid.Empty;
}
