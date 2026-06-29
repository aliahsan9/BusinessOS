namespace BusinessOS.Application.Common.Models;

public sealed record TenantLimits(
    int MaxUsers,
    int MaxCustomers,
    int MaxProjects,
    long MaxStorageMb,
    int MaxAiRequests);

public sealed record TenantUsageSnapshot(
    int UserCount,
    int CustomerCount,
    int ProjectCount,
    long StorageUsedMb,
    int AiRequestsUsed,
    DateTime LastCalculatedAt);
