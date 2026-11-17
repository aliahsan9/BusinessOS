using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class CompanyStorefront : AuditableEntity
{
    public Guid TenantId { get; set; }
    public bool IsPublished { get; set; }
    public string? Tagline { get; set; }
    public string? PublicDescription { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? PublicEmail { get; set; }
    public string? PublicPhone { get; set; }
    public string? Website { get; set; }
    public string? Location { get; set; }
    public string? Industry { get; set; }
    public string? Category { get; set; }
    public string? SocialLinksJson { get; set; }
    public string? HoursJson { get; set; }
    public string BrandColor { get; set; } = "#2563eb";
    public string Layout { get; set; } = "standard";
    public bool ShowProducts { get; set; } = true;
    public bool ShowServices { get; set; } = true;
    public bool ShowPortfolio { get; set; } = true;
    public bool ShowReviews { get; set; } = true;
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? ConsentNotice { get; set; }
    public string PreferredWorkspace { get; set; } = "general";
}

public class StorefrontSlugRedirect : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string FromSlug { get; set; } = default!;
}

public class StorefrontOffering : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Kind { get; set; } = "service";
    public Guid? ProductId { get; set; }
    public string Name { get; set; } = default!;
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Sku { get; set; }
    public string PricingModel { get; set; } = "quote";
    public decimal? Price { get; set; }
    public string? Deliverables { get; set; }
    public string? DeliveryTime { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsPublished { get; set; }
    public int SortOrder { get; set; }
}

public class StorefrontPackage : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid? OfferingId { get; set; }
    public string Name { get; set; } = default!;
    public string? Scope { get; set; }
    public string? Deliverables { get; set; }
    public string PricingModel { get; set; } = "fixed";
    public decimal? Price { get; set; }
    public bool IsPublished { get; set; } = true;
    public int SortOrder { get; set; }
}

public class StorefrontPortfolioItem : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Title { get; set; } = default!;
    public string? Summary { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Technologies { get; set; }
    public string? Industry { get; set; }
    public string? ProjectUrl { get; set; }
    public string? Results { get; set; }
    public bool IsPublished { get; set; }
}

public class StorefrontReview : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string DisplayName { get; set; } = default!;
    public int Rating { get; set; }
    public string Body { get; set; } = default!;
    public string Status { get; set; } = "Pending";
    public string? ReportReason { get; set; }
}

public class StorefrontInquiry : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }
    public Guid? OfferingId { get; set; }
    public Guid? PackageId { get; set; }
    public string? BudgetRange { get; set; }
    public string? Requirements { get; set; }
    public string Message { get; set; } = default!;
    public string Kind { get; set; } = "inquiry";
    public Guid? LeadId { get; set; }
    public Guid? OrderId { get; set; }
    public string? ClientToken { get; set; }
}

public class DashboardPreference : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = default!;
    public string WidgetsJson { get; set; } = "[]";
}

public class SupplierPortalAccess : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid SupplierId { get; set; }
    public string Email { get; set; } = default!;
    public string TokenHash { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
}
