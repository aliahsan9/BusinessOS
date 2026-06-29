namespace BusinessOS.Application.Common.Models;

public sealed record TenantLimits(
    int MaxUsers,
    int MaxCustomers,
    int MaxProjects,
    int MaxTasks,
    long MaxStorageMb,
    int MaxAiRequests);

public sealed record TenantUsageSnapshot(
    int UserCount,
    int CustomerCount,
    int ProjectCount,
    int TaskCount,
    long StorageUsedMb,
    int AiRequestsUsed,
    DateTime LastCalculatedAt);

public static class PlanLimitHelper
{
    public const int Unlimited = -1;

    public static bool IsUnlimited(int limit) => limit == Unlimited;

    public static bool IsWithinLimit(int current, int max) =>
        IsUnlimited(max) || current < max;

    public static string FormatLimit(int limit) =>
        IsUnlimited(limit) ? "Unlimited" : limit.ToString();
}
