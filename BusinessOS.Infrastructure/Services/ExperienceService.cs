using System.Globalization;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Experience;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Domain.Companies;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class ExperienceService : IExperienceService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> ReservedSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "api", "auth", "about", "company", "contact", "dashboard", "faq", "forbidden",
        "insights", "nalva", "not-found", "onboarding", "portal", "privacy", "public", "refunds",
        "register-business", "services", "supplier-portal", "terms", "www"
    };
    private static readonly HashSet<string> Workspaces = new(StringComparer.OrdinalIgnoreCase)
    {
        "agency", "retail", "service", "ecommerce", "general"
    };
    private static readonly HashSet<string> Sizes = new(StringComparer.OrdinalIgnoreCase) { "sm", "md", "lg" };
    private static readonly HashSet<string> Layouts = new(StringComparer.OrdinalIgnoreCase) { "standard", "catalog", "services" };
    private static readonly HashSet<string> PricingModels = new(StringComparer.OrdinalIgnoreCase) { "fixed", "starting", "hourly", "quote" };

    private readonly BusinessOSDbContext _db;
    private readonly IDbContextFactory<BusinessOSDbContext> _factory;
    private readonly ICurrentUserService _user;
    private readonly ITenantProvider _tenant;

    public ExperienceService(
        BusinessOSDbContext db,
        IDbContextFactory<BusinessOSDbContext> factory,
        ICurrentUserService user,
        ITenantProvider tenant)
    {
        _db = db;
        _factory = factory;
        _user = user;
        _tenant = tenant;
    }

    public async Task<DashboardExperienceDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        RequireUser();
        var dto = new DashboardExperienceDto
        {
            Widgets = await WidgetsAsync(cancellationToken),
            Insights = await InsightsAsync(cancellationToken)
        };
        await FillOperationsAsync(dto, cancellationToken);
        return dto;
    }

    public async Task<DashboardExperienceDto> SaveLayoutAsync(IReadOnlyList<DashboardWidgetWrite> widgets, CancellationToken cancellationToken = default)
    {
        RequireUser();
        var allowed = AllowedWidgets().Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var clean = new List<DashboardWidgetDto>();
        foreach (var widget in widgets ?? [])
        {
            if (!allowed.Contains(widget.Id) || clean.Any(x => x.Id == widget.Id))
                continue;
            var known = AllowedWidgets().First(x => x.Id.Equals(widget.Id, StringComparison.OrdinalIgnoreCase));
            clean.Add(known with
            {
                Size = Sizes.Contains(widget.Size) ? widget.Size.ToLowerInvariant() : "md",
                Visible = widget.Visible
            });
        }
        foreach (var missing in AllowedWidgets().Where(x => clean.All(saved => saved.Id != x.Id)))
            clean.Add(missing);

        var userId = _user.UserId!;
        var row = await _db.DashboardPreferences.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (row is null)
        {
            row = new DashboardPreference { UserId = userId };
            _db.DashboardPreferences.Add(row);
        }
        row.WidgetsJson = JsonSerializer.Serialize(clean, JsonOptions);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetDashboardAsync(cancellationToken);
    }

    public async Task<DashboardExperienceDto> RestoreLayoutAsync(CancellationToken cancellationToken = default)
    {
        RequireUser();
        var row = await _db.DashboardPreferences.FirstOrDefaultAsync(x => x.UserId == _user.UserId, cancellationToken);
        if (row is not null)
        {
            _db.DashboardPreferences.Remove(row);
            await _db.SaveChangesAsync(cancellationToken);
        }
        return await GetDashboardAsync(cancellationToken);
    }

    public async Task<WorkspaceExperienceDto> GetWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        RequireUser();
        var tenant = await CurrentTenantAsync(cancellationToken);
        var storefront = await _db.CompanyStorefronts.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var workspace = storefront?.PreferredWorkspace;
        if (string.IsNullOrWhiteSpace(workspace) || !Workspaces.Contains(workspace))
            workspace = WorkspaceFor(tenant.BusinessType);
        return DescribeWorkspace(tenant, workspace);
    }

    public async Task<WorkspaceExperienceDto> SaveWorkspaceAsync(string workspace, CancellationToken cancellationToken = default)
    {
        if (!_user.HasPermission(PermissionCodes.OrganizationManage) && !_user.HasPermission(PermissionCodes.TenantManage))
            throw new ForbiddenException("You cannot change the workspace.");
        var selected = (workspace ?? "").Trim().ToLowerInvariant();
        if (!Workspaces.Contains(selected))
            throw new BadRequestException("Choose agency, retail, service, ecommerce, or general.");
        var row = await EnsureStorefrontAsync(cancellationToken);
        row.PreferredWorkspace = selected;
        await _db.SaveChangesAsync(cancellationToken);
        return await GetWorkspaceAsync(cancellationToken);
    }

    public async Task<StorefrontAdminDto> GetStorefrontAsync(CancellationToken cancellationToken = default)
    {
        RequireStorefront(false);
        await EnsureStorefrontAsync(cancellationToken);
        return await MapAdminAsync(_db, cancellationToken);
    }

    public async Task<StorefrontAdminDto> SaveStorefrontAsync(StorefrontWriteRequest request, CancellationToken cancellationToken = default)
    {
        RequireStorefront(true);
        var tenant = await CurrentTenantAsync(cancellationToken);
        var row = await EnsureStorefrontAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.Slug))
            await ChangeSlugAsync(tenant, request.Slug, cancellationToken);
        row.IsPublished = request.IsPublished;
        row.Tagline = Clip(request.Tagline, 180);
        row.PublicDescription = Clip(request.PublicDescription, 4000);
        row.CoverImageUrl = SafeHttp(request.CoverImageUrl);
        row.PublicEmail = Clip(request.PublicEmail, 200);
        row.PublicPhone = Clip(request.PublicPhone, 40);
        row.Website = SafeHttp(request.Website);
        row.Location = Clip(request.Location, 240);
        row.Industry = Clip(request.Industry, 120);
        row.Category = Clip(request.Category, 120);
        row.SocialLinksJson = Clip(request.SocialLinksJson, 2000);
        row.HoursJson = Clip(request.HoursJson, 2000);
        row.BrandColor = NormalizeColor(request.BrandColor);
        row.Layout = Layouts.Contains(request.Layout ?? "") ? request.Layout!.ToLowerInvariant() : "standard";
        row.ShowProducts = request.ShowProducts;
        row.ShowServices = request.ShowServices;
        row.ShowPortfolio = request.ShowPortfolio;
        row.ShowReviews = request.ShowReviews;
        row.SeoTitle = Clip(request.SeoTitle, 160);
        row.SeoDescription = Clip(request.SeoDescription, 320);
        row.ConsentNotice = Clip(request.ConsentNotice, 1000);
        await _db.SaveChangesAsync(cancellationToken);
        return await MapAdminAsync(_db, cancellationToken);
    }

    public async Task<StorefrontOfferingDto> SaveOfferingAsync(StorefrontOfferingWrite request, CancellationToken cancellationToken = default)
    {
        RequireStorefront(true);
        var name = Required(request.Name, "Name", 200);
        var kind = request.Kind.Equals("product", StringComparison.OrdinalIgnoreCase) ? "product" : "service";
        var pricing = PricingModels.Contains(request.PricingModel) ? request.PricingModel.ToLowerInvariant() : "quote";
        Product? product = null;
        if (request.ProductId is Guid productId)
        {
            product = await _db.Products.FirstOrDefaultAsync(x => x.Id == productId, cancellationToken)
                ?? throw new BadRequestException("That product is not in this company.");
        }
        if (kind == "product" && product is null)
            throw new BadRequestException("A published product must be linked to a catalog product.");

        var row = request.Id is Guid id
            ? await _db.StorefrontOfferings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("Offering was not found.")
            : new StorefrontOffering();
        if (request.Id is null)
            _db.StorefrontOfferings.Add(row);
        row.Kind = kind;
        row.ProductId = product?.Id;
        row.Name = name;
        row.Summary = Clip(request.Summary, 500);
        row.Description = Clip(request.Description, 4000);
        row.Category = Clip(request.Category, 120);
        row.Sku = product?.SKU;
        row.PricingModel = pricing;
        row.Price = request.Price is < 0 ? throw new BadRequestException("Price cannot be negative.") : request.Price;
        row.Deliverables = Clip(request.Deliverables, 2000);
        row.DeliveryTime = Clip(request.DeliveryTime, 120);
        row.ImageUrl = SafeHttp(request.ImageUrl) ?? product?.ImageUrl;
        row.IsFeatured = request.IsFeatured;
        row.IsPublished = request.IsPublished;
        row.SortOrder = request.SortOrder;
        await _db.SaveChangesAsync(cancellationToken);
        return MapOffering(row, product);
    }

    public async Task DeleteOfferingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        RequireStorefront(true);
        var row = await _db.StorefrontOfferings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Offering was not found.");
        _db.StorefrontOfferings.Remove(row);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<StorefrontPackageDto> SavePackageAsync(StorefrontPackageWrite request, CancellationToken cancellationToken = default)
    {
        RequireStorefront(true);
        if (request.OfferingId is Guid offeringId && !await _db.StorefrontOfferings.AnyAsync(x => x.Id == offeringId, cancellationToken))
            throw new BadRequestException("The package must belong to an offering in this company.");
        var row = request.Id is Guid id
            ? await _db.StorefrontPackages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("Package was not found.")
            : new StorefrontPackage();
        if (request.Id is null)
            _db.StorefrontPackages.Add(row);
        row.OfferingId = request.OfferingId;
        row.Name = Required(request.Name, "Name", 160);
        row.Scope = Clip(request.Scope, 2000);
        row.Deliverables = Clip(request.Deliverables, 2000);
        row.PricingModel = PricingModels.Contains(request.PricingModel) ? request.PricingModel.ToLowerInvariant() : "quote";
        row.Price = request.Price is < 0 ? throw new BadRequestException("Price cannot be negative.") : request.Price;
        row.IsPublished = request.IsPublished;
        row.SortOrder = request.SortOrder;
        await _db.SaveChangesAsync(cancellationToken);
        return MapPackage(row);
    }

    public async Task DeletePackageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        RequireStorefront(true);
        var row = await _db.StorefrontPackages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Package was not found.");
        _db.StorefrontPackages.Remove(row);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<StorefrontPortfolioDto> SavePortfolioAsync(StorefrontPortfolioWrite request, CancellationToken cancellationToken = default)
    {
        RequireStorefront(true);
        var row = request.Id is Guid id
            ? await _db.StorefrontPortfolioItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("Portfolio item was not found.")
            : new StorefrontPortfolioItem();
        if (request.Id is null)
            _db.StorefrontPortfolioItems.Add(row);
        row.Title = Required(request.Title, "Title", 180);
        row.Summary = Clip(request.Summary, 2000);
        row.CoverImageUrl = SafeHttp(request.CoverImageUrl);
        row.Technologies = Clip(request.Technologies, 400);
        row.Industry = Clip(request.Industry, 120);
        row.ProjectUrl = SafeHttp(request.ProjectUrl);
        row.Results = Clip(request.Results, 1000);
        row.IsPublished = request.IsPublished;
        await _db.SaveChangesAsync(cancellationToken);
        return MapPortfolio(row);
    }

    public async Task DeletePortfolioAsync(Guid id, CancellationToken cancellationToken = default)
    {
        RequireStorefront(true);
        var row = await _db.StorefrontPortfolioItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Portfolio item was not found.");
        _db.StorefrontPortfolioItems.Remove(row);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<StorefrontReviewDto> ModerateReviewAsync(Guid id, string status, CancellationToken cancellationToken = default)
    {
        RequireStorefront(true);
        var normalized = status?.Trim().ToLowerInvariant() switch
        {
            "published" => "Published",
            "rejected" => "Rejected",
            "pending" => "Pending",
            _ => throw new BadRequestException("Status must be published, rejected, or pending.")
        };
        var row = await _db.StorefrontReviews.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Review was not found.");
        row.Status = normalized;
        await _db.SaveChangesAsync(cancellationToken);
        return MapReview(row);
    }

    public async Task<SupplierPortalLinkDto> CreateSupplierLinkAsync(Guid supplierId, string email, CancellationToken cancellationToken = default)
    {
        if (!_user.HasPermission(PermissionCodes.SupplierView) && !_user.HasPermission(PermissionCodes.PurchaseOrderView))
            throw new ForbiddenException("You cannot invite a supplier.");
        if (!await _db.Suppliers.AnyAsync(x => x.Id == supplierId, cancellationToken))
            throw new NotFoundException("Supplier was not found.");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var access = new SupplierPortalAccess
        {
            SupplierId = supplierId,
            Email = Required(email, "Email", 200).ToLowerInvariant(),
            TokenHash = Hash(token),
            ExpiresAt = DateTime.UtcNow.AddDays(14)
        };
        _db.SupplierPortalAccesses.Add(access);
        await _db.SaveChangesAsync(cancellationToken);
        return new SupplierPortalLinkDto { Id = access.Id, Email = access.Email, ExpiresAt = access.ExpiresAt, Token = token };
    }

    public async Task RevokeSupplierLinkAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_user.HasPermission(PermissionCodes.SupplierView) && !_user.HasPermission(PermissionCodes.PurchaseOrderView))
            throw new ForbiddenException("You cannot revoke a supplier link.");
        var access = await _db.SupplierPortalAccesses.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Supplier link was not found.");
        access.IsRevoked = true;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PublicCompanyDto?> GetPublicCompanyAsync(string slug, CancellationToken cancellationToken = default)
    {
        await using var lookup = await _factory.CreateDbContextAsync(cancellationToken);
        var (tenant, redirect) = await ResolveSlugAsync(lookup, slug, cancellationToken);
        if (tenant is null || !IsPublicTenant(tenant))
            return null;
        if (redirect is not null)
            return new PublicCompanyDto { Slug = slug.Trim().ToLowerInvariant(), RedirectSlug = redirect, Name = tenant.Name };

        var storefront = await lookup.CompanyStorefronts.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && !x.IsDeleted && x.IsPublished, cancellationToken);
        if (storefront is null)
            return null;

        var offerings = await lookup.StorefrontOfferings.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenant.Id && !x.IsDeleted && x.IsPublished)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        var productIds = offerings.Where(x => x.ProductId != null).Select(x => x.ProductId!.Value).ToList();
        var products = await lookup.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(x => productIds.Contains(x.Id) && x.TenantId == tenant.Id && !x.IsDeleted)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        offerings = offerings.Where(x => x.Kind != "product" || (x.ProductId is Guid id && products.ContainsKey(id))).ToList();
        var packages = storefront.ShowServices
            ? await lookup.StorefrontPackages.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.TenantId == tenant.Id && !x.IsDeleted && x.IsPublished)
                .OrderBy(x => x.SortOrder).ToListAsync(cancellationToken)
            : [];
        var portfolio = storefront.ShowPortfolio
            ? await lookup.StorefrontPortfolioItems.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.TenantId == tenant.Id && !x.IsDeleted && x.IsPublished)
                .OrderByDescending(x => x.CreatedAt).Take(24).ToListAsync(cancellationToken)
            : [];
        var reviews = storefront.ShowReviews
            ? await lookup.StorefrontReviews.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.TenantId == tenant.Id && !x.IsDeleted && x.Status == "Published")
                .OrderByDescending(x => x.CreatedAt).Take(20).ToListAsync(cancellationToken)
            : [];

        return new PublicCompanyDto
        {
            Name = tenant.Name,
            Slug = tenant.Slug,
            LogoUrl = tenant.LogoUrl,
            CoverImageUrl = storefront.CoverImageUrl,
            Tagline = storefront.Tagline,
            Description = storefront.PublicDescription ?? tenant.Description,
            Industry = storefront.Industry,
            Category = storefront.Category,
            Website = storefront.Website ?? tenant.Website,
            Email = storefront.PublicEmail,
            Phone = storefront.PublicPhone,
            Location = storefront.Location,
            SocialLinksJson = storefront.SocialLinksJson,
            HoursJson = storefront.HoursJson,
            BrandColor = storefront.BrandColor,
            Layout = storefront.Layout,
            SeoTitle = storefront.SeoTitle,
            SeoDescription = storefront.SeoDescription,
            ConsentNotice = storefront.ConsentNotice,
            Currency = tenant.Currency,
            ShowProducts = storefront.ShowProducts,
            ShowServices = storefront.ShowServices,
            ShowPortfolio = storefront.ShowPortfolio,
            ShowReviews = storefront.ShowReviews,
            Offerings = offerings.Where(x => x.Kind == "product" ? storefront.ShowProducts : storefront.ShowServices)
                .Select(x => MapOffering(x, x.ProductId is Guid id && products.TryGetValue(id, out var product) ? product : null)).ToList(),
            Packages = packages.Select(MapPackage).ToList(),
            Portfolio = portfolio.Select(MapPortfolio).ToList(),
            Reviews = reviews.Select(MapReview).ToList()
        };
    }

    public Task<PublicSubmissionDto> SubmitInquiryAsync(string slug, PublicInquiryRequest request, CancellationToken cancellationToken = default) =>
        SubmitLeadAsync(slug, request, cancellationToken);

    public async Task<PublicSubmissionDto> SubmitOrderAsync(string slug, PublicOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(request.CompanyWebsite))
            return new PublicSubmissionDto { Accepted = true, Message = "Received." };
        if (!request.Consent)
            throw new BadRequestException("Consent is required.");
        var quantity = request.Quantity;
        if (quantity is < 1 or > 99)
            throw new BadRequestException("Quantity must be between 1 and 99.");
        var (db, tenant) = await OpenPublicAsync(slug, cancellationToken);
        await using (db)
        {
            var existing = await FindTokenAsync(db, request.ClientToken, cancellationToken);
            if (existing?.OrderId is Guid existingOrder)
            {
                var number = await db.Orders.Where(x => x.Id == existingOrder).Select(x => x.OrderNumber).FirstOrDefaultAsync(cancellationToken);
                return new PublicSubmissionDto { Accepted = true, OrderId = existingOrder, OrderNumber = number, LeadId = existing.LeadId, Status = "Pending", Message = "This order was already received." };
            }

            var offering = await db.StorefrontOfferings.FirstOrDefaultAsync(x => x.Id == request.OfferingId && x.IsPublished && x.Kind == "product", cancellationToken)
                ?? throw new NotFoundException("That product is not available.");
            var product = offering.ProductId is Guid productId
                ? await db.Products.FirstOrDefaultAsync(x => x.Id == productId && x.IsActive, cancellationToken)
                : null;
            if (product is null)
                throw new NotFoundException("That product is not available.");
            var physical = !product.Kind.Equals("Digital", StringComparison.OrdinalIgnoreCase) && !product.Kind.Equals("Service", StringComparison.OrdinalIgnoreCase);
            if (physical && string.IsNullOrWhiteSpace(request.Address))
                throw new BadRequestException("A delivery address is required.");
            if (physical)
            {
                var hasInventory = await db.Inventories.AnyAsync(x => x.ProductId == product.Id, cancellationToken);
                if (hasInventory)
                {
                    var adjusted = await db.TryAdjustStockAsync(product.Id, -quantity, cancellationToken);
                    if (!adjusted.Applied)
                        throw new ConflictException("That quantity is not available.");
                }
                else if (product.CurrentStock < quantity)
                    throw new ConflictException("That quantity is not available.");
                else
                    product.CurrentStock -= quantity;
            }

            var customer = await FindOrCreateCustomerAsync(db, request.Name, request.Email, request.Phone, request.Address, request.City, request.Country, request.PostalCode, tenant.Country, cancellationToken);
            var total = decimal.Round(product.SalePrice * quantity, 2, MidpointRounding.AwayFromZero);
            var order = new Order
            {
                Customer = customer,
                CustomerId = customer.Id,
                OrderNumber = "SF-" + DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..6],
                OrderDate = DateTime.UtcNow,
                Status = OrderStatusNames.Pending,
                TotalAmount = total,
                GrandTotal = total,
                PaymentStatus = "Unpaid",
                ShippingAddress = Clip(request.Address, 300),
                RowVersion = new byte[8]
            };
            order.OrderItems.Add(new OrderItem
            {
                ProductId = product.Id,
                Quantity = quantity,
                UnitPrice = product.SalePrice,
                UnitCost = product.CostPrice,
                Total = total
            });
            db.Orders.Add(order);
            var lead = await CreateLeadAsync(db, request.Name, request.Email, request.Phone, "Public storefront order", request.ClientToken, $"Order {order.OrderNumber} for {product.Name}.", cancellationToken);
            db.StorefrontInquiries.Add(new StorefrontInquiry
            {
                Name = Required(request.Name, "Name", 120),
                Email = NormalizeEmail(request.Email),
                Phone = Clip(request.Phone, 40),
                OfferingId = offering.Id,
                Message = $"Order for {quantity} x {product.Name}.",
                Kind = "order",
                LeadId = lead.Id,
                OrderId = order.Id,
                ClientToken = Clip(request.ClientToken, 80)
            });
            NotifyOwner(db, tenant, "New storefront order", $"{request.Name} ordered {product.Name}.");
            await db.SaveChangesAsync(cancellationToken);
            return new PublicSubmissionDto
            {
                Accepted = true,
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                LeadId = lead.Id,
                Status = order.Status,
                Message = "Order received. Payment is not collected on the public storefront."
            };
        }
    }

    public async Task<PublicSubmissionDto> SubmitReviewAsync(string slug, PublicReviewRequest request, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(request.CompanyWebsite))
            return new PublicSubmissionDto { Accepted = true, Message = "Received." };
        var (db, _) = await OpenPublicAsync(slug, cancellationToken);
        await using (db)
        {
            if (request.ReviewId is Guid reviewId && !string.IsNullOrWhiteSpace(request.ReportReason))
            {
                var existing = await db.StorefrontReviews.FirstOrDefaultAsync(x => x.Id == reviewId, cancellationToken)
                    ?? throw new NotFoundException("Review was not found.");
                existing.Status = "Reported";
                existing.ReportReason = Clip(request.ReportReason, 400);
                await db.SaveChangesAsync(cancellationToken);
                return new PublicSubmissionDto { Accepted = true, Status = "Reported", Message = "The review was reported for moderation." };
            }
            if (!request.Consent)
                throw new BadRequestException("Consent is required.");
            if (request.Rating is < 1 or > 5)
                throw new BadRequestException("Rating must be from 1 to 5.");
            var review = new StorefrontReview
            {
                DisplayName = Required(request.DisplayName, "Name", 80),
                Rating = request.Rating,
                Body = Required(request.Body, "Review", 2000),
                Status = "Pending"
            };
            db.StorefrontReviews.Add(review);
            await db.SaveChangesAsync(cancellationToken);
            return new PublicSubmissionDto { Accepted = true, Status = "Pending", Message = "Thank you. The company reviews submissions before they appear." };
        }
    }

    public async Task<SupplierPortalDto> GetSupplierPortalAsync(string token, CancellationToken cancellationToken = default)
    {
        await using var lookup = await _factory.CreateDbContextAsync(cancellationToken);
        var hash = Hash(token ?? "");
        var access = await lookup.SupplierPortalAccesses.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TokenHash == hash && !x.IsDeleted, cancellationToken)
            ?? throw new UnauthorizedException("This supplier link is invalid.");
        if (access.IsRevoked || access.ExpiresAt <= DateTime.UtcNow)
            throw new UnauthorizedException("This supplier link has expired.");
        _tenant.SetTenantId(access.TenantId);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == access.SupplierId, cancellationToken)
            ?? throw new UnauthorizedException("This supplier link is invalid.");
        var company = await db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == access.TenantId)
            .Select(x => x.Name)
            .FirstAsync(cancellationToken);
        var orders = await db.Purchases.AsNoTracking()
            .Where(x => x.SupplierId == supplier.Id)
            .OrderByDescending(x => x.PurchaseDate)
            .Take(50)
            .Select(x => new SupplierPortalOrderDto
            {
                Reference = x.ReferenceNumber,
                Date = x.PurchaseDate,
                Status = x.Status,
                Total = x.TotalAmount
            })
            .ToListAsync(cancellationToken);
        return new SupplierPortalDto
        {
            SupplierName = supplier.Name,
            CompanyName = company,
            ExpiresAt = access.ExpiresAt,
            Orders = orders
        };
    }

    public async Task<string> BuildSitemapAsync(string origin, CancellationToken cancellationToken = default)
    {
        var root = origin?.Trim().TrimEnd('/') ?? "";
        if (!root.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !root.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            root = "";
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var published = await db.CompanyStorefronts.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.IsPublished && !x.IsDeleted)
            .Select(x => x.TenantId)
            .ToListAsync(cancellationToken);
        var slugs = await db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .Where(x => published.Contains(x.Id) && !x.IsDeleted && x.IsActive && (x.Status == TenantStatus.Active || x.Status == TenantStatus.Trial))
            .Select(x => x.Slug)
            .ToListAsync(cancellationToken);
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var urlset = new XElement(ns + "urlset",
            slugs.Select(slug => new XElement(ns + "url",
                new XElement(ns + "loc", string.IsNullOrEmpty(root) ? "/company/" + slug : root + "/company/" + slug))));
        return new XDocument(urlset).ToString();
    }

    private async Task<PublicSubmissionDto> SubmitLeadAsync(string slug, PublicInquiryRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.CompanyWebsite))
            return new PublicSubmissionDto { Accepted = true, Message = "Received." };
        if (!request.Consent)
            throw new BadRequestException("Consent is required.");
        var kind = request.Kind.Equals("booking", StringComparison.OrdinalIgnoreCase) ? "booking" : "inquiry";
        var (db, tenant) = await OpenPublicAsync(slug, cancellationToken);
        await using (db)
        {
            var existing = await FindTokenAsync(db, request.ClientToken, cancellationToken);
            if (existing is not null)
                return new PublicSubmissionDto { Accepted = true, LeadId = existing.LeadId, Status = "Received", Message = "This request was already received." };
            Guid? offeringId = null;
            string? offeringName = null;
            if (request.OfferingId is Guid id)
            {
                var offering = await db.StorefrontOfferings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.IsPublished, cancellationToken)
                    ?? throw new BadRequestException("That product or service is not public.");
                offeringId = offering.Id;
                offeringName = offering.Name;
            }
            var source = kind == "booking" ? "Public storefront booking" : "Public storefront";
            var notes = new StringBuilder();
            if (offeringName is not null)
                notes.Append("Interest: ").Append(offeringName).Append(". ");
            if (!string.IsNullOrWhiteSpace(request.BudgetRange))
                notes.Append("Budget: ").Append(Clip(request.BudgetRange, 80)).Append(". ");
            if (!string.IsNullOrWhiteSpace(request.Requirements))
                notes.Append(Clip(request.Requirements, 2000)).Append(' ');
            notes.Append(Required(request.Message, "Message", 4000));
            var lead = await CreateLeadAsync(db, request.Name, request.Email, request.Phone, source, request.ClientToken, notes.ToString(), cancellationToken);
            db.StorefrontInquiries.Add(new StorefrontInquiry
            {
                Name = Required(request.Name, "Name", 120),
                Email = NormalizeEmail(request.Email),
                Phone = Clip(request.Phone, 40),
                OfferingId = offeringId,
                PackageId = request.PackageId,
                BudgetRange = Clip(request.BudgetRange, 80),
                Requirements = Clip(request.Requirements, 4000),
                Message = Required(request.Message, "Message", 4000),
                Kind = kind,
                LeadId = lead.Id,
                ClientToken = Clip(request.ClientToken, 80)
            });
            NotifyOwner(db, tenant, kind == "booking" ? "New booking request" : "New storefront inquiry", $"{request.Name} sent a {kind}.");
            await db.SaveChangesAsync(cancellationToken);
            return new PublicSubmissionDto { Accepted = true, LeadId = lead.Id, Status = "Received", Message = "The company has received your request." };
        }
    }

    private async Task<(BusinessOSDbContext Db, Tenant Tenant)> OpenPublicAsync(string slug, CancellationToken cancellationToken)
    {
        await using var lookup = await _factory.CreateDbContextAsync(cancellationToken);
        var (tenant, redirect) = await ResolveSlugAsync(lookup, slug, cancellationToken);
        if (redirect is not null)
            throw new BadRequestException("This company address has changed.");
        if (tenant is null || !IsPublicTenant(tenant))
            throw new NotFoundException("Company was not found.");
        var published = await lookup.CompanyStorefronts.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(x => x.TenantId == tenant.Id && !x.IsDeleted && x.IsPublished, cancellationToken);
        if (!published)
            throw new NotFoundException("This storefront is not published.");
        _tenant.SetTenantId(tenant.Id);
        var db = await _factory.CreateDbContextAsync(cancellationToken);
        var scoped = await db.Tenants.FirstOrDefaultAsync(cancellationToken) ?? tenant;
        return (db, scoped);
    }

    private async Task<IReadOnlyList<DashboardWidgetDto>> WidgetsAsync(CancellationToken cancellationToken)
    {
        var defaults = DefaultWidgets();
        var row = await _db.DashboardPreferences.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == _user.UserId, cancellationToken);
        if (row is null || string.IsNullOrWhiteSpace(row.WidgetsJson))
            return defaults;
        try
        {
            var saved = JsonSerializer.Deserialize<List<DashboardWidgetDto>>(row.WidgetsJson, JsonOptions) ?? [];
            var allowed = defaults.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
            var ordered = new List<DashboardWidgetDto>();
            foreach (var widget in saved)
            {
                if (!allowed.TryGetValue(widget.Id, out var known) || ordered.Any(x => x.Id == known.Id))
                    continue;
                ordered.Add(known with { Size = Sizes.Contains(widget.Size) ? widget.Size : known.Size, Visible = widget.Visible });
            }
            ordered.AddRange(defaults.Where(x => ordered.All(savedWidget => savedWidget.Id != x.Id)));
            return ordered;
        }
        catch (JsonException)
        {
            return defaults;
        }
    }

    private List<DashboardWidgetDto> DefaultWidgets()
    {
        var allowed = AllowedWidgets();
        var preferred = PreferredWidgetIds();
        var ordered = preferred.Select(id => allowed.FirstOrDefault(x => x.Id == id)).Where(x => x is not null).Cast<DashboardWidgetDto>().ToList();
        ordered.AddRange(allowed.Where(x => ordered.All(y => y.Id != x.Id)).Select(x => x with { Visible = false }));
        return ordered;
    }

    private IReadOnlyList<string> PreferredWidgetIds()
    {
        var roles = _user.Roles;
        var onlyEmployee = roles.Count > 0 && roles.All(role => role is "Employee" or "Agent");
        if (onlyEmployee)
            return ["tasks", "activity", "projects", "insights"];
        var onlySales = roles.Contains("Sales") && roles.All(role => role is "Sales" or "Viewer");
        if (onlySales)
            return ["sales", "customers", "orders", "insights"];
        return ["performance", "sales", "customers", "orders", "projects", "employees", "ai", "automation", "insights", "activity", "tasks", "products"];
    }

    private List<DashboardWidgetDto> AllowedWidgets()
    {
        var widgets = new List<DashboardWidgetDto>();
        void Add(string id, string label, bool allowed) { if (allowed) widgets.Add(new DashboardWidgetDto(id, label, "md", true)); }
        Add("performance", "Revenue and profit", _user.HasPermission(PermissionCodes.FinanceView) || _user.HasPermission(PermissionCodes.OrderView));
        Add("sales", "Sales", _user.HasPermission(PermissionCodes.OrderView));
        Add("customers", "Customers", _user.HasPermission(PermissionCodes.CustomerView));
        Add("orders", "Orders", _user.HasPermission(PermissionCodes.OrderView));
        Add("products", "Products", _user.HasPermission(PermissionCodes.ProductView));
        Add("projects", "Projects and tasks", _user.HasPermission(PermissionCodes.ProjectView) || _user.HasPermission(PermissionCodes.TaskView));
        Add("employees", "Employees", _user.HasPermission(PermissionCodes.EmployeeView));
        Add("ai", "AI workforce", _user.HasPermission(PermissionCodes.NalvaView));
        Add("automation", "Automation", _user.HasPermission(PermissionCodes.NalvaView));
        Add("insights", "Business insights", true);
        Add("activity", "Activity", _user.HasPermission(PermissionCodes.ActivityView));
        Add("tasks", "Follow-ups", true);
        return widgets;
    }

    private async Task<IReadOnlyList<BusinessInsightDto>> InsightsAsync(CancellationToken cancellationToken)
    {
        var insights = new List<BusinessInsightDto>();
        var now = DateTime.UtcNow;
        var currency = await _db.Tenants.AsNoTracking().Select(x => x.Currency).FirstOrDefaultAsync(cancellationToken) ?? "USD";
        if (string.IsNullOrWhiteSpace(currency)) currency = "USD";

        if (_user.HasPermission(PermissionCodes.OrderView))
        {
            // Align with owner KPI: completed-order revenue for last 30 days vs prior 30.
            var start = now.AddDays(-30);
            var previousStart = start.AddDays(-30);
            var current = await _db.Orders
                .Where(x => x.OrderDate >= start && x.Status == OrderStatusNames.Completed)
                .SumAsync(x => (decimal?)x.GrandTotal, cancellationToken) ?? 0;
            var previous = await _db.Orders
                .Where(x => x.OrderDate >= previousStart && x.OrderDate < start && x.Status == OrderStatusNames.Completed)
                .SumAsync(x => (decimal?)x.GrandTotal, cancellationToken) ?? 0;
            if (current > 0 || previous > 0)
            {
                decimal? change = previous == 0 ? null : Math.Round((current - previous) / previous * 100m, 1);
                var direction = change is null ? "changed"
                    : change > 0 ? "increased"
                    : change < 0 ? "decreased"
                    : "held steady";
                var changeText = change is null
                    ? "with no comparable sales in the prior period"
                    : change == 0
                        ? "matching the previous 30 days"
                        : $"by {Math.Abs(change.Value):0.#}% compared with the previous 30 days";
                insights.Add(new BusinessInsightDto
                {
                    Code = "sales-opportunity",
                    Title = change is > 0 ? "Sales are up" : change is < 0 ? "Sales slowed" : "Sales overview",
                    Message = $"Your completed sales {direction} {changeText}. Current period: {Money(current, currency)}.",
                    Severity = change is > 0 ? "positive" : change is < 0 ? "warning" : "info",
                    MetricLabel = "Revenue (30 days)",
                    MetricValue = Money(current, currency),
                    RecommendedAction = "Review sales performance",
                    Route = "/sales",
                    UpdatedAt = now
                });
            }
        }

        if (_user.HasPermission(PermissionCodes.FinanceView) || _user.HasPermission(PermissionCodes.InvoiceView))
        {
            var overdueRows = await _db.Invoices.AsNoTracking()
                .Where(x => x.DueDate < now && x.OutstandingAmount > 0 && x.Status != "Paid" && x.Status != "Void" && x.Status != "Cancelled")
                .GroupBy(_ => 1)
                .Select(g => new { Count = g.Count(), Total = g.Sum(x => x.OutstandingAmount) })
                .FirstOrDefaultAsync(cancellationToken);
            if (overdueRows is { Count: > 0 })
            {
                insights.Add(new BusinessInsightDto
                {
                    Code = "outstanding-payments",
                    Title = "Outstanding payments",
                    Message = overdueRows.Count == 1
                        ? $"1 customer has an overdue balance totaling {Money(overdueRows.Total, currency)}."
                        : $"{overdueRows.Count} invoices are overdue, totaling {Money(overdueRows.Total, currency)}.",
                    Severity = "critical",
                    MetricLabel = "Overdue",
                    MetricValue = Money(overdueRows.Total, currency),
                    RecommendedAction = "Follow up on overdue invoices",
                    Route = "/invoices",
                    UpdatedAt = now
                });
            }
        }

        if (_user.HasPermission(PermissionCodes.InventoryView) || _user.HasPermission(PermissionCodes.ProductView))
        {
            // Prefer Inventories as the stock source of truth (matches overview KPIs).
            var low = await (
                from inv in _db.Inventories.AsNoTracking()
                join product in _db.Products.AsNoTracking() on inv.ProductId equals product.Id
                where inv.ReorderLevel > 0 && inv.CurrentStock > 0 && inv.CurrentStock <= inv.ReorderLevel
                orderby inv.CurrentStock
                select new { product.Name, inv.CurrentStock, inv.ReorderLevel }
            ).Take(5).ToListAsync(cancellationToken);
            var lowCount = await _db.Inventories.AsNoTracking()
                .CountAsync(x => x.ReorderLevel > 0 && x.CurrentStock > 0 && x.CurrentStock <= x.ReorderLevel, cancellationToken);
            if (lowCount > 0 && low.Count > 0)
            {
                var first = low[0];
                var names = string.Join(", ", low.Take(3).Select(x => x.Name));
                insights.Add(new BusinessInsightDto
                {
                    Code = "inventory-alert",
                    Title = "Inventory alert",
                    Message = lowCount == 1
                        ? $"{first.Name} is below its reorder level ({first.CurrentStock:0.##} on hand; reorder at {first.ReorderLevel:0.##})."
                        : $"{lowCount} products are below their reorder levels, including {names}.",
                    Severity = "warning",
                    MetricLabel = "Low stock",
                    MetricValue = lowCount.ToString(),
                    RecommendedAction = "Review low-stock products",
                    Route = "/inventory",
                    UpdatedAt = now
                });
            }

            // Top seller running out — only when velocity + stock data support it.
            var monthStart = now.AddDays(-30);
            var topSeller = await (
                from item in _db.OrderItems.AsNoTracking()
                join order in _db.Orders.AsNoTracking() on item.OrderId equals order.Id
                join product in _db.Products.AsNoTracking() on item.ProductId equals product.Id
                where order.OrderDate >= monthStart && order.Status == OrderStatusNames.Completed
                group item by new { item.ProductId, product.Name } into g
                orderby g.Sum(x => x.Quantity) descending
                select new { g.Key.ProductId, g.Key.Name, Qty = g.Sum(x => x.Quantity) }
            ).FirstOrDefaultAsync(cancellationToken);

            if (topSeller is not null && topSeller.Qty >= 5)
            {
                var stock = await _db.Inventories.AsNoTracking()
                    .Where(x => x.ProductId == topSeller.ProductId)
                    .Select(x => new { x.CurrentStock, x.ReorderLevel })
                    .FirstOrDefaultAsync(cancellationToken);
                if (stock is not null && stock.CurrentStock > 0 && stock.CurrentStock <= Math.Max(stock.ReorderLevel, 5))
                {
                    var daily = topSeller.Qty / 30m;
                    if (daily > 0 && stock.CurrentStock / daily <= 7)
                    {
                        insights.Add(new BusinessInsightDto
                        {
                            Code = "top-product-stock",
                            Title = "Top product may run out soon",
                            Message = $"{topSeller.Name} is selling about {daily:0.#} units/day with {stock.CurrentStock:0.##} left — roughly {stock.CurrentStock / daily:0.#} days of cover.",
                            Severity = "warning",
                            MetricLabel = "Units left",
                            MetricValue = $"{stock.CurrentStock:0.##}",
                            RecommendedAction = "Reorder this product",
                            Route = "/inventory",
                            UpdatedAt = now
                        });
                    }
                }
            }
        }

        if (_user.HasPermission(PermissionCodes.FinanceView))
        {
            var start = now.AddDays(-30);
            var previousStart = start.AddDays(-30);
            var currentExp = await _db.Expenses.Where(x => x.ExpenseDate >= start).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
            var previousExp = await _db.Expenses.Where(x => x.ExpenseDate >= previousStart && x.ExpenseDate < start).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
            if (previousExp > 0 && currentExp > previousExp)
            {
                var pct = Math.Round((currentExp - previousExp) / previousExp * 100m, 1);
                if (pct >= 10)
                {
                    insights.Add(new BusinessInsightDto
                    {
                        Code = "expense-warning",
                        Title = "Operating expenses rose",
                        Message = $"Your operating expenses increased by {pct:0.#}% this month ({Money(currentExp, currency)} vs {Money(previousExp, currency)}).",
                        Severity = "warning",
                        MetricLabel = "Expenses (30 days)",
                        MetricValue = Money(currentExp, currency),
                        RecommendedAction = "Review expense categories",
                        Route = "/expenses",
                        UpdatedAt = now
                    });
                }
            }
        }

        if (_user.HasPermission(PermissionCodes.LeadView))
        {
            var followUps = await _db.FollowUps.CountAsync(x => x.Status == "Open" && x.DueAt < now, cancellationToken);
            if (followUps > 0)
            {
                insights.Add(new BusinessInsightDto
                {
                    Code = "follow-ups",
                    Title = "Overdue follow-ups",
                    Message = followUps == 1 ? "1 sales follow-up is overdue." : $"{followUps} sales follow-ups are overdue.",
                    Severity = "warning",
                    MetricLabel = "Follow-ups",
                    MetricValue = followUps.ToString(),
                    RecommendedAction = "Complete overdue follow-ups",
                    Route = "/crm/follow-ups",
                    UpdatedAt = now
                });
            }
        }

        if (_user.HasPermission(PermissionCodes.ProjectView))
        {
            var names = await _db.Projects.AsNoTracking()
                .Where(x => x.Deadline != null && x.Deadline < now && (x.Status == ProjectStatus.Active || x.Status == ProjectStatus.Planning))
                .OrderBy(x => x.Deadline)
                .Select(x => x.Name)
                .Take(3)
                .ToListAsync(cancellationToken);
            if (names.Count > 0)
            {
                insights.Add(new BusinessInsightDto
                {
                    Code = "project-deadline",
                    Title = "Project deadlines",
                    Message = names.Count == 1
                        ? $"{names[0]} is past its planned deadline."
                        : $"{names[0]} and {names.Count - 1} other open project{(names.Count == 2 ? "" : "s")} are past their planned deadlines.",
                    Severity = "warning",
                    RecommendedAction = "Review project timelines",
                    Route = "/projects",
                    UpdatedAt = now
                });
            }
        }

        return insights;
    }

    private async Task FillOperationsAsync(DashboardExperienceDto dto, CancellationToken cancellationToken)
    {
        if (_user.HasPermission(PermissionCodes.OrderView))
        {
            var counts = await _db.Orders.GroupBy(x => x.Status).Select(x => new { x.Key, Count = x.Count() }).ToListAsync(cancellationToken);
            int Count(params string[] names) => counts.Where(x => names.Contains(x.Key, StringComparer.OrdinalIgnoreCase)).Sum(x => x.Count);
            dto.PendingOrders = Count(OrderStatusNames.Pending);
            dto.ProcessingOrders = Count(OrderStatusNames.Processing, OrderStatusNames.Confirmed);
            dto.CompletedOrders = Count(OrderStatusNames.Completed);
            dto.CancelledOrders = Count(OrderStatusNames.Cancelled);
        }
        if (_user.HasPermission(PermissionCodes.FinanceView))
        {
            var now = DateTime.UtcNow;
            var start = now.AddDays(-30);
            var previousStart = start.AddDays(-30);
            // Same recognized-sales definition as dashboard overview / finance KPIs.
            dto.Revenue = await _db.Orders.Where(x => x.OrderDate >= start && x.Status == OrderStatusNames.Completed).SumAsync(x => (decimal?)x.GrandTotal, cancellationToken) ?? 0;
            dto.PreviousRevenue = await _db.Orders.Where(x => x.OrderDate >= previousStart && x.OrderDate < start && x.Status == OrderStatusNames.Completed).SumAsync(x => (decimal?)x.GrandTotal, cancellationToken) ?? 0;
            dto.Expenses = await _db.Expenses.Where(x => x.ExpenseDate >= start).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
            dto.PreviousExpenses = await _db.Expenses.Where(x => x.ExpenseDate >= previousStart && x.ExpenseDate < start).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
        }
        if (_user.HasPermission(PermissionCodes.ProjectView) || _user.HasPermission(PermissionCodes.TaskView))
        {
            dto.ActiveProjects = await _db.Projects.CountAsync(x => x.Status == ProjectStatus.Active || x.Status == ProjectStatus.Planning, cancellationToken);
            dto.CompletedProjects = await _db.Projects.CountAsync(x => x.Status == ProjectStatus.Completed, cancellationToken);
            dto.OverdueTasks = await _db.WorkTasks.CountAsync(x => x.DueDate != null && x.DueDate < DateTime.UtcNow && x.Status != WorkTaskStatus.Done && x.Status != WorkTaskStatus.Cancelled, cancellationToken);
        }
        if (_user.HasPermission(PermissionCodes.EmployeeView))
        {
            dto.TotalEmployees = await _db.Employees.CountAsync(cancellationToken);
            dto.ActiveEmployees = await _db.Employees.CountAsync(x => x.IsActive, cancellationToken);
            if (_user.HasPermission(PermissionCodes.LeaveManage))
                dto.PendingLeave = await _db.LeaveRequests.CountAsync(x => x.Status == "Pending", cancellationToken);
        }
        if (_user.HasPermission(PermissionCodes.NalvaView))
        {
            var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            dto.ActiveAgents = await _db.NalvaCompanyAgents.CountAsync(x => x.Status == NalvaStatuses.Active, cancellationToken);
            dto.RunningTasks = await _db.NalvaTasks.CountAsync(x => x.Status == NalvaStatuses.Running, cancellationToken);
            dto.CompletedTasks = await _db.NalvaTasks.CountAsync(x => x.Status == NalvaStatuses.Completed, cancellationToken);
            dto.FailedExecutions = await _db.NalvaExecutions.CountAsync(x => x.Status == NalvaStatuses.Failed, cancellationToken);
            dto.PendingApprovals = await _db.NalvaApprovals.CountAsync(x => x.Status == NalvaStatuses.Pending, cancellationToken);
            dto.EstimatedAiCostUsd = await _db.NalvaExecutions.Where(x => x.StartedAt >= month).SumAsync(x => (decimal?)x.EstimatedCostUsd, cancellationToken) ?? 0;
            dto.AiCostIsEstimate = true;
            dto.ActiveWorkflows = await _db.NalvaWorkflowDefinitions.CountAsync(x => x.IsActive, cancellationToken);
            dto.ScheduledWorkflows = await _db.NalvaWorkflowDefinitions.CountAsync(x => x.IsActive && x.IntervalMinutes != null, cancellationToken);
            dto.SuccessfulWorkflowRuns = await _db.NalvaWorkflowRuns.CountAsync(x => x.Status == NalvaStatuses.Completed, cancellationToken);
            dto.FailedWorkflowRuns = await _db.NalvaWorkflowRuns.CountAsync(x => x.Status == NalvaStatuses.Failed, cancellationToken);
        }
    }

    private async Task<CompanyStorefront> EnsureStorefrontAsync(CancellationToken cancellationToken)
    {
        var row = await _db.CompanyStorefronts.FirstOrDefaultAsync(cancellationToken);
        if (row is not null)
            return row;
        row = new CompanyStorefront();
        _db.CompanyStorefronts.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return row;
    }

    private async Task<StorefrontAdminDto> MapAdminAsync(BusinessOSDbContext db, CancellationToken cancellationToken)
    {
        var tenant = await CurrentTenantAsync(cancellationToken);
        var row = await db.CompanyStorefronts.AsNoTracking().FirstAsync(cancellationToken);
        var offerings = await db.StorefrontOfferings.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync(cancellationToken);
        var productIds = offerings.Where(x => x.ProductId != null).Select(x => x.ProductId!.Value).ToList();
        var products = await db.Products.AsNoTracking().Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        return new StorefrontAdminDto
        {
            CompanyName = tenant.Name,
            Slug = tenant.Slug,
            PublicPath = "/company/" + tenant.Slug,
            IsPublished = row.IsPublished,
            Tagline = row.Tagline,
            PublicDescription = row.PublicDescription,
            CoverImageUrl = row.CoverImageUrl,
            LogoUrl = tenant.LogoUrl,
            PublicEmail = row.PublicEmail,
            PublicPhone = row.PublicPhone,
            Website = row.Website,
            Location = row.Location,
            Industry = row.Industry,
            Category = row.Category,
            SocialLinksJson = row.SocialLinksJson,
            HoursJson = row.HoursJson,
            BrandColor = row.BrandColor,
            Layout = row.Layout,
            ShowProducts = row.ShowProducts,
            ShowServices = row.ShowServices,
            ShowPortfolio = row.ShowPortfolio,
            ShowReviews = row.ShowReviews,
            SeoTitle = row.SeoTitle,
            SeoDescription = row.SeoDescription,
            ConsentNotice = row.ConsentNotice,
            PreferredWorkspace = row.PreferredWorkspace,
            Offerings = offerings.Select(x => MapOffering(x, x.ProductId is Guid id && products.TryGetValue(id, out var product) ? product : null)).ToList(),
            Packages = (await db.StorefrontPackages.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync(cancellationToken)).Select(MapPackage).ToList(),
            Portfolio = (await db.StorefrontPortfolioItems.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken)).Select(MapPortfolio).ToList(),
            Reviews = (await db.StorefrontReviews.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken)).Select(MapReview).ToList()
        };
    }

    private WorkspaceExperienceDto DescribeWorkspace(Tenant tenant, string workspace)
    {
        var links = workspace.ToLowerInvariant() switch
        {
            "agency" => Links(("Overview", "/dashboard"), ("Clients", "/customers"), ("Leads", "/crm/leads"), ("Projects", "/projects"), ("Services", "/storefront"), ("Proposals", "/quotations"), ("Tasks", "/projects/board"), ("Team", "/people/employees"), ("Finance", "/finance"), ("AI workforce", "/nalva/command")),
            "retail" => Links(("Overview", "/dashboard"), ("Products", "/products"), ("Inventory", "/inventory"), ("Warehouses", "/warehouses"), ("Suppliers", "/suppliers"), ("Purchase orders", "/purchase-orders"), ("Sales", "/sales"), ("Orders", "/orders"), ("Customers", "/customers"), ("Returns", "/returns"), ("Expenses", "/expenses")),
            "service" => Links(("Overview", "/dashboard"), ("Services", "/storefront"), ("Customers", "/customers"), ("Quotations", "/quotations"), ("Projects", "/projects"), ("Employees", "/people/employees"), ("Invoices", "/invoices"), ("Payments", "/payments")),
            "ecommerce" => Links(("Overview", "/dashboard"), ("Products", "/products"), ("Orders", "/orders"), ("Customers", "/customers"), ("Inventory", "/inventory"), ("Returns", "/returns"), ("Storefront", "/storefront"), ("AI workforce", "/nalva")),
            _ => Links(("Overview", "/dashboard"), ("Customers", "/customers"), ("Orders", "/orders"), ("Finance", "/finance"), ("Projects", "/projects"), ("People", "/people/employees"), ("AI workforce", "/nalva/command"), ("Storefront", "/storefront"))
        };
        return new WorkspaceExperienceDto
        {
            BusinessType = tenant.BusinessType,
            Workspace = workspace.ToLowerInvariant(),
            EnabledModules = ModuleList.Parse(tenant.EnabledModulesJson),
            SuggestedNavigation = links,
            Note = "Every module you are allowed to use stays in the sidebar. This workspace only changes which work is highlighted."
        };
    }

    private static List<WorkspaceLinkDto> Links(params (string Label, string Route)[] items) =>
        items.Select(item => new WorkspaceLinkDto { Label = item.Label, Route = item.Route }).ToList();

    private static string WorkspaceFor(string? businessType) => businessType?.ToLowerInvariant() switch
    {
        "software-agency" or "it-consultancy" or "marketing-agency" => "agency",
        "retail" => "retail",
        "service" => "service",
        "ecommerce" => "ecommerce",
        _ => "general"
    };

    private async Task ChangeSlugAsync(Tenant tenant, string slug, CancellationToken cancellationToken)
    {
        var next = slug.Trim().ToLowerInvariant();
        if (next == tenant.Slug)
            return;
        if (next.Length is < 3 or > 60 || !System.Text.RegularExpressions.Regex.IsMatch(next, "^[a-z0-9]+(?:-[a-z0-9]+)*$") || ReservedSlugs.Contains(next))
            throw new BadRequestException("Use 3 to 60 lowercase letters, numbers, and single hyphens.");
        var taken = await _db.Tenants.IgnoreQueryFilters().AnyAsync(x => x.Slug == next && x.Id != tenant.Id && !x.IsDeleted, cancellationToken)
            || await _db.StorefrontSlugRedirects.IgnoreQueryFilters().AnyAsync(x => x.FromSlug == next && !x.IsDeleted, cancellationToken);
        if (taken)
            throw new ConflictException("That public address is already used.");
        _db.StorefrontSlugRedirects.Add(new StorefrontSlugRedirect { FromSlug = tenant.Slug });
        tenant.Slug = next;
    }

    private async Task<(Tenant? Tenant, string? Redirect)> ResolveSlugAsync(BusinessOSDbContext db, string slug, CancellationToken cancellationToken)
    {
        var key = (slug ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(key))
            return (null, null);
        var tenant = await db.Tenants.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(x => x.Slug == key && !x.IsDeleted, cancellationToken);
        if (tenant is not null)
            return (tenant, null);
        var redirect = await db.StorefrontSlugRedirects.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(x => x.FromSlug == key && !x.IsDeleted, cancellationToken);
        if (redirect is null)
            return (null, null);
        var target = await db.Tenants.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(x => x.Id == redirect.TenantId && !x.IsDeleted, cancellationToken);
        return (target, target?.Slug);
    }

    private static bool IsPublicTenant(Tenant tenant) =>
        tenant.IsActive && !tenant.IsDeleted && tenant.Status is TenantStatus.Active or TenantStatus.Trial;

    private async Task<StorefrontInquiry?> FindTokenAsync(BusinessOSDbContext db, string? token, CancellationToken cancellationToken)
    {
        var value = Clip(token, 80);
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return await db.StorefrontInquiries.FirstOrDefaultAsync(x => x.ClientToken == value, cancellationToken);
    }

    private async Task<Lead> CreateLeadAsync(BusinessOSDbContext db, string name, string email, string? phone, string source, string? token, string notes, CancellationToken cancellationToken)
    {
        var normalized = NormalizeEmail(email);
        var since = DateTime.UtcNow.AddHours(-12);
        var existing = await db.Leads.FirstOrDefaultAsync(x => x.Email == normalized && x.Source == source && x.CreatedAt >= since, cancellationToken);
        if (existing is not null && !string.IsNullOrWhiteSpace(token) && existing.Notes?.Contains(token, StringComparison.Ordinal) == true)
            return existing;
        var stageId = await db.PipelineStages.Where(x => x.IsActive).OrderBy(x => x.SortOrder).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
        var lead = new Lead
        {
            Name = Required(name, "Name", 120),
            Email = normalized,
            Phone = Clip(phone, 40),
            Source = source,
            Status = "New",
            Notes = Clip(notes, 4000),
            StageId = stageId
        };
        db.Leads.Add(lead);
        return lead;
    }

    private async Task<Customer> FindOrCreateCustomerAsync(BusinessOSDbContext db, string name, string email, string? phone, string? address, string? city, string? country, string? postalCode, string? tenantCountry, CancellationToken cancellationToken)
    {
        var normalized = NormalizeEmail(email);
        var existing = await db.Customers.FirstOrDefaultAsync(x => x.Email == normalized, cancellationToken);
        if (existing is not null)
            return existing;
        var parts = Required(name, "Name", 120).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var customer = new Customer
        {
            FirstName = parts[0],
            LastName = parts.Length > 1 ? parts[1] : "Customer",
            Email = normalized,
            PhoneNumber = string.IsNullOrWhiteSpace(phone) ? "Not provided" : Clip(phone, 40)!,
            Address = string.IsNullOrWhiteSpace(address) ? "Not provided" : Clip(address, 240)!,
            City = string.IsNullOrWhiteSpace(city) ? "Not provided" : Clip(city, 80)!,
            Country = string.IsNullOrWhiteSpace(country) ? (string.IsNullOrWhiteSpace(tenantCountry) ? "Not provided" : tenantCountry) : Clip(country, 80)!,
            PostalCode = string.IsNullOrWhiteSpace(postalCode) ? "00000" : Clip(postalCode, 20)!
        };
        db.Customers.Add(customer);
        return customer;
    }

    private static void NotifyOwner(BusinessOSDbContext db, Tenant tenant, string title, string message)
    {
        if (string.IsNullOrWhiteSpace(tenant.OwnerUserId))
            return;
        db.Notifications.Add(new Notification
        {
            UserId = tenant.OwnerUserId,
            Title = title,
            Message = message,
            Type = "Storefront",
            Link = "/crm/leads"
        });
    }

    private async Task<Tenant> CurrentTenantAsync(CancellationToken cancellationToken) =>
        await _db.Tenants.FirstOrDefaultAsync(cancellationToken) ?? throw new NotFoundException("Company was not found.");

    private void RequireUser()
    {
        if (string.IsNullOrWhiteSpace(_user.UserId))
            throw new UnauthorizedException("Sign in is required.");
    }

    private void RequireStorefront(bool manage)
    {
        RequireUser();
        if (manage)
        {
            if (_user.HasPermission(PermissionCodes.StorefrontManage) || _user.HasPermission(PermissionCodes.OrganizationManage) || _user.HasPermission(PermissionCodes.TenantManage))
                return;
            throw new ForbiddenException("You cannot edit the storefront.");
        }
        if (_user.HasPermission(PermissionCodes.StorefrontView) || _user.HasPermission(PermissionCodes.OrganizationView) || _user.HasPermission(PermissionCodes.StorefrontManage) || _user.HasPermission(PermissionCodes.OrganizationManage))
            return;
        throw new ForbiddenException("You cannot view the storefront.");
    }

    private static StorefrontOfferingDto MapOffering(StorefrontOffering row, Product? product) =>
        new(row.Id, row.Kind, row.ProductId, row.Name, row.Summary, row.Description, row.Category, row.Sku ?? product?.SKU,
            row.PricingModel, product?.SalePrice ?? row.Price, row.Deliverables, row.DeliveryTime, row.ImageUrl ?? product?.ImageUrl,
            row.IsFeatured, row.IsPublished, row.SortOrder, product is null ? null : product.Kind.Equals("Digital", StringComparison.OrdinalIgnoreCase) || product.CurrentStock > 0);

    private static StorefrontPackageDto MapPackage(StorefrontPackage row) =>
        new(row.Id, row.OfferingId, row.Name, row.Scope, row.Deliverables, row.PricingModel, row.Price, row.IsPublished, row.SortOrder);

    private static StorefrontPortfolioDto MapPortfolio(StorefrontPortfolioItem row) =>
        new(row.Id, row.Title, row.Summary, row.CoverImageUrl, row.Technologies, row.Industry, row.ProjectUrl, row.Results, row.IsPublished);

    private static StorefrontReviewDto MapReview(StorefrontReview row) =>
        new(row.Id, row.DisplayName, row.Rating, row.Body, row.Status, row.CreatedAt);

    private static string Money(decimal amount, string currency) =>
        amount.ToString("0.00", CultureInfo.InvariantCulture) + " " + currency;

    private static string Required(string? value, string label, int max)
    {
        var text = (value ?? "").Trim();
        if (text.Length < 2)
            throw new BadRequestException($"{label} is required.");
        return text.Length <= max ? text : text[..max];
    }

    private static string NormalizeEmail(string? email)
    {
        var value = (email ?? "").Trim().ToLowerInvariant();
        try
        {
            var parsed = new MailAddress(value);
            if (!parsed.Address.Contains('@'))
                throw new FormatException();
            return parsed.Address.ToLowerInvariant();
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            throw new BadRequestException("Enter a valid email address.");
        }
    }

    private static string? Clip(string? value, int max)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
            return null;
        return text.Length <= max ? text : text[..max];
    }

    private static string NormalizeColor(string? color)
    {
        var value = (color ?? "").Trim();
        return System.Text.RegularExpressions.Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$") ? value.ToLowerInvariant() : "#2563eb";
    }

    private static string? SafeHttp(string? value)
    {
        var text = Clip(value, 500);
        if (text is null)
            return null;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new BadRequestException("Links must start with http:// or https://.");
        return text;
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
