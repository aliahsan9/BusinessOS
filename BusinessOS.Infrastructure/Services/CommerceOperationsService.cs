using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Application.Features.Inventory.Services;
using BusinessOS.Application.Features.Operations.DTOs;
using BusinessOS.Application.Features.Operations.Services;
using BusinessOS.Application.Features.Payments.Commands.RefundPayment;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class CommerceOperationsService : ICommerceOperationsService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IInventoryService _inventory;
    private readonly ISender _sender;

    public CommerceOperationsService(
        IApplicationDbContext db,
        ICurrentUserService user,
        IInventoryService inventory,
        ISender sender)
    {
        _db = db;
        _user = user;
        _inventory = inventory;
        _sender = sender;
    }

    public async Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.WarehouseView, PermissionCodes.WarehouseManage, PermissionCodes.InventoryView);
        return await _db.Warehouses.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new WarehouseDto(x.Id, x.Name, x.Code, x.Address, x.IsDefault, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<WarehouseDto> SaveWarehouseAsync(Guid? id, WarehouseRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.WarehouseManage);
        var warehouse = id is null ? new Warehouse() : await _db.Warehouses.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Warehouse not found.");
        if (id is null)
            _db.Warehouses.Add(warehouse);
        warehouse.Name = Ops.Required(request.Name, "Name");
        warehouse.Code = request.Code?.Trim();
        warehouse.Address = request.Address?.Trim();
        warehouse.IsDefault = request.IsDefault;
        if (request.IsDefault)
        {
            var others = await _db.Warehouses.Where(x => x.Id != warehouse.Id && x.IsDefault).ToListAsync(cancellationToken);
            foreach (var other in others)
                other.IsDefault = false;
        }
        await _db.SaveChangesAsync(cancellationToken);
        return new WarehouseDto(warehouse.Id, warehouse.Name, warehouse.Code, warehouse.Address, warehouse.IsDefault, warehouse.IsActive);
    }

    public async Task<IReadOnlyList<WarehouseStockDto>> GetWarehouseStockAsync(Guid warehouseId, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.WarehouseView, PermissionCodes.InventoryView);
        return await (from stock in _db.WarehouseStocks.AsNoTracking()
                      join product in _db.Products.AsNoTracking() on stock.ProductId equals product.Id
                      where stock.WarehouseId == warehouseId
                      orderby product.Name
                      select new WarehouseStockDto(product.Id, product.Name, stock.OnHand, stock.Reserved, stock.OnHand - stock.Reserved))
            .ToListAsync(cancellationToken);
    }

    public async Task TransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.WarehouseManage, PermissionCodes.InventoryAdjust);
        if (request.FromWarehouseId == request.ToWarehouseId)
            throw new BadRequestException("Choose two different warehouses.");
        if (request.Lines is null || request.Lines.Count == 0)
            throw new BadRequestException("A transfer needs at least one line.");
        var allowNegative = await _db.TenantSettings.AsNoTracking().Select(x => (bool?)x.AllowNegativeStock).FirstOrDefaultAsync(cancellationToken) == true;
        var transfer = new StockTransfer
        {
            TransferNumber = Ops.Number("TR"),
            FromWarehouseId = request.FromWarehouseId,
            ToWarehouseId = request.ToWarehouseId,
            Notes = request.Notes,
            Status = TransferStatuses.Completed
        };
        var transaction = await _db.BeginTransactionAsync(cancellationToken);
        try
        {
            _db.StockTransfers.Add(transfer);
            foreach (var line in request.Lines)
            {
                if (line.Quantity <= 0)
                    throw new BadRequestException("Transfer quantity must be positive.");
                var source = await _db.WarehouseStocks.FirstOrDefaultAsync(x => x.WarehouseId == request.FromWarehouseId && x.ProductId == line.ProductId, cancellationToken)
                    ?? throw new BadRequestException("Source warehouse does not have this product.");
                if (!allowNegative && source.OnHand < line.Quantity)
                    throw new BadRequestException("Source warehouse does not have enough available stock.");
                source.OnHand -= line.Quantity;
                var destination = await _db.WarehouseStocks.FirstOrDefaultAsync(x => x.WarehouseId == request.ToWarehouseId && x.ProductId == line.ProductId, cancellationToken);
                if (destination is null)
                {
                    destination = new WarehouseStock { WarehouseId = request.ToWarehouseId, ProductId = line.ProductId };
                    _db.WarehouseStocks.Add(destination);
                }
                destination.OnHand += line.Quantity;
                _db.StockTransferLines.Add(new StockTransferLine
                {
                    StockTransferId = transfer.Id,
                    ProductId = line.ProductId,
                    Quantity = line.Quantity
                });
            }
            await _db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
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

    public async Task<IReadOnlyList<InventoryAlertDto>> GetAlertsAsync(CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.InventoryView, PermissionCodes.WarehouseView);
        var rows = await _db.Products.AsNoTracking()
            .Where(x => x.IsActive && x.Kind != ProductKinds.Service && x.CurrentStock <= x.ReorderLevel)
            .OrderBy(x => x.CurrentStock)
            .Select(x => new InventoryAlertDto(x.Id, x.Name, x.CurrentStock, x.ReorderLevel, x.CurrentStock <= 0 ? "Out of stock" : "Low stock"))
            .ToListAsync(cancellationToken);
        return rows;
    }

    public async Task<Guid> SaveCatalogItemAsync(CatalogItemRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.ProductCreate);
        var sku = Ops.Required(request.Sku, "SKU");
        if (await _db.Products.AnyAsync(x => x.SKU == sku, cancellationToken))
            throw new ConflictException("SKU already exists in this company.");
        if (request.ParentCategoryId is not null)
        {
            var category = await _db.Categories.FirstOrDefaultAsync(x => x.Id == request.CategoryId, cancellationToken)
                ?? throw new NotFoundException("Category not found.");
            category.ParentCategoryId = request.ParentCategoryId;
        }
        var kind = string.Equals(request.Kind, ProductKinds.Service, StringComparison.OrdinalIgnoreCase) ? ProductKinds.Service : ProductKinds.Physical;
        var product = new Product
        {
            CategoryId = request.CategoryId,
            Name = Ops.Required(request.Name, "Name"),
            SKU = sku,
            Description = request.Description,
            CostPrice = request.CostPrice,
            SalePrice = request.SalePrice,
            ReorderLevel = request.ReorderLevel ?? 0,
            Kind = kind,
            Barcode = request.Barcode,
            UnitOfMeasure = request.UnitOfMeasure,
            PricingModel = request.PricingModel,
            Duration = request.Duration,
            DeliveryDetails = request.DeliveryDetails,
            CurrentStock = 0
        };
        _db.Products.Add(product);
        _db.PriceHistories.Add(new PriceHistory { ProductId = product.Id, CostPrice = product.CostPrice, SalePrice = product.SalePrice, ChangedByUserId = _user.UserId });
        await _db.SaveChangesAsync(cancellationToken);
        if (kind == ProductKinds.Physical)
            await _inventory.CreateInventoryForProductAsync(product, cancellationToken);
        return product.Id;
    }

    public async Task<FulfillmentDto> SaveFulfillmentAsync(FulfillmentRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.FulfillmentManage, PermissionCodes.OrderUpdate);
        var order = await _db.Orders.Include(x => x.OrderItems).FirstOrDefaultAsync(x => x.Id == request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order not found.");
        var fulfillment = await _db.OrderFulfillments.FirstOrDefaultAsync(x => x.OrderId == order.Id, cancellationToken);
        if (fulfillment is null)
        {
            fulfillment = new OrderFulfillment { OrderId = order.Id, Status = FulfillmentStatuses.Pending };
            _db.OrderFulfillments.Add(fulfillment);
        }
        fulfillment.Carrier = request.Carrier?.Trim();
        fulfillment.TrackingNumber = request.TrackingNumber?.Trim();
        if (request.Lines is { Count: > 0 })
        {
            var shipped = request.Lines.Sum(x => x.Quantity);
            var ordered = order.OrderItems.Sum(x => x.Quantity);
            fulfillment.Status = shipped < ordered ? FulfillmentStatuses.Partial : FulfillmentStatuses.Packed;
            foreach (var line in request.Lines)
            {
                var item = order.OrderItems.FirstOrDefault(x => x.ProductId == line.ProductId)
                    ?? throw new BadRequestException("Fulfillment line does not match an order item.");
                _db.FulfillmentLines.Add(new FulfillmentLine
                {
                    OrderFulfillmentId = fulfillment.Id,
                    OrderItemId = item.Id,
                    Quantity = line.Quantity
                });
            }
        }
        await _db.SaveChangesAsync(cancellationToken);
        return MapFulfillment(fulfillment);
    }

    public async Task<FulfillmentDto> AdvanceFulfillmentAsync(Guid id, string status, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.FulfillmentManage, PermissionCodes.OrderUpdate);
        var allowed = new[] { FulfillmentStatuses.Picking, FulfillmentStatuses.Packed, FulfillmentStatuses.Shipped, FulfillmentStatuses.Delivered, FulfillmentStatuses.Partial };
        if (!allowed.Contains(status, StringComparer.OrdinalIgnoreCase))
            throw new BadRequestException("Unsupported fulfillment status.");
        var fulfillment = await _db.OrderFulfillments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Fulfillment not found.");
        fulfillment.Status = status;
        if (status.Equals(FulfillmentStatuses.Shipped, StringComparison.OrdinalIgnoreCase))
            fulfillment.ShippedAt = DateTime.UtcNow;
        if (status.Equals(FulfillmentStatuses.Delivered, StringComparison.OrdinalIgnoreCase))
            fulfillment.DeliveredAt = DateTime.UtcNow;
        fulfillment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapFulfillment(fulfillment);
    }

    public async Task<ReturnDto> CreateReturnAsync(ReturnRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.ReturnManage, PermissionCodes.OrderUpdate);
        var order = await _db.Orders.Include(x => x.OrderItems).FirstOrDefaultAsync(x => x.Id == request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order not found.");
        if (!order.Status.Equals(OrderStatusNames.Completed, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Returns can be requested for completed orders.");
        var salesReturn = new SalesReturn
        {
            ReturnNumber = Ops.Number("RTN"),
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            Reason = Ops.Required(request.Reason, "Reason"),
            Status = ReturnStatuses.Requested
        };
        _db.SalesReturns.Add(salesReturn);
        foreach (var line in request.Lines)
        {
            var item = order.OrderItems.FirstOrDefault(x => x.ProductId == line.ProductId)
                ?? throw new BadRequestException("Returned product is not on the order.");
            if (line.Quantity <= 0 || line.Quantity > item.Quantity)
                throw new BadRequestException("Return quantity is invalid.");
            _db.SalesReturnLines.Add(new SalesReturnLine
            {
                SalesReturnId = salesReturn.Id,
                ProductId = line.ProductId,
                Quantity = line.Quantity
            });
        }
        await _db.SaveChangesAsync(cancellationToken);
        return MapReturn(salesReturn);
    }

    public async Task<IReadOnlyList<ReturnDto>> GetReturnsAsync(CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.ReturnManage, PermissionCodes.OrderView);
        var rows = await _db.SalesReturns.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(cancellationToken);
        return rows.Select(MapReturn).ToList();
    }

    public async Task<ReturnDto> DecideReturnAsync(Guid id, ReturnDecisionRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.ReturnManage);
        var salesReturn = await _db.SalesReturns.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Return not found.");
        if (salesReturn.Status is not (ReturnStatuses.Requested))
            throw new ConflictException("This return has already been decided.");
        if (!request.Approve)
        {
            salesReturn.Status = ReturnStatuses.Rejected;
            salesReturn.DecisionNotes = request.Notes;
            await _db.SaveChangesAsync(cancellationToken);
            return MapReturn(salesReturn);
        }

        salesReturn.Restock = request.Restock;
        salesReturn.RefundAmount = request.RefundAmount;
        salesReturn.PaymentId = request.PaymentId;
        salesReturn.DecisionNotes = request.Notes;
        salesReturn.Status = request.RefundAmount > 0 ? ReturnStatuses.Refunded : ReturnStatuses.Approved;
        if (request.Restock)
        {
            var lines = await _db.SalesReturnLines.Where(x => x.SalesReturnId == salesReturn.Id).ToListAsync(cancellationToken);
            foreach (var line in lines)
            {
                var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == line.ProductId, cancellationToken);
                if (product?.Kind == ProductKinds.Service)
                    continue;
                await _inventory.IncreaseStockAsync(new StockChangeRequest
                {
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    ReferenceNumber = salesReturn.ReturnNumber,
                    Notes = "Restocked from approved return"
                }, cancellationToken);
            }
        }
        if (request.RefundAmount > 0)
        {
            if (request.PaymentId is null)
                throw new BadRequestException("A refund must reference the original payment.");
            Ops.Require(_user, PermissionCodes.PaymentUpdate, PermissionCodes.PaymentCreate);
            await _sender.Send(new RefundPaymentCommand(request.PaymentId.Value, request.RefundAmount, null, salesReturn.Id.ToString("N")), cancellationToken);
            _db.IncomeRecords.Add(new IncomeRecord
            {
                Category = "Refund",
                Amount = -request.RefundAmount,
                OccurredOn = DateTime.UtcNow,
                PaymentMethod = "Refund",
                Reference = salesReturn.ReturnNumber,
                Notes = "Customer refund",
                Status = "Approved"
            });
        }
        await _db.SaveChangesAsync(cancellationToken);
        return MapReturn(salesReturn);
    }

    public async Task<IncomeDto> SaveIncomeAsync(IncomeRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.FinanceView, PermissionCodes.ExpenseCreate);
        var income = new IncomeRecord
        {
            Category = Ops.Required(request.Category, "Category"),
            Amount = request.Amount,
            OccurredOn = request.OccurredOn,
            PaymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod) ? "Other" : request.PaymentMethod.Trim(),
            Reference = request.Reference,
            Notes = request.Notes,
            IsRecurring = request.IsRecurring,
            Status = "Approved"
        };
        _db.IncomeRecords.Add(income);
        await _db.SaveChangesAsync(cancellationToken);
        return MapIncome(income);
    }

    public async Task<PagedResult<IncomeDto>> GetIncomeAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.FinanceView);
        var paged = await _db.IncomeRecords.AsNoTracking().OrderByDescending(x => x.OccurredOn).PageAsync(page, pageSize, cancellationToken);
        return new PagedResult<IncomeDto>
        {
            Items = paged.Items.Select(MapIncome).ToList(),
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount
        };
    }

    public async Task<PurchaseInvoiceDto> SavePurchaseInvoiceAsync(PurchaseInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.PurchaseOrderCreate, PermissionCodes.FinanceView);
        var invoice = new PurchaseInvoice
        {
            InvoiceNumber = Ops.Required(request.InvoiceNumber, "Invoice number"),
            SupplierId = request.SupplierId,
            PurchaseId = request.PurchaseId,
            IssueDate = request.IssueDate,
            DueDate = request.DueDate,
            SubTotal = request.SubTotal,
            Tax = request.Tax,
            Total = Math.Round(request.SubTotal + request.Tax, 2),
            Notes = request.Notes,
            AttachmentUrl = request.AttachmentUrl,
            Status = "Issued"
        };
        _db.PurchaseInvoices.Add(invoice);
        await _db.SaveChangesAsync(cancellationToken);
        return MapPurchaseInvoice(invoice);
    }

    public async Task<IReadOnlyList<PurchaseInvoiceDto>> GetPurchaseInvoicesAsync(CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.FinanceView, PermissionCodes.PurchaseOrderView);
        var rows = await _db.PurchaseInvoices.AsNoTracking().OrderByDescending(x => x.IssueDate).Take(100).ToListAsync(cancellationToken);
        return rows.Select(MapPurchaseInvoice).ToList();
    }

    public async Task<PurchaseInvoiceDto> PayPurchaseInvoiceAsync(Guid id, SupplierPaymentRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.ExpenseCreate, PermissionCodes.FinanceView);
        if (request.Amount <= 0)
            throw new BadRequestException("Payment amount must be positive.");
        var invoice = await _db.PurchaseInvoices.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Purchase invoice not found.");
        if (invoice.AmountPaid + request.Amount > invoice.Total)
            throw new BadRequestException("Payment exceeds the invoice balance.");
        invoice.AmountPaid += request.Amount;
        invoice.Status = invoice.AmountPaid >= invoice.Total ? "Paid" : "Partially Paid";
        var category = await _db.ExpenseCategories.FirstOrDefaultAsync(x => x.Name == "Purchases", cancellationToken);
        if (category is null)
        {
            category = new ExpenseCategory { Name = "Purchases", Description = "Supplier payments" };
            _db.ExpenseCategories.Add(category);
            await _db.SaveChangesAsync(cancellationToken);
        }
        _db.Expenses.Add(new Expense
        {
            Title = $"Supplier payment {invoice.InvoiceNumber}",
            Amount = request.Amount,
            ExpenseDate = DateTime.UtcNow,
            ExpenseCategoryId = category.Id,
            PaymentMethod = request.PaymentMethod,
            ReferenceNumber = request.Reference,
            Status = "Paid",
            PurchaseInvoiceId = invoice.Id
        });
        await _db.SaveChangesAsync(cancellationToken);
        return MapPurchaseInvoice(invoice);
    }

    public async Task<IReadOnlyList<TaxRateDto>> GetTaxRatesAsync(CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.FinanceView, PermissionCodes.SettingsView);
        return await _db.TaxRates.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new TaxRateDto(x.Id, x.Name, x.RatePercent, x.IsInclusive, x.IsDefault, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<TaxRateDto> SaveTaxRateAsync(TaxRateRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.SettingsUpdate, PermissionCodes.FinanceView);
        if (request.IsDefault)
        {
            var current = await _db.TaxRates.Where(x => x.IsDefault).ToListAsync(cancellationToken);
            foreach (var rate in current)
                rate.IsDefault = false;
        }
        var tax = new TaxRate
        {
            Name = Ops.Required(request.Name, "Name"),
            RatePercent = request.RatePercent,
            IsInclusive = request.IsInclusive,
            IsDefault = request.IsDefault
        };
        _db.TaxRates.Add(tax);
        await _db.SaveChangesAsync(cancellationToken);
        return new TaxRateDto(tax.Id, tax.Name, tax.RatePercent, tax.IsInclusive, tax.IsDefault, tax.IsActive);
    }

    public async Task<SubscriptionExpenseDto> SaveSubscriptionAsync(SubscriptionExpenseRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.ExpenseCreate, PermissionCodes.FinanceView);
        var item = new OperationalSubscription
        {
            Provider = Ops.Required(request.Provider, "Provider"),
            Name = Ops.Required(request.Name, "Name"),
            BillingCycle = string.IsNullOrWhiteSpace(request.BillingCycle) ? "Monthly" : request.BillingCycle.Trim(),
            Cost = request.Cost,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.Trim(),
            RenewalDate = request.RenewalDate,
            ExpenseCategory = request.ExpenseCategory,
            PaymentStatus = "Scheduled"
        };
        _db.OperationalSubscriptions.Add(item);
        await _db.SaveChangesAsync(cancellationToken);
        return MapSubscription(item);
    }

    public async Task<IReadOnlyList<SubscriptionExpenseDto>> GetSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.FinanceView, PermissionCodes.ExpenseView);
        return await _db.OperationalSubscriptions.AsNoTracking().OrderBy(x => x.RenewalDate)
            .Select(x => new SubscriptionExpenseDto(x.Id, x.Provider, x.Name, x.BillingCycle, x.Cost, x.Currency, x.RenewalDate, x.PaymentStatus))
            .ToListAsync(cancellationToken);
    }

    public async Task<CashFlowDto> GetCashFlowAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.FinanceView);
        var opening = await _db.TenantSettings.AsNoTracking().Select(x => (decimal?)x.OpeningCashBalance).FirstOrDefaultAsync(cancellationToken) ?? 0;
        var payments = await _db.Payments.AsNoTracking()
            .Where(x => x.PaymentDate >= from && x.PaymentDate <= to && x.Status == PaymentStatusNames.Completed)
            .SumAsync(x => (decimal?)(x.Amount - x.RefundedAmount), cancellationToken) ?? 0;
        var income = await _db.IncomeRecords.AsNoTracking()
            .Where(x => x.OccurredOn >= from && x.OccurredOn <= to && x.Status == "Approved")
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
        var expenses = await _db.Expenses.AsNoTracking()
            .Where(x => x.ExpenseDate >= from && x.ExpenseDate <= to && (x.Status == "Paid" || x.Status == "Approved"))
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
        var receivables = await _db.Invoices.AsNoTracking()
            .Where(x => x.Status != "Cancelled" && x.Status != "Draft")
            .SumAsync(x => (decimal?)x.OutstandingAmount, cancellationToken) ?? 0;
        var payables = await _db.PurchaseInvoices.AsNoTracking()
            .Where(x => x.Status != "Cancelled")
            .SumAsync(x => (decimal?)(x.Total - x.AmountPaid), cancellationToken) ?? 0;
        var inflow = payments + income;
        return new CashFlowDto(opening, inflow, expenses, opening + inflow - expenses, receivables, payables);
    }

    public async Task<ProfitAndLossDto> GetProfitAndLossAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.FinanceView);
        var periodStart = from;
        var periodEnd = to;
        var invoices = await _db.Invoices.AsNoTracking()
            .Where(x => x.InvoiceDate >= periodStart && x.InvoiceDate <= periodEnd && x.Status != "Cancelled" && x.Status != "Draft")
            .Select(x => new { x.GrandTotal, x.AmountPaid })
            .ToListAsync(cancellationToken);
        var recognized = invoices.Sum(x => x.GrandTotal);
        var cash = await _db.Payments.AsNoTracking()
            .Where(x => x.PaymentDate >= periodStart && x.PaymentDate <= periodEnd && x.Status == PaymentStatusNames.Completed)
            .SumAsync(x => (decimal?)(x.Amount - x.RefundedAmount), cancellationToken) ?? 0;
        var cogs = await (from item in _db.OrderItems.AsNoTracking()
                          join order in _db.Orders.AsNoTracking() on item.OrderId equals order.Id
                          where order.OrderDate >= periodStart && order.OrderDate <= periodEnd && order.Status == OrderStatusNames.Completed
                          select (decimal?)(item.UnitCost * item.Quantity)).SumAsync(cancellationToken) ?? 0;
        var opex = await _db.Expenses.AsNoTracking()
            .Where(x => x.ExpenseDate >= periodStart && x.ExpenseDate <= periodEnd && x.Status != "Rejected")
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
        var gross = recognized - cogs;
        return new ProfitAndLossDto(recognized, recognized, cash, cogs, gross, opex, gross - opex);
    }

    private static FulfillmentDto MapFulfillment(OrderFulfillment fulfillment) =>
        new(fulfillment.Id, fulfillment.OrderId, fulfillment.Status, fulfillment.Carrier, fulfillment.TrackingNumber, fulfillment.ShippedAt, fulfillment.DeliveredAt);

    private static ReturnDto MapReturn(SalesReturn salesReturn) =>
        new(salesReturn.Id, salesReturn.ReturnNumber, salesReturn.OrderId, salesReturn.Status, salesReturn.Restock, salesReturn.RefundAmount);

    private static IncomeDto MapIncome(IncomeRecord income) =>
        new(income.Id, income.Category, income.Amount, income.OccurredOn, income.PaymentMethod, income.Status, income.Reference);

    private static PurchaseInvoiceDto MapPurchaseInvoice(PurchaseInvoice invoice) =>
        new(invoice.Id, invoice.InvoiceNumber, invoice.SupplierId, invoice.Total, invoice.AmountPaid, invoice.Status, invoice.DueDate);

    private static SubscriptionExpenseDto MapSubscription(OperationalSubscription item) =>
        new(item.Id, item.Provider, item.Name, item.BillingCycle, item.Cost, item.Currency, item.RenewalDate, item.PaymentStatus);
}
