using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Platform inbound lead from the public website. Not tenant-owned; never exposed through tenant filters.
/// </summary>
public class MarketingLead : AuditableEntity
{
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Company { get; set; }
    public string Kind { get; set; } = MarketingLeadKinds.Contact;
    public string Topic { get; set; } = default!;
    public string Subject { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string Status { get; set; } = MarketingLeadStatuses.New;
    public string? SourcePath { get; set; }
    public string? ReferralCode { get; set; }
    public bool MarketingOptIn { get; set; }
    public DateTime? ConsentAt { get; set; }
    public string? AssignedToUserId { get; set; }
    public string? Notes { get; set; }
    public Guid? ConvertedCompanyId { get; set; }
}

public static class MarketingLeadKinds
{
    public const string Contact = "Contact";
    public const string Demo = "Demo";
    public const string Waitlist = "Waitlist";
    public const string Support = "Support";
}

public static class MarketingLeadStatuses
{
    public const string New = "New";
    public const string Contacted = "Contacted";
    public const string Qualified = "Qualified";
    public const string Closed = "Closed";
    public const string Spam = "Spam";
}

/// <summary>Subscriber-facing support ticket for BusinessOS itself (not a tenant's CRM support tickets).</summary>
public class PlatformSupportTicket : AuditableEntity
{
    public Guid? TenantId { get; set; }
    public string? UserId { get; set; }
    public string RequesterName { get; set; } = default!;
    public string RequesterEmail { get; set; } = default!;
    public string Category { get; set; } = PlatformSupportCategories.General;
    public string Subject { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string Status { get; set; } = PlatformSupportStatuses.Open;
    public string Priority { get; set; } = "Normal";
    public string? AgentReply { get; set; }
    public bool EscalatedToHuman { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public static class PlatformSupportCategories
{
    public const string General = "General";
    public const string Billing = "Billing";
    public const string Technical = "Technical";
    public const string Onboarding = "Onboarding";
    public const string Account = "Account";
}

public static class PlatformSupportStatuses
{
    public const string Open = "Open";
    public const string InProgress = "InProgress";
    public const string WaitingOnCustomer = "WaitingOnCustomer";
    public const string Resolved = "Resolved";
    public const string Closed = "Closed";
}

/// <summary>Tenant referral code for acquisition attribution. Rewards are recorded, not auto-applied as credits.</summary>
public class ReferralCode : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public int RedemptionCount { get; set; }
    public int MaxRedemptions { get; set; } = 100;
}

public class ReferralRedemption : AuditableEntity
{
    public Guid ReferralCodeId { get; set; }
    public Guid ReferrerTenantId { get; set; }
    public Guid? ReferredTenantId { get; set; }
    public string ReferredEmail { get; set; } = default!;
    public string Status { get; set; } = ReferralRedemptionStatuses.Attributed;
    public DateTime? ConvertedAt { get; set; }
}

public static class ReferralRedemptionStatuses
{
    public const string Attributed = "Attributed";
    public const string Registered = "Registered";
    public const string Paid = "Paid";
    public const string Rejected = "Rejected";
}
