using System.Text.Json;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Application.Features.DataImports.Models;
using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Application.Features.Inventory.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.DataImports.Services;

public sealed class DataImportCommitService
{
    private readonly IApplicationDbContext _context;
    private readonly IInventoryService _inventoryService;
    private readonly ISubscriptionEntitlementService _entitlements;
    private readonly ITenantProvider _tenantProvider;

    public DataImportCommitService(
        IApplicationDbContext context,
        IInventoryService inventoryService,
        ISubscriptionEntitlementService entitlements,
        ITenantProvider tenantProvider)
    {
        _context = context;
        _inventoryService = inventoryService;
        _entitlements = entitlements;
        _tenantProvider = tenantProvider;
    }

    public async Task<DataImportCommitResponse> CommitAsync(
        DataImportPreviewSession session,
        IReadOnlyDictionary<string, string?> mapping,
        string? userId,
        bool importValidRowsOnly,
        CancellationToken cancellationToken)
    {
        // Idempotent replay of identical completed file
        var prior = await _context.DataImportBatches
            .AsNoTracking()
            .Where(x => x.ContentHash == session.ContentHash
                        && x.EntityType == session.EntityType
                        && (x.Status == "Completed" || x.Status == "IdempotentReplay"))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (prior is not null)
        {
            var replay = new DataImportBatch
            {
                Id = Guid.NewGuid(),
                EntityType = session.EntityType,
                FileName = session.FileName,
                Status = "IdempotentReplay",
                TotalRows = prior.TotalRows,
                CreatedCount = 0,
                UpdatedCount = 0,
                SkippedCount = prior.TotalRows,
                ErrorCount = 0,
                ImportedByUserId = userId,
                ContentHash = session.ContentHash,
                ErrorSummaryJson = $"Identical file already imported as batch {prior.Id}."
            };
            _context.DataImportBatches.Add(replay);
            await _context.SaveChangesAsync(cancellationToken);

            return new DataImportCommitResponse(
                replay.Id,
                session.EntityType,
                session.FileName,
                replay.Status,
                prior.TotalRows,
                0,
                0,
                prior.TotalRows,
                0,
                0,
                true,
                [$"No changes applied. Identical file was already imported (batch {prior.Id})."]);
        }

        var failures = ImportPreviewBuilder.CollectFailures(
            new DataImportPreviewSession
            {
                PreviewId = session.PreviewId,
                TenantId = session.TenantId,
                EntityType = session.EntityType,
                FileName = session.FileName,
                Format = session.Format,
                ContentHash = session.ContentHash,
                CsvColumns = session.CsvColumns,
                ColumnMapping = mapping,
                RawRows = session.RawRows
            });

        if (failures.Count > 0 && !importValidRowsOnly)
        {
            throw new BadRequestException(
                $"{failures.Count} row(s) have validation errors. " +
                "Fix the file, or commit with importValidRowsOnly=true after downloading the failed-row report from preview. " +
                "Invalid rows are never discarded silently.");
        }

        // Plan capacity gate for creates
        var createCount = await EstimateCreateCountAsync(session, mapping, cancellationToken);
        var capacity = await ImportPlanCapacityEvaluator.EvaluateAsync(
            session.EntityType,
            createCount,
            _entitlements,
            _tenantProvider.TenantId,
            cancellationToken);

        if (capacity is { WithinLimit: false })
        {
            throw new PlanLimitExceededException(
                capacity.Resource,
                capacity.CurrentUsage,
                capacity.Maximum < 0 ? -1 : capacity.Maximum,
                capacity.UpgradeHint);
        }

        var errors = new List<string>();
        var created = 0;
        var updated = 0;
        var skipped = 0;
        var committedFailures = new List<ImportRowFailure>();

        if (session.EntityType is DataImportEntityTypes.Sales or DataImportEntityTypes.Purchases)
        {
            var result = session.EntityType == DataImportEntityTypes.Sales
                ? await CommitSalesAsync(session, mapping, importValidRowsOnly, failures, cancellationToken)
                : await CommitPurchasesAsync(session, mapping, importValidRowsOnly, failures, cancellationToken);
            created = result.Created;
            updated = result.Updated;
            skipped = result.Skipped;
            errors.AddRange(result.Errors);
            committedFailures.AddRange(result.Failures);
        }
        else
        {
            var failureKeys = failures
                .Select(f => f.RowNumber)
                .ToHashSet();

            for (var index = 0; index < session.RawRows.Count; index++)
            {
                var rowNumber = index + 2;
                var row = session.RawRows[index];

                if (failureKeys.Contains(rowNumber))
                {
                    skipped++;
                    var failure = failures.First(f => f.RowNumber == rowNumber);
                    committedFailures.Add(failure);
                    errors.AddRange(failure.Errors.Select(e => $"Row {rowNumber}: {e}"));
                    continue;
                }

                var naturalKey = ImportPreviewBuilder.ResolveNaturalKey(session.EntityType, row, mapping);

                try
                {
                    var rowResult = session.EntityType switch
                    {
                        DataImportEntityTypes.Categories =>
                            await UpsertCategoryAsync(row, mapping, naturalKey, cancellationToken),
                        DataImportEntityTypes.Products =>
                            await UpsertProductAsync(row, mapping, naturalKey, cancellationToken),
                        DataImportEntityTypes.Customers =>
                            await UpsertCustomerAsync(row, mapping, naturalKey, cancellationToken),
                        DataImportEntityTypes.Suppliers =>
                            await UpsertSupplierAsync(row, mapping, naturalKey, cancellationToken),
                        DataImportEntityTypes.Expenses =>
                            await UpsertExpenseAsync(row, mapping, naturalKey, cancellationToken),
                        DataImportEntityTypes.CustomerPayments =>
                            await UpsertCustomerPaymentAsync(row, mapping, naturalKey, cancellationToken),
                        DataImportEntityTypes.SupplierPayments =>
                            await UpsertSupplierPaymentAsync(row, mapping, naturalKey, cancellationToken),
                        DataImportEntityTypes.InventoryAdjustments =>
                            await UpsertInventoryAdjustmentAsync(row, mapping, naturalKey, cancellationToken),
                        _ => ImportRowResult.Skip
                    };

                    switch (rowResult)
                    {
                        case ImportRowResult.Created:
                            created++;
                            break;
                        case ImportRowResult.Updated:
                            updated++;
                            break;
                        default:
                            skipped++;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    skipped++;
                    var msg = $"Failed '{naturalKey}': {ex.Message}";
                    errors.Add(msg);
                    committedFailures.Add(new ImportRowFailure(
                        rowNumber,
                        naturalKey,
                        [ex.Message],
                        ImportPreviewBuilder.ValidateRow(session.EntityType, row, mapping).Count == 0
                            ? row.ToDictionary(k => k.Key, v => v.Value)
                            : row.ToDictionary(k => k.Key, v => v.Value)));
                }
            }
        }

        var status = errors.Count == 0
            ? "Completed"
            : created + updated > 0 ? "Partial" : "Failed";

        var batch = new DataImportBatch
        {
            Id = Guid.NewGuid(),
            EntityType = session.EntityType,
            FileName = session.FileName,
            Status = status,
            TotalRows = session.RawRows.Count,
            CreatedCount = created,
            UpdatedCount = updated,
            SkippedCount = skipped,
            ErrorCount = errors.Count,
            ImportedByUserId = userId,
            ContentHash = session.ContentHash,
            ErrorSummaryJson = errors.Count == 0
                ? null
                : string.Join(" | ", errors.Take(40)),
            FailedRowsJson = committedFailures.Count == 0
                ? null
                : JsonSerializer.Serialize(committedFailures)
        };

        _context.DataImportBatches.Add(batch);
        await _context.SaveChangesAsync(cancellationToken);

        return new DataImportCommitResponse(
            batch.Id,
            session.EntityType,
            session.FileName,
            batch.Status,
            batch.TotalRows,
            created,
            updated,
            skipped,
            errors.Count,
            committedFailures.Count,
            false,
            errors.Take(50).ToList());
    }

    private async Task<int> EstimateCreateCountAsync(
        DataImportPreviewSession session,
        IReadOnlyDictionary<string, string?> mapping,
        CancellationToken ct)
    {
        if (!DataImportEntityTypes.IsQuotaCountedCreate(session.EntityType))
            return 0;

        var preview = await ImportPreviewBuilder.BuildAsync(
            new DataImportPreviewSession
            {
                PreviewId = session.PreviewId,
                TenantId = session.TenantId,
                EntityType = session.EntityType,
                FileName = session.FileName,
                Format = session.Format,
                ContentHash = session.ContentHash,
                CsvColumns = session.CsvColumns,
                ColumnMapping = mapping,
                RawRows = session.RawRows
            },
            _context,
            ct);

        return preview.CreateCount;
    }

    private async Task<(int Created, int Updated, int Skipped, List<string> Errors, List<ImportRowFailure> Failures)>
        CommitSalesAsync(
            DataImportPreviewSession session,
            IReadOnlyDictionary<string, string?> mapping,
            bool importValidRowsOnly,
            IReadOnlyList<ImportRowFailure> failures,
            CancellationToken ct)
    {
        var errors = new List<string>();
        var committedFailures = new List<ImportRowFailure>();
        var failureRows = failures.Select(f => f.RowNumber).ToHashSet();
        var created = 0;
        var updated = 0;
        var skipped = 0;

        var groups = new Dictionary<string, List<(int RowNumber, IReadOnlyDictionary<string, string> Row)>>(
            StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < session.RawRows.Count; i++)
        {
            var rowNumber = i + 2;
            if (failureRows.Contains(rowNumber))
            {
                skipped++;
                var f = failures.First(x => x.RowNumber == rowNumber);
                committedFailures.Add(f);
                errors.AddRange(f.Errors.Select(e => $"Row {rowNumber}: {e}"));
                continue;
            }

            var importKey = ImportColumnMapper.GetMappedValue(session.RawRows[i], mapping, "ImportKey").Trim();
            if (!groups.TryGetValue(importKey, out var list))
            {
                list = [];
                groups[importKey] = list;
            }

            list.Add((rowNumber, session.RawRows[i]));
        }

        foreach (var (importKey, lines) in groups)
        {
            try
            {
                var first = lines[0].Row;
                var orderNumber = ImportColumnMapper.GetMappedValue(first, mapping, "OrderNumber").Trim();
                var orderDate = DateTime.Parse(ImportColumnMapper.GetMappedValue(first, mapping, "OrderDate"));
                var customerEmail = ImportPreviewBuilder.NormalizeEmail(
                    ImportColumnMapper.GetMappedValue(first, mapping, "CustomerEmail"));
                var paymentStatus = ImportColumnMapper.GetMappedValue(first, mapping, "PaymentStatus");
                if (string.IsNullOrWhiteSpace(paymentStatus))
                    paymentStatus = "Unpaid";

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.Email.ToLower() == customerEmail, ct)
                    ?? throw new InvalidOperationException($"Customer '{customerEmail}' was not found. Import customers first.");

                var existing = await _context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.ExternalImportKey == importKey, ct);

                if (existing is not null)
                {
                    // Historical re-import: update header metadata only; do not touch stock or payments
                    existing.OrderNumber = orderNumber;
                    existing.OrderDate = DateTime.SpecifyKind(orderDate, DateTimeKind.Utc);
                    existing.PaymentStatus = paymentStatus;
                    updated++;
                    continue;
                }

                decimal subtotal = 0;
                var orderItems = new List<OrderItem>();
                foreach (var (_, row) in lines)
                {
                    var sku = ImportColumnMapper.GetMappedValue(row, mapping, "ProductSKU").Trim();
                    var qty = ParseDecimal(ImportColumnMapper.GetMappedValue(row, mapping, "Quantity"), 0);
                    var unitPrice = ParseDecimal(ImportColumnMapper.GetMappedValue(row, mapping, "UnitPrice"), 0);
                    if (qty <= 0)
                        throw new InvalidOperationException($"Quantity must be positive for SKU '{sku}'.");

                    var product = await _context.Products.FirstOrDefaultAsync(p => p.SKU == sku, ct)
                        ?? throw new InvalidOperationException($"Product SKU '{sku}' was not found.");

                    var lineTotal = qty * unitPrice;
                    subtotal += lineTotal;
                    orderItems.Add(new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        ProductId = product.Id,
                        Quantity = qty,
                        UnitPrice = unitPrice,
                        Total = lineTotal,
                        UnitCost = product.CostPrice
                    });
                }

                var discount = ParseDecimal(ImportColumnMapper.GetMappedValue(first, mapping, "Discount"), 0);
                var tax = ParseDecimal(ImportColumnMapper.GetMappedValue(first, mapping, "Tax"), 0);
                var grand = subtotal - discount + tax;

                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer.Id,
                    OrderNumber = orderNumber,
                    OrderDate = DateTime.SpecifyKind(orderDate, DateTimeKind.Utc),
                    Status = "Completed",
                    TotalAmount = subtotal,
                    Discount = discount,
                    Tax = tax,
                    GrandTotal = grand,
                    PaymentStatus = paymentStatus,
                    ExternalImportKey = importKey,
                    OrderItems = orderItems
                };

                _context.Orders.Add(order);
                created++;
            }
            catch (Exception ex)
            {
                skipped += lines.Count;
                errors.Add($"Sale '{importKey}': {ex.Message}");
                foreach (var (rowNumber, row) in lines)
                {
                    committedFailures.Add(new ImportRowFailure(
                        rowNumber, importKey, [ex.Message],
                        row.ToDictionary(k => k.Key, v => v.Value)));
                }
            }
        }

        await _context.SaveChangesAsync(ct);
        return (created, updated, skipped, errors, committedFailures);
    }

    private async Task<(int Created, int Updated, int Skipped, List<string> Errors, List<ImportRowFailure> Failures)>
        CommitPurchasesAsync(
            DataImportPreviewSession session,
            IReadOnlyDictionary<string, string?> mapping,
            bool importValidRowsOnly,
            IReadOnlyList<ImportRowFailure> failures,
            CancellationToken ct)
    {
        var errors = new List<string>();
        var committedFailures = new List<ImportRowFailure>();
        var failureRows = failures.Select(f => f.RowNumber).ToHashSet();
        var created = 0;
        var updated = 0;
        var skipped = 0;

        var groups = new Dictionary<string, List<(int RowNumber, IReadOnlyDictionary<string, string> Row)>>(
            StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < session.RawRows.Count; i++)
        {
            var rowNumber = i + 2;
            if (failureRows.Contains(rowNumber))
            {
                skipped++;
                var f = failures.First(x => x.RowNumber == rowNumber);
                committedFailures.Add(f);
                errors.AddRange(f.Errors.Select(e => $"Row {rowNumber}: {e}"));
                continue;
            }

            var importKey = ImportColumnMapper.GetMappedValue(session.RawRows[i], mapping, "ImportKey").Trim();
            if (!groups.TryGetValue(importKey, out var list))
            {
                list = [];
                groups[importKey] = list;
            }

            list.Add((rowNumber, session.RawRows[i]));
        }

        foreach (var (importKey, lines) in groups)
        {
            try
            {
                var first = lines[0].Row;
                var reference = ImportColumnMapper.GetMappedValue(first, mapping, "ReferenceNumber").Trim();
                var purchaseDate = DateTime.Parse(ImportColumnMapper.GetMappedValue(first, mapping, "PurchaseDate"));
                var supplierEmail = ImportPreviewBuilder.NormalizeEmail(
                    ImportColumnMapper.GetMappedValue(first, mapping, "SupplierEmail"));
                var status = ImportColumnMapper.GetMappedValue(first, mapping, "Status");
                if (string.IsNullOrWhiteSpace(status))
                    status = "Received";

                var supplier = await _context.Suppliers
                    .FirstOrDefaultAsync(s => s.Email.ToLower() == supplierEmail, ct)
                    ?? throw new InvalidOperationException($"Supplier '{supplierEmail}' was not found. Import suppliers first.");

                var existing = await _context.Purchases
                    .FirstOrDefaultAsync(p => p.IdempotencyKey == importKey, ct);

                if (existing is not null)
                {
                    existing.ReferenceNumber = reference;
                    existing.PurchaseDate = DateTime.SpecifyKind(purchaseDate, DateTimeKind.Utc);
                    existing.Status = status;
                    updated++;
                    continue;
                }

                decimal total = 0;
                var items = new List<PurchaseItem>();
                foreach (var (_, row) in lines)
                {
                    var sku = ImportColumnMapper.GetMappedValue(row, mapping, "ProductSKU").Trim();
                    var qty = ParseDecimal(ImportColumnMapper.GetMappedValue(row, mapping, "Quantity"), 0);
                    var unitPrice = ParseDecimal(ImportColumnMapper.GetMappedValue(row, mapping, "UnitPrice"), 0);
                    var product = await _context.Products.FirstOrDefaultAsync(p => p.SKU == sku, ct)
                        ?? throw new InvalidOperationException($"Product SKU '{sku}' was not found.");

                    var lineTotal = qty * unitPrice;
                    total += lineTotal;
                    items.Add(new PurchaseItem
                    {
                        Id = Guid.NewGuid(),
                        ProductId = product.Id,
                        Quantity = qty,
                        UnitPrice = unitPrice,
                        Total = lineTotal,
                        ReceivedQuantity = qty
                    });
                }

                _context.Purchases.Add(new Purchase
                {
                    Id = Guid.NewGuid(),
                    SupplierId = supplier.Id,
                    PurchaseDate = DateTime.SpecifyKind(purchaseDate, DateTimeKind.Utc),
                    TotalAmount = total,
                    Status = status,
                    ReferenceNumber = reference,
                    IdempotencyKey = importKey,
                    Notes = "Historical import — stock not adjusted by this document.",
                    PurchaseItems = items
                });
                created++;
            }
            catch (Exception ex)
            {
                skipped += lines.Count;
                errors.Add($"Purchase '{importKey}': {ex.Message}");
                foreach (var (rowNumber, row) in lines)
                {
                    committedFailures.Add(new ImportRowFailure(
                        rowNumber, importKey, [ex.Message],
                        row.ToDictionary(k => k.Key, v => v.Value)));
                }
            }
        }

        await _context.SaveChangesAsync(ct);
        return (created, updated, skipped, errors, committedFailures);
    }

    private async Task<ImportRowResult> UpsertCategoryAsync(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string?> mapping,
        string naturalKey,
        CancellationToken ct)
    {
        var name = ImportColumnMapper.GetMappedValue(row, mapping, "Name").Trim();
        var description = ImportColumnMapper.GetMappedValue(row, mapping, "Description");

        var existing = await _context.Categories
            .FirstOrDefaultAsync(x => x.Name.ToLower() == naturalKey, ct);

        if (existing is null)
        {
            _context.Categories.Add(new Category
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim()
            });
            return ImportRowResult.Created;
        }

        existing.Name = name;
        existing.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        return ImportRowResult.Updated;
    }

    private async Task<ImportRowResult> UpsertProductAsync(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string?> mapping,
        string naturalKey,
        CancellationToken ct)
    {
        var sku = ImportColumnMapper.GetMappedValue(row, mapping, "SKU").Trim();
        var name = ImportColumnMapper.GetMappedValue(row, mapping, "Name").Trim();
        var categoryName = ImportColumnMapper.GetMappedValue(row, mapping, "CategoryName").Trim();
        var description = ImportColumnMapper.GetMappedValue(row, mapping, "Description");
        var cost = ParseDecimal(ImportColumnMapper.GetMappedValue(row, mapping, "CostPrice"), 0m);
        var sale = ParseDecimal(ImportColumnMapper.GetMappedValue(row, mapping, "SalePrice"), 0m);
        var reorder = (int)ParseDecimal(ImportColumnMapper.GetMappedValue(row, mapping, "ReorderLevel"), 0m);
        var stock = ParseDecimal(ImportColumnMapper.GetMappedValue(row, mapping, "CurrentStock"), 0m);
        var barcode = ImportColumnMapper.GetMappedValue(row, mapping, "Barcode");
        var isActive = ParseBool(ImportColumnMapper.GetMappedValue(row, mapping, "IsActive"), true);

        var category = await _context.Categories
            .FirstOrDefaultAsync(x => x.Name.ToLower() == categoryName.ToLowerInvariant(), ct);

        if (category is null)
            throw new InvalidOperationException($"Category '{categoryName}' was not found.");

        var existing = await _context.Products.FirstOrDefaultAsync(x => x.SKU == sku, ct);
        if (existing is null)
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                CategoryId = category.Id,
                Name = name,
                SKU = sku,
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                CostPrice = cost,
                SalePrice = sale,
                ReorderLevel = reorder,
                CurrentStock = stock,
                Barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim(),
                IsActive = isActive
            };

            _context.Products.Add(product);
            _context.PriceHistories.Add(new PriceHistory
            {
                ProductId = product.Id,
                CostPrice = cost,
                SalePrice = sale
            });

            await _context.SaveChangesAsync(ct);
            await _inventoryService.CreateInventoryForProductAsync(product, ct);
            return ImportRowResult.Created;
        }

        existing.CategoryId = category.Id;
        existing.Name = name;
        existing.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        existing.CostPrice = cost;
        existing.SalePrice = sale;
        existing.ReorderLevel = reorder;
        existing.CurrentStock = stock;
        existing.Barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
        existing.IsActive = isActive;

        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductId == existing.Id, ct);
        if (inventory is not null)
        {
            inventory.CurrentStock = stock;
            inventory.LastUpdated = DateTime.UtcNow;
        }

        return ImportRowResult.Updated;
    }

    private async Task<ImportRowResult> UpsertCustomerAsync(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string?> mapping,
        string naturalKey,
        CancellationToken ct)
    {
        var email = ImportPreviewBuilder.NormalizeEmail(
            ImportColumnMapper.GetMappedValue(row, mapping, "Email"));
        var existing = await _context.Customers
            .FirstOrDefaultAsync(x => x.Email.ToLower() == naturalKey, ct);

        var firstName = ImportColumnMapper.GetMappedValue(row, mapping, "FirstName").Trim();
        var lastName = ImportColumnMapper.GetMappedValue(row, mapping, "LastName").Trim();
        var phone = ImportColumnMapper.GetMappedValue(row, mapping, "PhoneNumber").Trim();
        var address = NullIfBlank(ImportColumnMapper.GetMappedValue(row, mapping, "Address")) ?? string.Empty;
        var city = NullIfBlank(ImportColumnMapper.GetMappedValue(row, mapping, "City")) ?? string.Empty;
        var country = NullIfBlank(ImportColumnMapper.GetMappedValue(row, mapping, "Country")) ?? "Pakistan";
        var postal = NullIfBlank(ImportColumnMapper.GetMappedValue(row, mapping, "PostalCode")) ?? string.Empty;
        var company = ImportColumnMapper.GetMappedValue(row, mapping, "Company");
        var notes = ImportColumnMapper.GetMappedValue(row, mapping, "Notes");
        var isActive = ParseBool(ImportColumnMapper.GetMappedValue(row, mapping, "IsActive"), true);

        if (existing is null)
        {
            _context.Customers.Add(new Customer
            {
                Id = Guid.NewGuid(),
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                PhoneNumber = phone,
                Address = address,
                City = city,
                Country = country,
                PostalCode = postal,
                Company = string.IsNullOrWhiteSpace(company) ? null : company.Trim(),
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                IsActive = isActive
            });
            return ImportRowResult.Created;
        }

        existing.FirstName = firstName;
        existing.LastName = lastName;
        existing.Email = email;
        existing.PhoneNumber = phone;
        existing.Address = address;
        existing.City = city;
        existing.Country = country;
        existing.PostalCode = postal;
        existing.Company = string.IsNullOrWhiteSpace(company) ? null : company.Trim();
        existing.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        existing.IsActive = isActive;
        return ImportRowResult.Updated;
    }

    private async Task<ImportRowResult> UpsertSupplierAsync(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string?> mapping,
        string naturalKey,
        CancellationToken ct)
    {
        var name = ImportColumnMapper.GetMappedValue(row, mapping, "Name").Trim();
        var email = ImportColumnMapper.GetMappedValue(row, mapping, "Email").Trim();
        var phone = ImportColumnMapper.GetMappedValue(row, mapping, "Phone").Trim();
        var address = NullIfBlank(ImportColumnMapper.GetMappedValue(row, mapping, "Address")) ?? string.Empty;
        var contact = ImportColumnMapper.GetMappedValue(row, mapping, "ContactPerson");
        var notes = ImportColumnMapper.GetMappedValue(row, mapping, "Notes");

        Supplier? existing = naturalKey.StartsWith("email:", StringComparison.OrdinalIgnoreCase)
            ? await _context.Suppliers.FirstOrDefaultAsync(
                x => x.Email.ToLower() == email.ToLowerInvariant(), ct)
            : await _context.Suppliers.FirstOrDefaultAsync(
                x => x.Name.ToLower() == name.ToLowerInvariant(), ct);

        if (existing is null)
        {
            _context.Suppliers.Add(new Supplier
            {
                Id = Guid.NewGuid(),
                Name = name,
                Email = email,
                Phone = phone,
                Address = address,
                ContactPerson = string.IsNullOrWhiteSpace(contact) ? null : contact.Trim(),
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
            });
            return ImportRowResult.Created;
        }

        existing.Name = name;
        existing.Email = email;
        existing.Phone = phone;
        existing.Address = address;
        existing.ContactPerson = string.IsNullOrWhiteSpace(contact) ? null : contact.Trim();
        existing.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        return ImportRowResult.Updated;
    }

    private async Task<ImportRowResult> UpsertExpenseAsync(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string?> mapping,
        string naturalKey,
        CancellationToken ct)
    {
        var existing = await _context.Expenses
            .FirstOrDefaultAsync(x => x.ExternalImportKey == naturalKey, ct);

        var title = ImportColumnMapper.GetMappedValue(row, mapping, "Title").Trim();
        var categoryName = ImportColumnMapper.GetMappedValue(row, mapping, "CategoryName").Trim();
        var amount = ParseDecimal(ImportColumnMapper.GetMappedValue(row, mapping, "Amount"), 0);
        var expenseDate = DateTime.Parse(ImportColumnMapper.GetMappedValue(row, mapping, "ExpenseDate"));
        var method = NullIfBlank(ImportColumnMapper.GetMappedValue(row, mapping, "PaymentMethod")) ?? "Cash";
        var vendor = ImportColumnMapper.GetMappedValue(row, mapping, "Vendor");
        var reference = ImportColumnMapper.GetMappedValue(row, mapping, "ReferenceNumber");
        var description = ImportColumnMapper.GetMappedValue(row, mapping, "Description");

        var category = await _context.ExpenseCategories
            .FirstOrDefaultAsync(c => c.Name.ToLower() == categoryName.ToLowerInvariant(), ct);

        if (category is null)
        {
            category = new ExpenseCategory
            {
                Id = Guid.NewGuid(),
                Name = categoryName,
                Description = "Created during historical expense import"
            };
            _context.ExpenseCategories.Add(category);
            await _context.SaveChangesAsync(ct);
        }

        if (existing is not null)
        {
            existing.Title = title;
            existing.Amount = amount;
            existing.ExpenseDate = DateTime.SpecifyKind(expenseDate, DateTimeKind.Utc);
            existing.ExpenseCategoryId = category.Id;
            existing.PaymentMethod = method;
            existing.Vendor = NullIfBlank(vendor);
            existing.ReferenceNumber = NullIfBlank(reference);
            existing.Description = NullIfBlank(description);
            existing.Status = "Approved";
            return ImportRowResult.Updated;
        }

        _context.Expenses.Add(new Expense
        {
            Id = Guid.NewGuid(),
            Title = title,
            Amount = amount,
            ExpenseDate = DateTime.SpecifyKind(expenseDate, DateTimeKind.Utc),
            ExpenseCategoryId = category.Id,
            PaymentMethod = method,
            Vendor = NullIfBlank(vendor),
            ReferenceNumber = NullIfBlank(reference),
            Description = NullIfBlank(description),
            Status = "Approved",
            ExternalImportKey = naturalKey
        });
        return ImportRowResult.Created;
    }

    private async Task<ImportRowResult> UpsertCustomerPaymentAsync(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string?> mapping,
        string naturalKey,
        CancellationToken ct)
    {
        var existing = await _context.Payments
            .FirstOrDefaultAsync(x => x.ExternalImportKey == naturalKey, ct);

        if (existing is not null)
            return ImportRowResult.Updated; // no double-post

        var orderKey = ImportColumnMapper.GetMappedValue(row, mapping, "OrderImportKey").Trim();
        var amount = ParseDecimal(ImportColumnMapper.GetMappedValue(row, mapping, "Amount"), 0);
        var paymentDate = DateTime.Parse(ImportColumnMapper.GetMappedValue(row, mapping, "PaymentDate"));
        var method = ImportColumnMapper.GetMappedValue(row, mapping, "PaymentMethod").Trim();
        var reference = ImportColumnMapper.GetMappedValue(row, mapping, "ReferenceNo");

        var order = await _context.Orders.FirstOrDefaultAsync(o => o.ExternalImportKey == orderKey, ct)
            ?? throw new InvalidOperationException($"Order with import key '{orderKey}' was not found. Import sales first.");

        _context.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            Amount = amount,
            PaymentMethod = method,
            PaymentDate = DateTime.SpecifyKind(paymentDate, DateTimeKind.Utc),
            ReferenceNo = NullIfBlank(reference),
            Status = PaymentStatusNames.Completed,
            Currency = "PKR",
            ExternalImportKey = naturalKey
        });

        order.PaymentStatus = "Paid";
        return ImportRowResult.Created;
    }

    private async Task<ImportRowResult> UpsertSupplierPaymentAsync(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string?> mapping,
        string naturalKey,
        CancellationToken ct)
    {
        var existing = await _context.PurchaseInvoices
            .FirstOrDefaultAsync(x => x.ExternalImportKey == naturalKey, ct);

        if (existing is not null)
            return ImportRowResult.Updated;

        var purchaseKey = ImportColumnMapper.GetMappedValue(row, mapping, "PurchaseImportKey").Trim();
        var amount = ParseDecimal(ImportColumnMapper.GetMappedValue(row, mapping, "Amount"), 0);
        var paymentDate = DateTime.Parse(ImportColumnMapper.GetMappedValue(row, mapping, "PaymentDate"));
        var reference = ImportColumnMapper.GetMappedValue(row, mapping, "ReferenceNo");

        var purchase = await _context.Purchases.FirstOrDefaultAsync(p => p.IdempotencyKey == purchaseKey, ct)
            ?? throw new InvalidOperationException($"Purchase with import key '{purchaseKey}' was not found. Import purchases first.");

        _context.PurchaseInvoices.Add(new PurchaseInvoice
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = NullIfBlank(reference) ?? $"IMP-{naturalKey}",
            SupplierId = purchase.SupplierId,
            PurchaseId = purchase.Id,
            IssueDate = DateTime.SpecifyKind(paymentDate, DateTimeKind.Utc),
            DueDate = DateTime.SpecifyKind(paymentDate, DateTimeKind.Utc),
            SubTotal = amount,
            Tax = 0,
            Total = amount,
            AmountPaid = amount,
            Status = "Paid",
            Notes = "Historical supplier payment import",
            ExternalImportKey = naturalKey
        });

        purchase.Status = "Paid";
        return ImportRowResult.Created;
    }

    private async Task<ImportRowResult> UpsertInventoryAdjustmentAsync(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string?> mapping,
        string naturalKey,
        CancellationToken ct)
    {
        var refKey = $"import:{naturalKey}";
        var existing = await _context.StockTransactions
            .AnyAsync(x => x.ReferenceNumber == refKey, ct);

        if (existing)
            return ImportRowResult.Updated;

        var sku = ImportColumnMapper.GetMappedValue(row, mapping, "ProductSKU").Trim();
        var qty = ParseDecimal(ImportColumnMapper.GetMappedValue(row, mapping, "Quantity"), 0);
        var type = ImportColumnMapper.GetMappedValue(row, mapping, "TransactionType").Trim();
        if (string.IsNullOrWhiteSpace(type))
            type = StockTransactionTypeNames.Adjustment;

        if (!StockTransactionTypeNames.IsValid(type))
            type = StockTransactionTypeNames.Adjustment;

        var notes = ImportColumnMapper.GetMappedValue(row, mapping, "Notes");

        var product = await _context.Products.FirstOrDefaultAsync(p => p.SKU == sku, ct)
            ?? throw new InvalidOperationException($"Product SKU '{sku}' was not found.");

        await _inventoryService.AdjustStockAsync(
            new StockAdjustmentRequest
            {
                ProductId = product.Id,
                Quantity = Math.Abs(qty),
                TransactionType = type,
                ReferenceNumber = refKey,
                Notes = string.IsNullOrWhiteSpace(notes)
                    ? "Historical inventory adjustment import"
                    : notes.Trim()
            },
            ct);

        return ImportRowResult.Created;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal ParseDecimal(string value, decimal fallback) =>
        decimal.TryParse(value, out var parsed) ? parsed : fallback;

    private static bool ParseBool(string value, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim() switch
        {
            "1" or "true" or "yes" or "y" => true,
            "0" or "false" or "no" or "n" => false,
            _ => fallback
        };
    }

    private enum ImportRowResult
    {
        Skip,
        Created,
        Updated
    }
}
