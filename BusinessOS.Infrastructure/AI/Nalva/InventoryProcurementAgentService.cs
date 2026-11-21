using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Audit.Services;
using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Application.Features.Inventory.Services;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Application.Features.ProcurementAgent;
using BusinessOS.Application.Features.PurchaseOrders.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Nalva;

public sealed class InventoryProcurementAgentService : IInventoryProcurementAgentService
{
    private static readonly HashSet<string> OpenPurchaseStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        PurchaseOrderStatusNames.Draft,
        PurchaseOrderStatusNames.Pending,
        PurchaseOrderStatusNames.Approved,
        PurchaseOrderStatusNames.PartiallyReceived
    };

    private readonly BusinessOSDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly ICurrentUserService _user;
    private readonly IInventoryService _inventory;
    private readonly ISupplierResearchProvider _research;
    private readonly IEntityAuditService _audit;
    private readonly ILogger<InventoryProcurementAgentService> _logger;

    public InventoryProcurementAgentService(
        BusinessOSDbContext db,
        ITenantProvider tenant,
        ICurrentUserService user,
        IInventoryService inventory,
        ISupplierResearchProvider research,
        IEntityAuditService audit,
        ILogger<InventoryProcurementAgentService> logger)
    {
        _db = db;
        _tenant = tenant;
        _user = user;
        _inventory = inventory;
        _research = research;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ProcurementOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.InventoryView, PermissionCodes.PurchaseOrderView, PermissionCodes.NalvaView);
        var risks = await GetInventoryRisksAsync(cancellationToken);
        var deliveries = await GetDeliveriesAsync(cancellationToken);
        var open = await _db.Purchases.CountAsync(x => OpenPurchaseStatuses.Contains(x.Status), cancellationToken);

        var products = await _db.Products.AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.Id, x.CostPrice })
            .ToListAsync(cancellationToken);
        var costById = products.ToDictionary(x => x.Id, x => x.CostPrice);
        var estimated = risks
            .Where(x => x.RiskKind is "low-stock" or "out-of-stock")
            .Sum(x => x.SuggestedReorderQuantity * (costById.TryGetValue(x.ProductId, out var c) ? c : 0m));

        return new ProcurementOverviewDto(
            ActiveProducts: products.Count,
            LowStockCount: risks.Count(x => x.RiskKind == "low-stock"),
            OutOfStockCount: risks.Count(x => x.RiskKind == "out-of-stock"),
            SlowMovingCount: risks.Count(x => x.RiskKind == "slow-moving"),
            OverstockCount: risks.Count(x => x.RiskKind == "overstock"),
            OpenPurchaseOrders: open,
            DelayedDeliveries: deliveries.Count(x => x.IsDelayed),
            EstimatedProcurementNeed: decimal.Round(estimated, 2),
            DataClassification: "REAL");
    }

    public async Task<IReadOnlyList<InventoryRiskDto>> GetInventoryRisksAsync(CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.InventoryView, PermissionCodes.NalvaView);
        var settings = await EnsureSettingsAsync(cancellationToken);
        var inventories = await _db.Inventories.AsNoTracking()
            .Where(x => x.Product.IsActive)
            .Select(x => new
            {
                x.ProductId,
                ProductName = x.Product.Name,
                Sku = x.Product.SKU,
                x.CurrentStock,
                x.ReorderLevel,
                x.MaximumStockLevel,
                x.Product.CostPrice
            })
            .ToListAsync(cancellationToken);

        var incoming = await LoadIncomingByProductAsync(cancellationToken);
        var sales30 = await LoadSalesUnitsAsync(30, cancellationToken);
        var sales90 = await LoadSalesUnitsAsync(Math.Max(settings.SlowMovingDays, 1), cancellationToken);
        var unusual = await LoadUnusualStockDeltasAsync(cancellationToken);

        var results = new List<InventoryRiskDto>();
        foreach (var row in inventories)
        {
            incoming.TryGetValue(row.ProductId, out var inbound);
            sales30.TryGetValue(row.ProductId, out var units30);
            var daily = units30 > 0 ? units30 / 30m : (decimal?)null;
            var effective = row.CurrentStock + inbound;
            var days = daily is > 0 ? effective / daily : null;
            DateTime? stockout = days is > 0 ? DateTime.UtcNow.Date.AddDays((double)Math.Floor(days.Value)) : null;
            var confidence = units30 >= settings.MinimumHistoryDays ? "medium" : units30 > 0 ? "low" : "unavailable";

            string? risk = null;
            var suggested = 0m;
            var explanation = "";

            if (row.CurrentStock <= 0)
            {
                risk = "out-of-stock";
                suggested = CalculateReorder(row.CurrentStock, row.ReorderLevel, row.MaximumStockLevel, daily, inbound);
                explanation = inbound > 0
                    ? $"Out of stock with {inbound:0.##} already on open purchase orders."
                    : "Out of stock with no open inbound quantity.";
            }
            else if (row.ReorderLevel > 0 && effective <= row.ReorderLevel)
            {
                risk = "low-stock";
                suggested = CalculateReorder(row.CurrentStock, row.ReorderLevel, row.MaximumStockLevel, daily, inbound);
                explanation = $"Effective available {effective:0.##} (on hand {row.CurrentStock:0.##} + inbound {inbound:0.##}) is at or below reorder level {row.ReorderLevel:0.##}.";
            }
            else if (row.CurrentStock > 0 && !sales90.ContainsKey(row.ProductId))
            {
                risk = "slow-moving";
                explanation = $"No recorded sales in the last {settings.SlowMovingDays} days while {row.CurrentStock:0.##} units remain.";
            }
            else if (row.MaximumStockLevel > 0 && row.CurrentStock > row.MaximumStockLevel * settings.OverstockMultiplier)
            {
                risk = "overstock";
                explanation = $"On-hand {row.CurrentStock:0.##} exceeds {settings.OverstockMultiplier:0.##}× maximum {row.MaximumStockLevel:0.##}.";
            }
            else if (unusual.Contains(row.ProductId))
            {
                risk = "discrepancy";
                explanation = "Unusual stock movement magnitude detected in recent transactions. Verify physical count.";
            }

            if (risk is null)
                continue;

            results.Add(new InventoryRiskDto(
                row.ProductId,
                row.ProductName,
                row.Sku,
                row.CurrentStock,
                row.ReorderLevel,
                row.MaximumStockLevel,
                inbound,
                effective,
                risk,
                daily,
                days,
                stockout,
                suggested,
                confidence,
                explanation));
        }

        return results
            .OrderBy(x => x.RiskKind switch
            {
                "out-of-stock" => 0,
                "low-stock" => 1,
                "discrepancy" => 2,
                "slow-moving" => 3,
                _ => 4
            })
            .ThenBy(x => x.DaysOfCover ?? decimal.MaxValue)
            .ToList();
    }

    public async Task<IReadOnlyList<DemandInsightDto>> AnalyzeDemandAsync(Guid? productId = null, CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.InventoryView, PermissionCodes.AnalyticsView, PermissionCodes.NalvaView);
        var settings = await EnsureSettingsAsync(cancellationToken);
        var query = _db.Inventories.AsNoTracking().Where(x => x.Product.IsActive);
        if (productId is Guid id)
            query = query.Where(x => x.ProductId == id);

        var rows = await query.Select(x => new
        {
            x.ProductId,
            ProductName = x.Product.Name,
            Sku = x.Product.SKU,
            x.CurrentStock,
            x.ReorderLevel,
            x.MaximumStockLevel
        }).ToListAsync(cancellationToken);

        var sales30 = await LoadSalesUnitsAsync(30, cancellationToken);
        var sales90 = await LoadSalesUnitsAsync(90, cancellationToken);
        var incoming = await LoadIncomingByProductAsync(cancellationToken);
        var insights = new List<DemandInsightDto>();

        foreach (var row in rows)
        {
            sales30.TryGetValue(row.ProductId, out var u30);
            sales90.TryGetValue(row.ProductId, out var u90);
            incoming.TryGetValue(row.ProductId, out var inbound);
            var orderDays = await CountDistinctOrderDaysAsync(row.ProductId, 90, cancellationToken);

            if (orderDays < settings.MinimumHistoryDays || u30 <= 0)
            {
                insights.Add(new DemandInsightDto(
                    row.ProductId, row.ProductName, row.Sku, u30, u90,
                    null, null, row.CurrentStock, null, null, 0m,
                    "unavailable",
                    $"Insufficient sales history ({orderDays} distinct order days in 90 days; need {settings.MinimumHistoryDays}). Forecast withheld.",
                    false));
                continue;
            }

            var daily = decimal.Round(u30 / 30m, 4);
            var monthly = decimal.Round(daily * 30m, 2);
            var cover = daily > 0 ? decimal.Round((row.CurrentStock + inbound) / daily, 1) : (decimal?)null;
            DateTime? stockout = cover is > 0 ? DateTime.UtcNow.Date.AddDays((double)Math.Floor(cover.Value)) : null;
            var leadDays = await EstimateLeadTimeDaysAsync(row.ProductId, cancellationToken);
            var suggested = CalculateReorder(row.CurrentStock, row.ReorderLevel, row.MaximumStockLevel, daily, inbound, leadDays);
            var confidence = orderDays >= 45 ? "high" : "medium";
            var leadNote = leadDays is > 0
                ? $" Historical PO→receive lead time ~{leadDays} day(s) included in reorder cover."
                : " No verified supplier lead-time history; default 14-day cover used.";

            insights.Add(new DemandInsightDto(
                row.ProductId, row.ProductName, row.Sku, u30, u90, daily, monthly,
                row.CurrentStock, cover, stockout, suggested, confidence,
                $"Assumes last-30-day completed non-cancelled order rate continues; inbound {inbound:0.##} counted toward cover.{leadNote}",
                true));
        }

        return insights.OrderBy(x => x.DaysOfCover ?? decimal.MaxValue).ToList();
    }

    public async Task<IReadOnlyList<ProcurementRecommendationDto>> GetRecommendationsAsync(CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.InventoryView, PermissionCodes.PurchaseOrderView, PermissionCodes.NalvaView);
        var risks = (await GetInventoryRisksAsync(cancellationToken))
            .Where(x => x.RiskKind is "low-stock" or "out-of-stock")
            .ToList();

        var list = new List<ProcurementRecommendationDto>();
        foreach (var risk in risks)
        {
            if (risk.SuggestedReorderQuantity <= 0)
                continue;

            var options = await CompareSuppliersAsync(risk.ProductId, risk.SuggestedReorderQuantity, cancellationToken);
            var preferred = PickPreferred(options, (await EnsureSettingsAsync(cancellationToken)).PreferredSupplierCriterion);
            var unit = preferred?.UnitPrice ?? 0m;
            var fingerprint = BuildFingerprint(preferred?.SupplierId, risk.ProductId, risk.SuggestedReorderQuantity, unit, preferred?.PaymentTerms, null);

            list.Add(new ProcurementRecommendationDto(
                Id: Guid.Empty,
                ProductId: risk.ProductId,
                ProductName: risk.ProductName,
                Sku: risk.Sku,
                CurrentStock: risk.CurrentStock,
                ReorderLevel: risk.ReorderLevel,
                IncomingQuantity: risk.IncomingQuantity,
                SuggestedQuantity: risk.SuggestedReorderQuantity,
                PreferredSupplierId: preferred?.SupplierId,
                PreferredSupplierName: preferred?.SupplierName,
                PreferredUnitPrice: preferred?.UnitPrice,
                EstimatedTotalCost: preferred?.TotalCost,
                Confidence: risk.Confidence,
                Reason: risk.Explanation + (preferred is null
                    ? " No stored supplier price available; add a supplier or historical purchase before preparing a PO."
                    : $" Preferred option: {preferred.SupplierName} at {preferred.UnitPrice:0.##} ({preferred.Classification})."),
                Status: preferred?.SupplierId is null ? "Blocked" : "Ready",
                ApprovalFingerprint: fingerprint,
                CreatedAt: DateTime.UtcNow));
        }

        return list;
    }

    public async Task<IReadOnlyList<SupplierOptionDto>> CompareSuppliersAsync(Guid productId, decimal quantity, CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.SupplierView, PermissionCodes.PurchaseOrderView, PermissionCodes.NalvaView);
        if (quantity <= 0)
            throw new BadRequestException("Quantity must be greater than zero.");

        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new NotFoundException("Product was not found.");

        var options = new List<SupplierOptionDto>();

        // REAL: historical purchase prices for this tenant's suppliers.
        var history = await _db.PurchaseItems.AsNoTracking()
            .Where(x => x.ProductId == productId && !x.IsDeleted)
            .OrderByDescending(x => x.Id)
            .Select(x => new { x.Purchase.SupplierId, SupplierName = x.Purchase.Supplier.Name, x.UnitPrice, x.Quantity, x.Purchase.PurchaseDate })
            .Take(40)
            .ToListAsync(cancellationToken);

        foreach (var group in history.GroupBy(x => x.SupplierId))
        {
            var latest = group.OrderByDescending(x => x.PurchaseDate).First();
            var qty = Math.Max(quantity, 1);
            options.Add(new SupplierOptionDto(
                latest.SupplierId,
                latest.SupplierName,
                "stored-purchase-history",
                "REAL",
                latest.UnitPrice,
                qty,
                1,
                decimal.Round(latest.UnitPrice * qty, 2),
                null,
                null,
                "Based on last recorded purchase price",
                "Historical price only; availability and lead time were not verified externally."));
        }

        // Also surface suppliers with no purchase history using product cost (labeled).
        var known = options.Select(x => x.SupplierId).Where(x => x.HasValue).Select(x => x!.Value).ToHashSet();
        var suppliers = await _db.Suppliers.AsNoTracking().OrderBy(x => x.Name).Take(25).ToListAsync(cancellationToken);
        foreach (var supplier in suppliers.Where(x => !known.Contains(x.Id)))
        {
            if (product.CostPrice <= 0)
                continue;
            options.Add(new SupplierOptionDto(
                supplier.Id,
                supplier.Name,
                "product-cost-fallback",
                "REAL",
                product.CostPrice,
                quantity,
                1,
                decimal.Round(product.CostPrice * quantity, 2),
                null,
                null,
                "No purchase history; using product cost price",
                "Not a supplier quotation. Cost price is an internal estimate."));
        }

        // SIMULATED / UNAVAILABLE research provider — never mixed as REAL.
        if (_research.IsAvailable)
        {
            var researched = await _research.ResearchAsync(_tenant.TenantId, product.Name, product.SKU, quantity, cancellationToken);
            foreach (var quote in researched)
            {
                var sanitizedName = NalvaGuard.Clip(NalvaGuard.Redact(quote.SupplierName), 120);
                var qty = Math.Max(quantity, quote.MinimumOrderQuantity);
                options.Add(new SupplierOptionDto(
                    null,
                    sanitizedName,
                    _research.ProviderKey,
                    quote.Classification,
                    quote.UnitPrice,
                    qty,
                    quote.MinimumOrderQuantity,
                    decimal.Round(quote.UnitPrice * qty, 2),
                    quote.EstimatedDeliveryDays,
                    quote.PaymentTerms,
                    quote.Availability,
                    NalvaGuard.Clip(quote.Notes, 500)));
            }
        }
        else
        {
            options.Add(new SupplierOptionDto(
                null,
                "External supplier research",
                _research.ProviderKey,
                _research.Classification,
                0,
                quantity,
                0,
                0,
                null,
                null,
                "unavailable",
                "No authorized supplier research integration is connected."));
        }

        return options
            .Where(x => x.Classification != "UNAVAILABLE" || x.UnitPrice == 0)
            .OrderBy(x => x.Classification == "REAL" ? 0 : 1)
            .ThenBy(x => x.TotalCost <= 0 ? decimal.MaxValue : x.TotalCost)
            .ToList();
    }

    public async Task<PreparedPurchaseOrderDto> PreparePurchaseOrderAsync(PreparePurchaseOrderRequest request, CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.PurchaseOrderCreate, PermissionCodes.NalvaManage);
        if (request.Quantity <= 0 || request.UnitPrice < 0)
            throw new BadRequestException("Quantity and unit price are required.");

        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException("Product was not found.");
        var supplier = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.SupplierId, cancellationToken)
            ?? throw new NotFoundException("Supplier was not found.");

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await _db.ProcurementRecommendations.AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return MapPrepared(existing, duplicate: true, requiresApproval: true, created: false,
                    "Duplicate prevented: an identical procurement recommendation already exists.");
            }

            var existingPo = await _db.Purchases.AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, cancellationToken);
            if (existingPo is not null)
            {
                return new PreparedPurchaseOrderDto(
                    Guid.Empty, existingPo.Id, existingPo.Status, existingPo.ApprovalFingerprint ?? "",
                    false, false, true,
                    "Duplicate prevented: a purchase order with this idempotency key already exists.",
                    request.ProductId, request.SupplierId, request.Quantity, request.UnitPrice,
                    existingPo.TotalAmount, existingPo.ExpectedDeliveryDate);
            }
        }

        var fingerprint = BuildFingerprint(
            request.SupplierId, request.ProductId, request.Quantity, request.UnitPrice,
            request.PaymentTerms, request.ExpectedDeliveryDate);

        var recommendation = new ProcurementRecommendation
        {
            ProductId = product.Id,
            SupplierId = supplier.Id,
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            TotalAmount = decimal.Round(request.Quantity * request.UnitPrice, 2),
            ExpectedDeliveryDate = request.ExpectedDeliveryDate,
            PaymentTerms = request.PaymentTerms,
            Reason = string.IsNullOrWhiteSpace(request.Reason)
                ? $"Prepared purchase for {product.Name} from {supplier.Name}."
                : NalvaGuard.Clip(request.Reason, 2000),
            Confidence = "medium",
            Status = "AwaitingApproval",
            ApprovalFingerprint = fingerprint,
            IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim(),
            Source = request.Source
        };
        _db.ProcurementRecommendations.Add(recommendation);

        // Draft PO stores the exact approved terms; remains Draft until approval submits Pending/Approved path.
        var purchase = new Purchase
        {
            Id = Guid.NewGuid(),
            SupplierId = supplier.Id,
            PurchaseDate = DateTime.UtcNow,
            TotalAmount = recommendation.TotalAmount,
            Status = PurchaseOrderStatusNames.Draft,
            ReferenceNumber = $"PROC-{DateTime.UtcNow:yyyyMMddHHmmss}",
            Notes = recommendation.Reason,
            ExpectedDeliveryDate = request.ExpectedDeliveryDate,
            IdempotencyKey = recommendation.IdempotencyKey,
            ApprovalFingerprint = fingerprint,
            PaymentTerms = request.PaymentTerms,
            SentToSupplier = false,
            PurchaseItems =
            [
                new PurchaseItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Quantity = request.Quantity,
                    UnitPrice = request.UnitPrice,
                    Total = recommendation.TotalAmount,
                    ReceivedQuantity = 0
                }
            ]
        };
        recommendation.Id = Guid.NewGuid();
        recommendation.PurchaseId = purchase.Id;
        _db.Purchases.Add(purchase);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogChangeAsync("ProcurementRecommendation", recommendation.Id, "Prepared", null,
            new { recommendation.ProductId, recommendation.SupplierId, recommendation.Quantity, recommendation.UnitPrice, fingerprint }, cancellationToken);

        _db.NalvaWorkItems.Add(new NalvaWorkItem
        {
            AgentKey = "procurement",
            Kind = "ProcurementRecommendation",
            Title = $"Reorder {product.Name}",
            Body = recommendation.Reason,
            Status = "AwaitingApproval",
            RelatedEntityType = "Purchase",
            RelatedEntityId = purchase.Id
        });
        await _db.SaveChangesAsync(cancellationToken);

        return MapPrepared(recommendation, duplicate: false, requiresApproval: true, created: true,
            $"Prepared draft purchase {purchase.ReferenceNumber} for approval. Fingerprint {fingerprint[..Math.Min(12, fingerprint.Length)]}… binds supplier, quantity, and price.");
    }

    public async Task<PreparedPurchaseOrderDto> SubmitPreparedPurchaseAsync(
        Guid recommendationId,
        string? idempotencyKey,
        bool approved,
        string? approvalFingerprint,
        CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.PurchaseOrderCreate, PermissionCodes.NalvaApprove, PermissionCodes.NalvaManage);
        if (!approved)
            throw new ForbiddenException("Purchase submission requires an explicit approval.");

        var recommendation = await _db.ProcurementRecommendations
            .FirstOrDefaultAsync(x => x.Id == recommendationId, cancellationToken)
            ?? throw new NotFoundException("Procurement recommendation was not found.");

        if (!string.IsNullOrWhiteSpace(approvalFingerprint)
            && !string.Equals(approvalFingerprint, recommendation.ApprovalFingerprint, StringComparison.Ordinal))
        {
            recommendation.Status = "Invalidated";
            await _db.SaveChangesAsync(cancellationToken);
            throw new BadRequestException("Approval fingerprint does not match. Material terms changed; previous approval is invalid.");
        }

        if (recommendation.PurchaseId is null)
            throw new BadRequestException("Recommendation has no draft purchase order.");

        var purchase = await _db.Purchases.Include(x => x.PurchaseItems)
            .FirstOrDefaultAsync(x => x.Id == recommendation.PurchaseId, cancellationToken)
            ?? throw new NotFoundException("Draft purchase order was not found.");

        var currentFingerprint = BuildFingerprint(
            purchase.SupplierId,
            purchase.PurchaseItems.First().ProductId,
            purchase.PurchaseItems.First().Quantity,
            purchase.PurchaseItems.First().UnitPrice,
            purchase.PaymentTerms,
            purchase.ExpectedDeliveryDate);

        if (!string.Equals(currentFingerprint, recommendation.ApprovalFingerprint, StringComparison.Ordinal))
        {
            recommendation.Status = "Invalidated";
            await _db.SaveChangesAsync(cancellationToken);
            throw new BadRequestException("Purchase terms changed after preparation. Approval is invalidated.");
        }

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var other = await _db.Purchases.AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey && x.Id != purchase.Id, cancellationToken);
            if (other is not null)
                return MapPrepared(recommendation, true, false, false, "Duplicate prevented by idempotency key.");
        }

        if (purchase.Status is PurchaseOrderStatusNames.Pending or PurchaseOrderStatusNames.Approved)
        {
            return MapPrepared(recommendation, true, false, false, "Purchase was already submitted after approval.");
        }

        purchase.Status = PurchaseOrderStatusNames.Pending;
        purchase.Notes = (purchase.Notes ?? "") + " | Approved and submitted internally. Not transmitted to an external supplier.";
        purchase.SentToSupplier = false;
        recommendation.Status = "Submitted";
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogChangeAsync("Purchase", purchase.Id, "ProcurementSubmitted", null,
            new { PurchaseId = purchase.Id, RecommendationId = recommendation.Id, purchase.ApprovalFingerprint, purchase.SentToSupplier }, cancellationToken);

        return MapPrepared(recommendation, false, false, true,
            "Pending purchase created after approval. External supplier transmission is UNAVAILABLE; SentToSupplier=false.");
    }

    public async Task<IReadOnlyList<DeliveryTrackingDto>> GetDeliveriesAsync(CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.PurchaseOrderView, PermissionCodes.NalvaView);
        var rows = await _db.Purchases.AsNoTracking()
            .Where(x => x.Status == PurchaseOrderStatusNames.Approved
                        || x.Status == PurchaseOrderStatusNames.PartiallyReceived
                        || x.Status == PurchaseOrderStatusNames.Pending)
            .OrderBy(x => x.ExpectedDeliveryDate ?? x.PurchaseDate)
            .Take(100)
            .Select(x => new
            {
                x.Id,
                x.ReferenceNumber,
                x.SupplierId,
                SupplierName = x.Supplier.Name,
                x.Status,
                x.PurchaseDate,
                x.ExpectedDeliveryDate,
                Ordered = x.PurchaseItems.Where(i => !i.IsDeleted).Sum(i => i.Quantity),
                Received = x.PurchaseItems.Where(i => !i.IsDeleted).Sum(i => i.ReceivedQuantity)
            })
            .ToListAsync(cancellationToken);

        var today = DateTime.UtcNow.Date;
        return rows.Select(x =>
        {
            var expected = x.ExpectedDeliveryDate?.Date;
            var delayed = expected is not null
                          && expected < today
                          && x.Status != PurchaseOrderStatusNames.Received
                          && x.Received < x.Ordered;
            var daysLate = delayed && expected is not null ? (int)(today - expected.Value).TotalDays : 0;
            return new DeliveryTrackingDto(
                x.Id,
                x.ReferenceNumber ?? x.Id.ToString("N")[..8],
                x.SupplierId,
                x.SupplierName,
                x.Status,
                x.PurchaseDate,
                x.ExpectedDeliveryDate,
                delayed,
                daysLate,
                x.Ordered,
                x.Received,
                Math.Max(0, x.Ordered - x.Received));
        }).ToList();
    }

    public async Task<ReceiveVerificationResultDto> ReceiveVerifiedAsync(ReceiveVerificationRequest request, CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.PurchaseOrderUpdate, PermissionCodes.InventoryUpdate);
        if (request.Lines.Count == 0)
            throw new BadRequestException("At least one verified receiving line is required. Supplier claims alone cannot increase stock.");

        var purchase = await _db.Purchases.Include(x => x.PurchaseItems)
            .FirstOrDefaultAsync(x => x.Id == request.PurchaseOrderId, cancellationToken)
            ?? throw new NotFoundException("Purchase order was not found.");

        if (!PurchaseOrderStatusRules.CanReceive(purchase.Status))
            throw new BadRequestException($"Purchase order in '{purchase.Status}' status cannot be received.");

        var discrepancies = new List<string>();
        decimal stockIncreased = 0;
        var transaction = await _db.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var line in request.Lines)
            {
                var item = purchase.PurchaseItems.FirstOrDefault(x => x.Id == line.PurchaseItemId && !x.IsDeleted)
                    ?? throw new BadRequestException($"Purchase line {line.PurchaseItemId} was not found on this order.");

                if (line.ReceivedQuantity < 0 || line.DamagedQuantity < 0)
                    throw new BadRequestException("Received and damaged quantities cannot be negative.");

                var remaining = item.Quantity - item.ReceivedQuantity;
                if (line.ReceivedQuantity > remaining)
                    throw new BadRequestException($"Cannot receive {line.ReceivedQuantity} for product line; only {remaining} remaining.");

                // Damaged units are recorded but do not increase sellable stock.
                var goodQty = line.ReceivedQuantity;
                if (goodQty > 0)
                {
                    await _inventory.IncreaseStockAsync(new StockChangeRequest
                    {
                        ProductId = item.ProductId,
                        Quantity = goodQty,
                        ReferenceNumber = purchase.ReferenceNumber ?? purchase.Id.ToString(),
                        Notes = $"Verified receive for PO {purchase.Id}"
                    }, cancellationToken);
                    stockIncreased += goodQty;
                }

                item.ReceivedQuantity += line.ReceivedQuantity;
                item.DamagedQuantity += line.DamagedQuantity;
                var shortfall = Math.Max(0, item.Quantity - item.ReceivedQuantity);
                item.ShortageQuantity = shortfall;
                if (!string.IsNullOrWhiteSpace(line.DiscrepancyNotes))
                    item.ReceivingNotes = NalvaGuard.Clip(line.DiscrepancyNotes, 1000);

                if (line.DamagedQuantity > 0)
                    discrepancies.Add($"Damaged {line.DamagedQuantity:0.##} on line {item.Id}.");
                if (shortfall > 0)
                    discrepancies.Add($"Shortage remaining {shortfall:0.##} on line {item.Id}.");
            }

            var fullyReceived = purchase.PurchaseItems.Where(x => !x.IsDeleted).All(x => x.ReceivedQuantity >= x.Quantity);
            var newStatus = fullyReceived ? PurchaseOrderStatusNames.Received : PurchaseOrderStatusNames.PartiallyReceived;
            if (!string.Equals(purchase.Status, newStatus, StringComparison.OrdinalIgnoreCase))
            {
                var ok = await _db.TryTransitionPurchaseStatusAsync(purchase.Id, purchase.Status, newStatus, cancellationToken);
                if (!ok && !fullyReceived)
                    throw new ConflictException("Purchase order status changed concurrently.", ErrorCodes.ConcurrencyConflict);
                purchase.Status = newStatus;
            }

            var evt = new PurchaseReceivingEvent
            {
                PurchaseId = purchase.Id,
                ReceivedByUserId = _user.UserId ?? "system",
                Notes = NalvaGuard.Clip(request.Notes, 2000),
                PayloadJson = NalvaGuard.Clip(JsonSerializer.Serialize(request.Lines), 8000),
                StockIncreasedBy = stockIncreased,
                ResultStatus = purchase.Status
            };
            _db.PurchaseReceivingEvents.Add(evt);
            await _db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);

            await _audit.LogChangeAsync("Purchase", purchase.Id, "ReceivedVerified", null,
                new { PurchaseId = purchase.Id, stockIncreased, purchase.Status, discrepancies }, cancellationToken);

            return new ReceiveVerificationResultDto(
                purchase.Id,
                purchase.Status,
                stockIncreased,
                discrepancies,
                fullyReceived
                    ? $"Fully received. Stock increased by {stockIncreased:0.##}."
                    : $"Partially received. Stock increased by {stockIncreased:0.##}.");
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    public async Task<ProcurementAutomationSettingsDto> GetAutomationSettingsAsync(CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.NalvaView, PermissionCodes.NalvaManage);
        var row = await EnsureSettingsAsync(cancellationToken);
        return MapSettings(row);
    }

    public async Task<ProcurementAutomationSettingsDto> SaveAutomationSettingsAsync(
        ProcurementAutomationSettingsWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        var row = await EnsureSettingsAsync(cancellationToken);
        if (request.DailyInventoryMonitoring is bool v1) row.DailyInventoryMonitoring = v1;
        if (request.LowStockDetection is bool v2) row.LowStockDetection = v2;
        if (request.DemandAnalysis is bool v3) row.DemandAnalysis = v3;
        if (request.ProcurementRecommendations is bool v4) row.ProcurementRecommendations = v4;
        if (request.PurchaseOrderStatusMonitoring is bool v5) row.PurchaseOrderStatusMonitoring = v5;
        if (request.DeliveryFollowUps is bool v6) row.DeliveryFollowUps = v6;
        if (request.InventoryDiscrepancyAlerts is bool v7) row.InventoryDiscrepancyAlerts = v7;
        if (request.SlowMovingDays is int days && days is >= 7 and <= 365) row.SlowMovingDays = days;
        if (request.OverstockMultiplier is decimal mult && mult is >= 1 and <= 20) row.OverstockMultiplier = mult;
        if (request.MinimumHistoryDays is int hist && hist is >= 1 and <= 90) row.MinimumHistoryDays = hist;
        if (!string.IsNullOrWhiteSpace(request.PreferredSupplierCriterion))
        {
            var criterion = request.PreferredSupplierCriterion.Trim().ToLowerInvariant();
            if (criterion is not ("lowest-total" or "fastest-delivery" or "preferred-history"))
                throw new BadRequestException("Preferred supplier criterion is invalid.");
            row.PreferredSupplierCriterion = criterion;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogChangeAsync("ProcurementAgentSettings", row.Id, "Updated", null, request, cancellationToken);
        return MapSettings(row);
    }

    public async Task<IReadOnlyList<ProcurementActivityDto>> GetActivityAsync(CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.NalvaView, PermissionCodes.PurchaseOrderView);
        var work = await _db.NalvaWorkItems.AsNoTracking()
            .Where(x => x.AgentKey == "procurement")
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .Select(x => new ProcurementActivityDto(
                x.Id, x.Kind, x.Title, x.Body, x.Status, x.CreatedAt, x.RelatedEntityId, null))
            .ToListAsync(cancellationToken);

        var recommendations = await _db.ProcurementRecommendations.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .Select(x => new ProcurementActivityDto(
                x.Id, "Recommendation", "Procurement recommendation", x.Reason, x.Status, x.CreatedAt, x.PurchaseId, x.ProductId))
            .ToListAsync(cancellationToken);

        return work.Concat(recommendations).OrderByDescending(x => x.CreatedAt).Take(80).ToList();
    }

    public async Task RunScheduledMonitorAsync(CancellationToken cancellationToken = default)
    {
        // Hosted processors may run without an interactive user; still require tenant context.
        if (!_tenant.HasTenant())
            return;

        try
        {
            var settings = await EnsureSettingsAsync(cancellationToken);
            if (!settings.DailyInventoryMonitoring && !settings.DeliveryFollowUps)
                return;

            if (settings.LowStockDetection || settings.DailyInventoryMonitoring)
            {
                var risks = await GetInventoryRisksAsync(cancellationToken);
                var critical = risks.Where(x => x.RiskKind is "low-stock" or "out-of-stock" or "discrepancy").Take(20).ToList();
                foreach (var risk in critical)
                {
                    _db.NalvaWorkItems.Add(new NalvaWorkItem
                    {
                        AgentKey = "procurement",
                        Kind = "InventoryAlert",
                        Title = $"{risk.RiskKind}: {risk.ProductName}",
                        Body = risk.Explanation,
                        Status = "Open",
                        RelatedEntityType = "Product",
                        RelatedEntityId = risk.ProductId
                    });
                }
            }

            if (settings.DeliveryFollowUps)
            {
                var deliveries = await GetDeliveriesAsync(cancellationToken);
                foreach (var delayed in deliveries.Where(x => x.IsDelayed).Take(20))
                {
                    var purchase = await _db.Purchases.FirstOrDefaultAsync(x => x.Id == delayed.PurchaseOrderId, cancellationToken);
                    if (purchase is null)
                        continue;
                    if (purchase.DelayedNotifiedAt is DateTime notified && notified.Date == DateTime.UtcNow.Date)
                        continue;

                    purchase.DelayedNotifiedAt = DateTime.UtcNow;
                    _db.NalvaWorkItems.Add(new NalvaWorkItem
                    {
                        AgentKey = "procurement",
                        Kind = "DeliveryDelay",
                        Title = $"Delayed delivery {delayed.ReferenceNumber}",
                        Body = $"Expected {delayed.ExpectedDeliveryDate:yyyy-MM-dd}; {delayed.DaysLate} day(s) late. Remaining {delayed.RemainingQuantity:0.##}.",
                        Status = "Open",
                        RelatedEntityType = "Purchase",
                        RelatedEntityId = delayed.PurchaseOrderId
                    });
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Procurement scheduled monitor completed for tenant {TenantId}", _tenant.TenantId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Procurement agent monitor failed for tenant {TenantId}", _tenant.TenantId);
            throw;
        }
    }

    private async Task<Dictionary<Guid, decimal>> LoadIncomingByProductAsync(CancellationToken ct)
    {
        var rows = await _db.PurchaseItems.AsNoTracking()
            .Where(x => !x.IsDeleted
                        && OpenPurchaseStatuses.Contains(x.Purchase.Status)
                        && x.Purchase.Status != PurchaseOrderStatusNames.Draft)
            .Select(x => new { x.ProductId, Remaining = x.Quantity - x.ReceivedQuantity })
            .ToListAsync(ct);

        return rows.GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => Math.Max(0, x.Remaining)));
    }

    private async Task<Dictionary<Guid, decimal>> LoadSalesUnitsAsync(int days, CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        var rows = await _db.OrderItems.AsNoTracking()
            .Where(x => !x.IsDeleted
                        && x.Order.OrderDate >= since
                        && x.Order.Status != OrderStatusNames.Cancelled)
            .GroupBy(x => x.ProductId)
            .Select(g => new { g.Key, Units = g.Sum(x => x.Quantity) })
            .ToListAsync(ct);
        return rows.ToDictionary(x => x.Key, x => x.Units);
    }

    private async Task<int> CountDistinctOrderDaysAsync(Guid productId, int days, CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        var dates = await _db.OrderItems.AsNoTracking()
            .Where(x => !x.IsDeleted
                        && x.ProductId == productId
                        && x.Order.OrderDate >= since
                        && x.Order.Status != OrderStatusNames.Cancelled)
            .Select(x => x.Order.OrderDate.Date)
            .Distinct()
            .CountAsync(ct);
        return dates;
    }

    private async Task<HashSet<Guid>> LoadUnusualStockDeltasAsync(CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-14);
        var rows = await _db.StockTransactions.AsNoTracking()
            .Where(x => x.CreatedAt >= since)
            .Select(x => new { x.ProductId, Delta = x.NewStock - x.PreviousStock })
            .ToListAsync(ct);

        var unusual = new HashSet<Guid>();
        foreach (var group in rows.GroupBy(x => x.ProductId))
        {
            var abs = group.Select(x => Math.Abs(x.Delta)).OrderBy(x => x).ToList();
            if (abs.Count < 3)
                continue;
            var median = abs[abs.Count / 2];
            if (median <= 0)
                continue;
            if (abs.Any(x => x >= median * 10 && x >= 20))
                unusual.Add(group.Key);
        }
        return unusual;
    }

    private static decimal CalculateReorder(
        decimal current,
        decimal reorderLevel,
        decimal maximum,
        decimal? dailyDemand,
        decimal inbound,
        int? leadTimeDays = null)
    {
        var coverDays = leadTimeDays is > 0 ? leadTimeDays.Value + 7 : 14;
        var target = maximum > 0
            ? maximum
            : Math.Max(reorderLevel * 2m, reorderLevel + (dailyDemand is > 0 ? dailyDemand.Value * coverDays : 0m));
        var need = target - (current + inbound);
        if (need <= 0 && current <= reorderLevel)
            need = Math.Max(reorderLevel - current - inbound, 1);
        return decimal.Round(Math.Max(need, 0), 2);
    }

    private async Task<int?> EstimateLeadTimeDaysAsync(Guid productId, CancellationToken ct)
    {
        var events = await _db.PurchaseReceivingEvents.AsNoTracking()
            .Where(x => x.Purchase != null && x.Purchase.PurchaseItems.Any(i => i.ProductId == productId && !i.IsDeleted))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.CreatedAt, PurchaseDate = x.Purchase!.PurchaseDate })
            .Take(10)
            .ToListAsync(ct);
        if (events.Count == 0)
            return null;
        var avg = events.Average(x => Math.Max(0, (x.CreatedAt.Date - x.PurchaseDate.Date).TotalDays));
        return (int)Math.Round(avg);
    }

    private static SupplierOptionDto? PickPreferred(IReadOnlyList<SupplierOptionDto> options, string criterion)
    {
        var real = options.Where(x => x.Classification == "REAL" && x.SupplierId is not null && x.UnitPrice > 0).ToList();
        if (real.Count == 0)
            return null;

        return criterion switch
        {
            "fastest-delivery" => real.OrderBy(x => x.EstimatedDeliveryDays ?? int.MaxValue).ThenBy(x => x.TotalCost).First(),
            "preferred-history" => real.FirstOrDefault(x => x.Source == "stored-purchase-history") ?? real.OrderBy(x => x.TotalCost).First(),
            _ => real.OrderBy(x => x.TotalCost).First()
        };
    }

    public static string BuildFingerprint(
        Guid? supplierId,
        Guid productId,
        decimal quantity,
        decimal unitPrice,
        string? paymentTerms,
        DateTime? expectedDelivery)
    {
        var raw = string.Join('|',
            supplierId?.ToString("N") ?? "none",
            productId.ToString("N"),
            quantity.ToString("0.####", CultureInfo.InvariantCulture),
            unitPrice.ToString("0.####", CultureInfo.InvariantCulture),
            paymentTerms?.Trim().ToLowerInvariant() ?? "",
            expectedDelivery?.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    private async Task<ProcurementAgentSettings> EnsureSettingsAsync(CancellationToken ct)
    {
        var row = await _db.ProcurementAgentSettings.FirstOrDefaultAsync(ct);
        if (row is not null)
            return row;
        row = new ProcurementAgentSettings();
        _db.ProcurementAgentSettings.Add(row);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    private static ProcurementAutomationSettingsDto MapSettings(ProcurementAgentSettings row) =>
        new(row.DailyInventoryMonitoring, row.LowStockDetection, row.DemandAnalysis, row.ProcurementRecommendations,
            row.PurchaseOrderStatusMonitoring, row.DeliveryFollowUps, row.InventoryDiscrepancyAlerts,
            row.SlowMovingDays, row.OverstockMultiplier, row.MinimumHistoryDays, row.PreferredSupplierCriterion,
            row.UpdatedAt);

    private static PreparedPurchaseOrderDto MapPrepared(
        ProcurementRecommendation recommendation,
        bool duplicate,
        bool requiresApproval,
        bool created,
        string summary) =>
        new(recommendation.Id, recommendation.PurchaseId, recommendation.Status, recommendation.ApprovalFingerprint,
            requiresApproval, created, duplicate, summary, recommendation.ProductId,
            recommendation.SupplierId ?? Guid.Empty, recommendation.Quantity, recommendation.UnitPrice,
            recommendation.TotalAmount, recommendation.ExpectedDeliveryDate);

    private void Require(string permission)
    {
        // Background hosted jobs are scoped by tenant, not an interactive HTTP user.
        if (string.IsNullOrWhiteSpace(_user.UserId))
            return;
        if (!_user.HasPermission(permission))
            throw new ForbiddenException($"Missing permission {permission}.");
    }

    private void RequireAny(params string[] permissions)
    {
        // Background hosted jobs are scoped by tenant, not an interactive HTTP user.
        if (string.IsNullOrWhiteSpace(_user.UserId))
            return;
        if (permissions.Any(_user.HasPermission))
            return;
        throw new ForbiddenException($"Missing permission {permissions[0]}.");
    }
}
