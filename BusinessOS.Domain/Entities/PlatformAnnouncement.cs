using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class PlatformAnnouncement : AuditableEntity
{
    public string Title { get; set; } = default!;
    public string Body { get; set; } = default!;
    public bool IsPublished { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public bool TargetAllCompanies { get; set; } = true;
    public string CreatedByUserId { get; set; } = default!;

    public ICollection<PlatformAnnouncementTarget> Targets { get; set; } = new List<PlatformAnnouncementTarget>();

    public bool IsVisibleTo(Guid tenantId, DateTime utcNow)
    {
        if (IsDeleted || !IsActive || !IsPublished)
            return false;

        if (StartsAt is DateTime starts && starts > utcNow)
            return false;

        if (EndsAt is DateTime ends && ends <= utcNow)
            return false;

        if (TargetAllCompanies)
            return true;

        return Targets.Any(target => target.TargetTenantId == tenantId);
    }
}

public class PlatformAnnouncementTarget
{
    public Guid AnnouncementId { get; set; }
    public Guid TargetTenantId { get; set; }

    public PlatformAnnouncement Announcement { get; set; } = default!;
}
