namespace BusinessOS.Application.Features.Experience;

public interface IExperienceService
{
    Task<DashboardExperienceDto> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<DashboardExperienceDto> SaveLayoutAsync(IReadOnlyList<DashboardWidgetWrite> widgets, CancellationToken cancellationToken = default);
    Task<DashboardExperienceDto> RestoreLayoutAsync(CancellationToken cancellationToken = default);
    Task<WorkspaceExperienceDto> GetWorkspaceAsync(CancellationToken cancellationToken = default);
    Task<WorkspaceExperienceDto> SaveWorkspaceAsync(string workspace, CancellationToken cancellationToken = default);
    Task<StorefrontAdminDto> GetStorefrontAsync(CancellationToken cancellationToken = default);
    Task<StorefrontAdminDto> SaveStorefrontAsync(StorefrontWriteRequest request, CancellationToken cancellationToken = default);
    Task<StorefrontOfferingDto> SaveOfferingAsync(StorefrontOfferingWrite request, CancellationToken cancellationToken = default);
    Task DeleteOfferingAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StorefrontPackageDto> SavePackageAsync(StorefrontPackageWrite request, CancellationToken cancellationToken = default);
    Task DeletePackageAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StorefrontPortfolioDto> SavePortfolioAsync(StorefrontPortfolioWrite request, CancellationToken cancellationToken = default);
    Task DeletePortfolioAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StorefrontReviewDto> ModerateReviewAsync(Guid id, string status, CancellationToken cancellationToken = default);
    Task<SupplierPortalLinkDto> CreateSupplierLinkAsync(Guid supplierId, string email, CancellationToken cancellationToken = default);
    Task RevokeSupplierLinkAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PublicCompanyDto?> GetPublicCompanyAsync(string slug, CancellationToken cancellationToken = default);
    Task<PublicSubmissionDto> SubmitInquiryAsync(string slug, PublicInquiryRequest request, CancellationToken cancellationToken = default);
    Task<PublicSubmissionDto> SubmitOrderAsync(string slug, PublicOrderRequest request, CancellationToken cancellationToken = default);
    Task<PublicSubmissionDto> SubmitReviewAsync(string slug, PublicReviewRequest request, CancellationToken cancellationToken = default);
    Task<SupplierPortalDto> GetSupplierPortalAsync(string token, CancellationToken cancellationToken = default);
    Task<string> BuildSitemapAsync(string origin, CancellationToken cancellationToken = default);
}

public sealed record DashboardWidgetDto(string Id, string Label, string Size, bool Visible);

public sealed class DashboardWidgetWrite
{
    public string Id { get; set; } = "";
    public string Size { get; set; } = "md";
    public bool Visible { get; set; } = true;
}

public sealed class BusinessInsightDto
{
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Route { get; set; }
}

public sealed class DashboardExperienceDto
{
    public IReadOnlyList<DashboardWidgetDto> Widgets { get; set; } = [];
    public IReadOnlyList<BusinessInsightDto> Insights { get; set; } = [];
    public int? PendingOrders { get; set; }
    public int? ProcessingOrders { get; set; }
    public int? CompletedOrders { get; set; }
    public int? CancelledOrders { get; set; }
    public int? ActiveProjects { get; set; }
    public int? CompletedProjects { get; set; }
    public int? OverdueTasks { get; set; }
    public int? TotalEmployees { get; set; }
    public int? ActiveEmployees { get; set; }
    public int? PendingLeave { get; set; }
    public int? ActiveAgents { get; set; }
    public int? RunningTasks { get; set; }
    public int? CompletedTasks { get; set; }
    public int? FailedExecutions { get; set; }
    public int? PendingApprovals { get; set; }
    public decimal? EstimatedAiCostUsd { get; set; }
    public bool? AiCostIsEstimate { get; set; }
    public int? ActiveWorkflows { get; set; }
    public int? SuccessfulWorkflowRuns { get; set; }
    public int? FailedWorkflowRuns { get; set; }
    public int? ScheduledWorkflows { get; set; }
    public decimal? Revenue { get; set; }
    public decimal? PreviousRevenue { get; set; }
    public decimal? Expenses { get; set; }
    public decimal? PreviousExpenses { get; set; }
}

public sealed class WorkspaceLinkDto
{
    public string Label { get; set; } = "";
    public string Route { get; set; } = "";
}

public sealed class WorkspaceExperienceDto
{
    public string BusinessType { get; set; } = "";
    public string Workspace { get; set; } = "general";
    public IReadOnlyList<string> EnabledModules { get; set; } = [];
    public IReadOnlyList<WorkspaceLinkDto> SuggestedNavigation { get; set; } = [];
    public string Note { get; set; } = "";
}

public sealed class StorefrontWriteRequest
{
    public bool IsPublished { get; set; }
    public string? Slug { get; set; }
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
    public string? BrandColor { get; set; }
    public string? Layout { get; set; }
    public bool ShowProducts { get; set; } = true;
    public bool ShowServices { get; set; } = true;
    public bool ShowPortfolio { get; set; } = true;
    public bool ShowReviews { get; set; } = true;
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? ConsentNotice { get; set; }
}

public sealed class StorefrontOfferingWrite
{
    public Guid? Id { get; set; }
    public string Kind { get; set; } = "service";
    public Guid? ProductId { get; set; }
    public string Name { get; set; } = "";
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string PricingModel { get; set; } = "quote";
    public decimal? Price { get; set; }
    public string? Deliverables { get; set; }
    public string? DeliveryTime { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsPublished { get; set; }
    public int SortOrder { get; set; }
}

public sealed class StorefrontPackageWrite
{
    public Guid? Id { get; set; }
    public Guid? OfferingId { get; set; }
    public string Name { get; set; } = "";
    public string? Scope { get; set; }
    public string? Deliverables { get; set; }
    public string PricingModel { get; set; } = "fixed";
    public decimal? Price { get; set; }
    public bool IsPublished { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class StorefrontPortfolioWrite
{
    public Guid? Id { get; set; }
    public string Title { get; set; } = "";
    public string? Summary { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Technologies { get; set; }
    public string? Industry { get; set; }
    public string? ProjectUrl { get; set; }
    public string? Results { get; set; }
    public bool IsPublished { get; set; }
}

public sealed record StorefrontOfferingDto(Guid Id, string Kind, Guid? ProductId, string Name, string? Summary, string? Description, string? Category, string? Sku, string PricingModel, decimal? Price, string? Deliverables, string? DeliveryTime, string? ImageUrl, bool IsFeatured, bool IsPublished, int SortOrder, bool? InStock);
public sealed record StorefrontPackageDto(Guid Id, Guid? OfferingId, string Name, string? Scope, string? Deliverables, string PricingModel, decimal? Price, bool IsPublished, int SortOrder);
public sealed record StorefrontPortfolioDto(Guid Id, string Title, string? Summary, string? CoverImageUrl, string? Technologies, string? Industry, string? ProjectUrl, string? Results, bool IsPublished);
public sealed record StorefrontReviewDto(Guid Id, string DisplayName, int Rating, string Body, string Status, DateTime CreatedAt);

public sealed class StorefrontAdminDto
{
    public string CompanyName { get; set; } = "";
    public string Slug { get; set; } = "";
    public string PublicPath { get; set; } = "";
    public bool IsPublished { get; set; }
    public string? Tagline { get; set; }
    public string? PublicDescription { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? LogoUrl { get; set; }
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
    public bool ShowProducts { get; set; }
    public bool ShowServices { get; set; }
    public bool ShowPortfolio { get; set; }
    public bool ShowReviews { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? ConsentNotice { get; set; }
    public string PreferredWorkspace { get; set; } = "general";
    public IReadOnlyList<StorefrontOfferingDto> Offerings { get; set; } = [];
    public IReadOnlyList<StorefrontPackageDto> Packages { get; set; } = [];
    public IReadOnlyList<StorefrontPortfolioDto> Portfolio { get; set; } = [];
    public IReadOnlyList<StorefrontReviewDto> Reviews { get; set; } = [];
}

public sealed class PublicCompanyDto
{
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? RedirectSlug { get; set; }
    public string? LogoUrl { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Tagline { get; set; }
    public string? Description { get; set; }
    public string? Industry { get; set; }
    public string? Category { get; set; }
    public string? Website { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Location { get; set; }
    public string? SocialLinksJson { get; set; }
    public string? HoursJson { get; set; }
    public string BrandColor { get; set; } = "#2563eb";
    public string Layout { get; set; } = "standard";
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? ConsentNotice { get; set; }
    public string Currency { get; set; } = "USD";
    public bool ShowProducts { get; set; }
    public bool ShowServices { get; set; }
    public bool ShowPortfolio { get; set; }
    public bool ShowReviews { get; set; }
    public IReadOnlyList<StorefrontOfferingDto> Offerings { get; set; } = [];
    public IReadOnlyList<StorefrontPackageDto> Packages { get; set; } = [];
    public IReadOnlyList<StorefrontPortfolioDto> Portfolio { get; set; } = [];
    public IReadOnlyList<StorefrontReviewDto> Reviews { get; set; } = [];
}

public class PublicInquiryRequest
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public Guid? OfferingId { get; set; }
    public Guid? PackageId { get; set; }
    public string? BudgetRange { get; set; }
    public string? Requirements { get; set; }
    public string Message { get; set; } = "";
    public bool Consent { get; set; }
    public string? CompanyWebsite { get; set; }
    public string? ClientToken { get; set; }
    public string Kind { get; set; } = "inquiry";
}

public class PublicOrderRequest
{
    public Guid OfferingId { get; set; }
    public int Quantity { get; set; } = 1;
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public bool Consent { get; set; }
    public string? CompanyWebsite { get; set; }
    public string? ClientToken { get; set; }
}

public class PublicReviewRequest
{
    public string DisplayName { get; set; } = "";
    public int Rating { get; set; }
    public string Body { get; set; } = "";
    public bool Consent { get; set; }
    public string? CompanyWebsite { get; set; }
    public string? ReportReason { get; set; }
    public Guid? ReviewId { get; set; }
}

public sealed class PublicSubmissionDto
{
    public bool Accepted { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? OrderId { get; set; }
    public string? OrderNumber { get; set; }
    public string? Status { get; set; }
    public string Message { get; set; } = "";
}

public sealed class SupplierPortalLinkDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public string Token { get; set; } = "";
}

public sealed class SupplierPortalOrderDto
{
    public string? Reference { get; set; }
    public DateTime Date { get; set; }
    public string Status { get; set; } = "";
    public decimal Total { get; set; }
}

public sealed class SupplierPortalDto
{
    public string SupplierName { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public IReadOnlyList<SupplierPortalOrderDto> Orders { get; set; } = [];
}
