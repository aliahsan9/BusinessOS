namespace BusinessOS.Application.Features.Tenant.DTOs;

public class TenantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? LogoUrl { get; set; }
    public string? Domain { get; set; }
    public string Timezone { get; set; } = "UTC";
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = default!;
    public string SubscriptionPlan { get; set; } = "Free";
    public Guid SubscriptionPlanId { get; set; }
    public string BusinessType { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Website { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class TenantSettingsDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string? LogoUrl { get; set; }
    public string Timezone { get; set; } = "UTC";
    public string Currency { get; set; } = "USD";
    public string BusinessType { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string Address { get; set; } = default!;
    public string? Website { get; set; }
    public string? Description { get; set; }
    public string? Theme { get; set; }
}

public class TenantUsageDto
{
    public int UserCount { get; set; }
    public int MaxUsers { get; set; }
    public int CustomerCount { get; set; }
    public int MaxCustomers { get; set; }
    public int ProjectCount { get; set; }
    public int MaxProjects { get; set; }
    public int TaskCount { get; set; }
    public int MaxTasks { get; set; }
    public long StorageUsedMb { get; set; }
    public long MaxStorageMb { get; set; }
    public int AiRequestsUsed { get; set; }
    public int MaxAiRequests { get; set; }
    public string SubscriptionPlan { get; set; } = "Free";
    public DateTime LastCalculatedAt { get; set; }
}

public class TenantDashboardDto
{
    public TenantDto Tenant { get; set; } = default!;
    public TenantUsageDto Usage { get; set; } = default!;
}

public record UpdateTenantRequest(
    string Name,
    string? LogoUrl,
    string Timezone,
    string Currency,
    string BusinessType,
    string Email,
    string? Website,
    string? Description);

public record UpdateTenantSettingsRequest(
    string Name,
    string? LogoUrl,
    string Timezone,
    string Currency,
    string BusinessType,
    string Email,
    string Phone,
    string Address,
    string? Website,
    string? Description);

public record RegisterBusinessRequest(
    string BusinessName,
    string OwnerFirstName,
    string OwnerLastName,
    string Email,
    string Password,
    string Timezone,
    string Currency,
    string Industry);
