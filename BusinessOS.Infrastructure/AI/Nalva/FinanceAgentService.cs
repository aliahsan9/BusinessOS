using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Audit.Services;
using BusinessOS.Application.Features.FinanceAgent;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Nalva;

public sealed class FinanceAgentService : IFinanceAgentService
{
    private static readonly HashSet<string> RecognizedExpenseStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        ExpenseStatusNames.Approved,
        ExpenseStatusNames.Paid
    };

    private static readonly HashSet<string> ExcludedInvoiceStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Draft",
        "Cancelled"
    };

    private readonly BusinessOSDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly ICurrentUserService _user;
    private readonly IEntityAuditService _audit;
    private readonly ILogger<FinanceAgentService> _logger;

    public FinanceAgentService(
        BusinessOSDbContext db,
        ITenantProvider tenant,
        ICurrentUserService user,
        IEntityAuditService audit,
        ILogger<FinanceAgentService> logger)
    {
        _db = db;
        _tenant = tenant;
        _user = user;
        _audit = audit;
        _logger = logger;
    }

    public async Task<FinanceOverviewDto> GetOverviewAsync(DateTime? startDate = null, DateTime? endDate = null, string? period = null, CancellationToken cancellationToken = default)
    {
        var intel = await GetIntelligenceAsync(startDate, endDate, period, cancellationToken);
        return new FinanceOverviewDto(
            intel.Currency,
            intel.TimeZoneId,
            intel.Period,
            intel.StartDate,
            intel.EndDate,
            intel.SalesRevenue.Value,
            intel.CollectedCash.Value,
            intel.TotalExpenses.Value,
            intel.GrossProfit.Value,
            intel.NetProfit.Value,
            intel.OutstandingReceivables.Value,
            intel.OutstandingPayables.Value,
            await CountOverdueInvoicesAsync(cancellationToken),
            intel.CashBalance.Value,
            intel.CashBalance.Available,
            intel.DataClassification,
            intel.Limitations,
            intel.Formulas);
    }

    public async Task<FinanceIntelligenceDto> GetIntelligenceAsync(DateTime? startDate = null, DateTime? endDate = null, string? period = null, CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.FinanceView, PermissionCodes.NalvaView);
        var ctx = await ResolveContextAsync(startDate, endDate, period, cancellationToken);
        var formulas = new List<string>
        {
            "Sales revenue = SUM(completed order GrandTotal) in period (accrual from orders).",
            "Collected cash = SUM(Completed payments Amount − RefundedAmount) in period. Unpaid invoices are not cash.",
            "Other income = SUM(Approved IncomeRecord.Amount) in period.",
            "Expenses = SUM(Approved/Paid Expense.Amount) in period. Purchase orders and unpaid purchase bills are excluded.",
            "COGS = SUM(OrderItem.UnitCost × Quantity) for completed orders in period.",
            "Gross profit = Sales revenue − COGS (only when COGS rows exist).",
            "Net profit = Gross profit − recognized expenses.",
            "Receivables = invoice OutstandingAmount where status is not Draft/Cancelled.",
            "Payables = purchase invoice (Total − AmountPaid) where status is not Cancelled. Open POs are not payables."
        };

        var limitations = new List<string>();

        var salesRevenue = await _db.Orders.AsNoTracking()
            .Where(x => x.OrderDate >= ctx.Start && x.OrderDate <= ctx.End && x.Status == OrderStatusNames.Completed)
            .SumAsync(x => (decimal?)x.GrandTotal, cancellationToken) ?? 0m;

        var collectedCash = await NetCollectedCashAsync(ctx.Start, ctx.End, cancellationToken);
        var otherIncome = await _db.IncomeRecords.AsNoTracking()
            .Where(x => x.OccurredOn >= ctx.Start && x.OccurredOn <= ctx.End && x.Status == "Approved")
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var expenses = await RecognizedExpensesQuery(ctx.Start, ctx.End)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var cogsRows = await (
            from item in _db.OrderItems.AsNoTracking()
            join order in _db.Orders.AsNoTracking() on item.OrderId equals order.Id
            where order.OrderDate >= ctx.Start && order.OrderDate <= ctx.End && order.Status == OrderStatusNames.Completed
            select new { item.UnitCost, item.Quantity }
        ).ToListAsync(cancellationToken);
        var cogs = cogsRows.Sum(x => x.UnitCost * x.Quantity);
        var hasCogsSignal = cogsRows.Any(x => x.UnitCost > 0);
        if (!hasCogsSignal)
            limitations.Add("COGS and gross profit are unavailable because completed order lines have no positive UnitCost values.");

        decimal? gross = hasCogsSignal ? decimal.Round(salesRevenue - cogs, 2) : null;
        decimal? net = gross.HasValue ? decimal.Round(gross.Value - expenses, 2) : null;
        if (!gross.HasValue)
            limitations.Add("Net profit is unavailable without trusted COGS.");

        var receivables = await OutstandingReceivablesTotalAsync(cancellationToken);
        var overdueReceivables = await OverdueReceivablesTotalAsync(cancellationToken);
        var payables = await OutstandingPayablesTotalAsync(cancellationToken);
        var cash = await ComputeCashBalanceAsync(ctx.AsOf, cancellationToken);

        if (!cash.Available)
            limitations.Add(cash.Limitation ?? "Cash balance is unavailable.");

        var trends = await BuildMonthlyTrendsAsync(ctx, cancellationToken);
        var profitability = await BuildProductProfitabilityAsync(ctx, cancellationToken);
        if (profitability.Count == 0)
            limitations.Add("Product profitability is unavailable for this period (no completed order lines).");

        return new FinanceIntelligenceDto(
            ctx.Currency,
            ctx.TimeZoneId,
            ctx.PeriodLabel,
            ctx.Start,
            ctx.End,
            Metric(true, decimal.Round(salesRevenue, 2), "accrual-orders"),
            Metric(true, decimal.Round(collectedCash, 2), "cash-collected"),
            Metric(true, decimal.Round(otherIncome, 2), "approved-income-records"),
            Metric(true, decimal.Round(expenses, 2), "approved-or-paid-expenses"),
            Metric(hasCogsSignal, hasCogsSignal ? decimal.Round(cogs, 2) : null, "completed-order-unit-cost", hasCogsSignal ? null : "No positive UnitCost data"),
            Metric(gross.HasValue, gross, "sales-revenue-minus-cogs", gross.HasValue ? null : "COGS unavailable"),
            Metric(net.HasValue, net, "gross-profit-minus-expenses", net.HasValue ? null : "Requires trusted gross profit"),
            Metric(true, decimal.Round(receivables, 2), "invoice-outstanding"),
            Metric(true, decimal.Round(payables, 2), "purchase-invoice-outstanding"),
            Metric(true, decimal.Round(overdueReceivables, 2), "overdue-invoice-outstanding"),
            Metric(cash.Available, cash.Value, "opening-plus-net-cash-movements", cash.Limitation),
            trends,
            profitability,
            limitations,
            formulas,
            "REAL");
    }

    public async Task<ExpenseIntelligenceDto> AnalyzeExpensesAsync(DateTime? startDate = null, DateTime? endDate = null, string? period = null, CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.FinanceView, PermissionCodes.ExpenseView, PermissionCodes.NalvaView);
        var ctx = await ResolveContextAsync(startDate, endDate, period, cancellationToken);
        var settings = await EnsureSettingsAsync(cancellationToken);
        var span = ctx.End - ctx.Start;
        var priorStart = ctx.Start - span;
        var priorEnd = ctx.Start.AddTicks(-1);

        var current = await RecognizedExpensesQuery(ctx.Start, ctx.End)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Amount,
                x.ExpenseDate,
                x.Vendor,
                x.IsRecurring,
                x.RecurrencePattern,
                x.Status,
                x.ExpenseCategoryId,
                CategoryName = x.ExpenseCategory.Name
            })
            .ToListAsync(cancellationToken);

        var prior = await RecognizedExpensesQuery(priorStart, priorEnd)
            .Select(x => new { x.ExpenseCategoryId, CategoryName = x.ExpenseCategory.Name, x.Amount })
            .ToListAsync(cancellationToken);

        var total = current.Sum(x => x.Amount);
        var priorTotal = prior.Sum(x => x.Amount);
        decimal? change = priorTotal == 0 ? null : decimal.Round((total - priorTotal) / priorTotal * 100m, 2);

        var categories = current
            .GroupBy(x => new { x.ExpenseCategoryId, x.CategoryName })
            .Select(g =>
            {
                var amount = g.Sum(x => x.Amount);
                var priorAmount = prior.Where(x => x.ExpenseCategoryId == g.Key.ExpenseCategoryId).Sum(x => x.Amount);
                decimal? pct = priorAmount == 0 ? (amount > 0 ? 100m : null) : decimal.Round((amount - priorAmount) / priorAmount * 100m, 2);
                var explanation = pct is null
                    ? $"No prior-period spend for {g.Key.CategoryName}."
                    : pct >= settings.AnomalyVariancePercent
                        ? $"{g.Key.CategoryName} increased {pct}% versus the prior period of equal length."
                        : pct <= -settings.AnomalyVariancePercent
                            ? $"{g.Key.CategoryName} decreased {Math.Abs(pct.Value)}% versus the prior period."
                            : $"{g.Key.CategoryName} is within the configured variance threshold.";
                return new ExpenseCategoryInsightDto(g.Key.ExpenseCategoryId, g.Key.CategoryName, decimal.Round(amount, 2), decimal.Round(priorAmount, 2), pct, g.Count(), explanation);
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        var recurring = current.Where(x => x.IsRecurring)
            .Select(x => new RecurringExpenseDto(x.Id, x.Title, x.Amount, x.CategoryName, x.RecurrencePattern, x.ExpenseDate))
            .ToList();

        var anomalies = new List<ExpenseAnomalyDto>();
        foreach (var group in current.GroupBy(x => x.ExpenseCategoryId))
        {
            var avg = group.Average(x => x.Amount);
            if (avg <= 0) continue;
            foreach (var row in group)
            {
                var variance = (row.Amount - avg) / avg * 100m;
                if (variance < settings.AnomalyVariancePercent) continue;
                anomalies.Add(new ExpenseAnomalyDto(
                    row.Id, row.Title, row.CategoryName, row.Amount, decimal.Round(avg, 2),
                    decimal.Round(variance, 2),
                    $"Amount is {decimal.Round(variance, 1)}% above the category average in this period."));
            }
        }

        var duplicateHints = DetectDuplicateExpenses(current.Select(x => (x.Id, x.Title, x.Amount, x.ExpenseDate, x.Vendor)).ToList());
        var suggestions = await SuggestExpenseCategoriesAsync(cancellationToken);

        var limitations = new List<string>
        {
            "Only Approved and Paid expenses are included. Pending/Rejected expenses and unpaid purchase invoices are excluded.",
            "Category suggestions are advisory and never silently rewrite finalized expense records."
        };

        return new ExpenseIntelligenceDto(
            ctx.Currency, ctx.Start, ctx.End,
            decimal.Round(total, 2), decimal.Round(priorTotal, 2), change,
            categories, recurring, anomalies, duplicateHints, suggestions, limitations);
    }

    public async Task<IReadOnlyList<ExpenseCategorySuggestionDto>> SuggestExpenseCategoriesAsync(CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.FinanceView, PermissionCodes.ExpenseView, PermissionCodes.NalvaView);
        var categories = await _db.ExpenseCategories.AsNoTracking()
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(cancellationToken);
        if (categories.Count == 0)
            return [];

        // Heuristic: expenses whose title/vendor poorly match the assigned category name, or generic "Uncategorized"/Other.
        var expenses = await _db.Expenses.AsNoTracking()
            .Where(x => x.Status != ExpenseStatusNames.Rejected)
            .OrderByDescending(x => x.ExpenseDate)
            .Take(100)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Vendor,
                x.ExpenseCategoryId,
                CategoryName = x.ExpenseCategory.Name
            })
            .ToListAsync(cancellationToken);

        var results = new List<ExpenseCategorySuggestionDto>();
        foreach (var expense in expenses)
        {
            var hay = $"{expense.Title} {expense.Vendor}".ToLowerInvariant();
            var best = categories
                .Select(c => new { c.Id, c.Name, Score = CategoryScore(hay, c.Name) })
                .OrderByDescending(x => x.Score)
                .First();

            var isGeneric = expense.CategoryName.Contains("other", StringComparison.OrdinalIgnoreCase)
                || expense.CategoryName.Contains("uncategor", StringComparison.OrdinalIgnoreCase)
                || expense.CategoryName.Contains("misc", StringComparison.OrdinalIgnoreCase);
            if (best.Id == expense.ExpenseCategoryId && !isGeneric)
                continue;
            if (best.Score < 1)
                continue;

            results.Add(new ExpenseCategorySuggestionDto(
                expense.Id,
                expense.Title,
                expense.Vendor,
                best.Id,
                best.Name,
                best.Score >= 3 ? "high" : best.Score >= 2 ? "medium" : "low",
                isGeneric
                    ? $"Current category '{expense.CategoryName}' looks generic; '{best.Name}' matches title/vendor keywords."
                    : $"Title/vendor keywords align more closely with '{best.Name}' than '{expense.CategoryName}'.",
                Applied: false));
        }

        return results.Take(25).ToList();
    }

    public async Task<ReceivablesIntelligenceDto> GetReceivablesAsync(CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.FinanceView, PermissionCodes.InvoiceView, PermissionCodes.NalvaView);
        var currency = await GetCurrencyAsync(cancellationToken);
        var today = DateTime.UtcNow.Date;
        var rows = await _db.Invoices.AsNoTracking()
            .Where(x => x.OutstandingAmount > 0 && !ExcludedInvoiceStatuses.Contains(x.Status))
            .Select(x => new
            {
                x.Id,
                x.InvoiceNumber,
                x.CustomerId,
                CustomerName = ((x.Customer.FirstName ?? "") + " " + (x.Customer.LastName ?? "")).Trim(),
                Company = x.Customer.Company,
                x.InvoiceDate,
                x.DueDate,
                x.GrandTotal,
                x.AmountPaid,
                x.OutstandingAmount,
                x.Status
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(x =>
        {
            var days = x.DueDate.Date >= today ? 0 : (today - x.DueDate.Date).Days;
            var name = string.IsNullOrWhiteSpace(x.CustomerName) ? (x.Company ?? "Customer") : x.CustomerName;
            var priority = days * 10 + (int)Math.Min(x.OutstandingAmount, 100000);
            return new ReceivableItemDto(
                x.Id, x.InvoiceNumber, x.CustomerId, name, x.InvoiceDate, x.DueDate,
                x.GrandTotal, x.AmountPaid, x.OutstandingAmount, days, x.Status, priority);
        })
        .OrderByDescending(x => x.PriorityScore)
        .ToList();

        AgingBucketDto Bucket(string label, Func<ReceivableItemDto, bool> pred)
        {
            var set = items.Where(pred).ToList();
            return new AgingBucketDto(label, decimal.Round(set.Sum(x => x.OutstandingAmount), 2), set.Count);
        }

        var aging = new List<AgingBucketDto>
        {
            Bucket("Current", x => x.DaysOverdue == 0),
            Bucket("1-30", x => x.DaysOverdue is >= 1 and <= 30),
            Bucket("31-60", x => x.DaysOverdue is >= 31 and <= 60),
            Bucket("61-90", x => x.DaysOverdue is >= 61 and <= 90),
            Bucket("90+", x => x.DaysOverdue > 90)
        };

        var late = items.Where(x => x.DaysOverdue > 0)
            .GroupBy(x => new { x.CustomerId, x.CustomerName })
            .Select(g => new LatePayingCustomerDto(
                g.Key.CustomerId,
                g.Key.CustomerName,
                g.Count(),
                decimal.Round(g.Sum(x => x.OutstandingAmount), 2),
                g.Count()))
            .OrderByDescending(x => x.OverdueAmount)
            .Take(20)
            .ToList();

        return new ReceivablesIntelligenceDto(
            currency,
            decimal.Round(items.Sum(x => x.OutstandingAmount), 2),
            decimal.Round(items.Where(x => x.DaysOverdue > 0).Sum(x => x.OutstandingAmount), 2),
            items.Count(x => x.DaysOverdue > 0),
            aging,
            items,
            late,
            ["Outstanding amounts come from Invoice.OutstandingAmount after recorded payments. Draft/Cancelled invoices are excluded."]);
    }

    public async Task<PayablesIntelligenceDto> GetPayablesAsync(CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.FinanceView, PermissionCodes.NalvaView);
        var settings = await EnsureSettingsAsync(cancellationToken);
        var currency = await GetCurrencyAsync(cancellationToken);
        var today = DateTime.UtcNow.Date;
        var suppliers = await _db.Suppliers.AsNoTracking().Select(x => new { x.Id, x.Name }).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var rows = await _db.PurchaseInvoices.AsNoTracking()
            .Where(x => x.Status != "Cancelled" && x.Total > x.AmountPaid)
            .ToListAsync(cancellationToken);

        var items = rows.Select(x =>
        {
            var outstanding = x.Total - x.AmountPaid;
            var daysUntil = (x.DueDate.Date - today).Days;
            var overdue = daysUntil < 0;
            var priority = (overdue ? 1000 + Math.Abs(daysUntil) : Math.Max(0, settings.PayableAlertDays - daysUntil)) * 10
                           + (int)Math.Min(outstanding, 100000);
            return new PayableItemDto(
                x.Id, x.InvoiceNumber, x.SupplierId,
                suppliers.TryGetValue(x.SupplierId, out var name) ? name : "Supplier",
                x.IssueDate, x.DueDate, x.Total, x.AmountPaid, decimal.Round(outstanding, 2),
                daysUntil, overdue, x.Status, priority);
        })
        .OrderByDescending(x => x.PriorityScore)
        .ToList();

        var schedule = items
            .OrderBy(x => x.DueDate)
            .Take(20)
            .Select(x => new PaymentScheduleItemDto(
                x.PurchaseInvoiceId,
                x.InvoiceNumber,
                x.SupplierName,
                x.IsOverdue ? today : x.DueDate.Date,
                x.Outstanding,
                x.IsOverdue
                    ? "Overdue supplier bill — prioritize after confirming available cash."
                    : "Pay on or before due date. Agent will not transfer funds."))
            .ToList();

        return new PayablesIntelligenceDto(
            currency,
            decimal.Round(items.Sum(x => x.Outstanding), 2),
            decimal.Round(items.Where(x => x.IsOverdue).Sum(x => x.Outstanding), 2),
            decimal.Round(items.Where(x => !x.IsOverdue && x.DaysUntilDue <= settings.PayableAlertDays).Sum(x => x.Outstanding), 2),
            items,
            schedule,
            [
                "Payables are supplier PurchaseInvoice balances only. Open purchase orders are not treated as paid expenses or payables until billed.",
                "The agent never executes supplier payments without explicit human approval."
            ]);
    }

    public async Task<CashFlowIntelligenceDto> GetCashFlowAsync(DateTime? startDate = null, DateTime? endDate = null, int projectionDays = 30, CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.FinanceView, PermissionCodes.NalvaView);
        var ctx = await ResolveContextAsync(startDate, endDate, null, cancellationToken);
        var settings = await EnsureSettingsAsync(cancellationToken);
        var openingConfigured = await _db.TenantSettings.AsNoTracking().AnyAsync(cancellationToken);
        var opening = await _db.TenantSettings.AsNoTracking().Select(x => (decimal?)x.OpeningCashBalance).FirstOrDefaultAsync(cancellationToken);
        var otherIncomeInflows = await _db.IncomeRecords.AsNoTracking()
            .Where(x => x.OccurredOn >= ctx.Start && x.OccurredOn <= ctx.End && x.Status == "Approved")
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var inflows = await NetCollectedCashAsync(ctx.Start, ctx.End, cancellationToken) + otherIncomeInflows;
        var outflows = await RecognizedExpensesQuery(ctx.Start, ctx.End).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var cash = await ComputeCashBalanceAsync(ctx.End, cancellationToken);
        var ar = await GetReceivablesAsync(cancellationToken);
        var ap = await GetPayablesAsync(cancellationToken);
        var expectedIn = ar.Items.Where(x => x.DaysOverdue >= 0).Sum(x => x.OutstandingAmount);
        var expectedOut = ap.Items.Where(x => x.DaysUntilDue <= Math.Max(projectionDays, 1)).Sum(x => x.Outstanding);

        var historyMonths = await CountHistoryMonthsAsync(cancellationToken);
        var projectionAvailable = historyMonths >= settings.MinimumCashFlowHistoryMonths && cash.Available;
        var assumptions = new List<string>
        {
            "Historical inflows = completed payments net of refunds + approved income records.",
            "Historical outflows = approved/paid expenses only (not open POs).",
            "Projection assumes overdue receivables collect gradually and payables are paid near due dates.",
            "Projections are estimates, not guaranteed outcomes."
        };
        var limitations = new List<string>();
        if (!openingConfigured || opening is null)
            limitations.Add("Opening cash balance is missing or zero-configured; closing balance confidence is reduced.");
        if (!projectionAvailable)
            limitations.Add($"Cash-flow projection unavailable until at least {settings.MinimumCashFlowHistoryMonths} months of payment/expense history and a reliable cash basis exist (found {historyMonths} months).");

        var projection = new List<CashFlowProjectionPointDto>();
        var warnings = new List<string>();
        if (projectionAvailable)
        {
            var balance = cash.Value ?? 0m;
            var days = Math.Clamp(projectionDays, 7, 90);
            var dailyIn = expectedIn / days;
            var payablesByDay = ap.Items
                .GroupBy(x => (x.IsOverdue ? DateTime.UtcNow.Date : x.DueDate.Date))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Outstanding));

            for (var i = 1; i <= days; i++)
            {
                var date = DateTime.UtcNow.Date.AddDays(i);
                payablesByDay.TryGetValue(date, out var dayOut);
                var dayIn = dailyIn;
                balance = balance + dayIn - dayOut;
                var confidence = i <= 7 ? "medium" : "low";
                projection.Add(new CashFlowProjectionPointDto(date, decimal.Round(dayIn, 2), decimal.Round(dayOut, 2), decimal.Round(balance, 2), confidence));
                if (balance < 0 && warnings.Count < 5)
                    warnings.Add($"Potential cash shortage around {date:yyyy-MM-dd} if receivables collect slowly and payables are settled on due dates.");
            }
        }

        if (ar.OverdueTotal > 0)
            warnings.Add($"Delayed customer payments totaling {decimal.Round(ar.OverdueTotal, 2)} {ctx.Currency} may reduce near-term liquidity.");

        return new CashFlowIntelligenceDto(
            ctx.Currency, ctx.Start, ctx.End,
            openingConfigured,
            opening,
            decimal.Round(inflows, 2),
            decimal.Round(outflows, 2),
            cash.Available ? cash.Value : null,
            cash.Available,
            decimal.Round(expectedIn, 2),
            decimal.Round(expectedOut, 2),
            projectionAvailable,
            projectionAvailable ? "low-to-medium" : "unavailable",
            projection,
            warnings,
            assumptions,
            limitations,
            "REAL");
    }

    public async Task<ReconciliationSummaryDto> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.FinanceView, PermissionCodes.NalvaView);
        var issues = new List<ReconciliationIssueDto>();

        var invoices = await _db.Invoices.AsNoTracking()
            .Where(x => !ExcludedInvoiceStatuses.Contains(x.Status))
            .Select(x => new { x.Id, x.InvoiceNumber, x.OrderId, x.GrandTotal, x.AmountPaid, x.OutstandingAmount, x.Status })
            .ToListAsync(cancellationToken);

        var paymentsByOrder = await _db.Payments.AsNoTracking()
            .Where(x => x.Status == PaymentStatusNames.Completed || x.Status == PaymentStatusNames.Refunded)
            .GroupBy(x => x.OrderId)
            .Select(g => new { OrderId = g.Key, Net = g.Sum(x => x.Amount - x.RefundedAmount) })
            .ToDictionaryAsync(x => x.OrderId, x => x.Net, cancellationToken);

        foreach (var invoice in invoices)
        {
            paymentsByOrder.TryGetValue(invoice.OrderId, out var netPaid);
            var expectedOutstanding = Math.Max(0, invoice.GrandTotal - netPaid);
            if (Math.Abs(invoice.AmountPaid - netPaid) > 0.01m || Math.Abs(invoice.OutstandingAmount - expectedOutstanding) > 0.01m)
            {
                issues.Add(new ReconciliationIssueDto(
                    "invoice-payment-mismatch",
                    $"Invoice {invoice.InvoiceNumber} AmountPaid/Outstanding does not match net completed payments on the linked order.",
                    invoice.Id, null, null, null, null, invoice.OutstandingAmount,
                    "Review payments and recalculate invoice balances. Do not delete records to force a match."));
            }
            else if (invoice.AmountPaid > 0 && invoice.AmountPaid < invoice.GrandTotal)
            {
                issues.Add(new ReconciliationIssueDto(
                    "partial-payment",
                    $"Invoice {invoice.InvoiceNumber} is partially paid ({invoice.AmountPaid} of {invoice.GrandTotal}).",
                    invoice.Id, null, null, null, null, invoice.OutstandingAmount,
                    "Track remaining balance; prepare reminder only if overdue."));
            }
        }

        var bills = await _db.PurchaseInvoices.AsNoTracking()
            .Where(x => x.Status != "Cancelled")
            .ToListAsync(cancellationToken);
        foreach (var bill in bills)
        {
            if (bill.AmountPaid > bill.Total + 0.01m)
            {
                issues.Add(new ReconciliationIssueDto(
                    "purchase-bill-overpay",
                    $"Purchase bill {bill.InvoiceNumber} AmountPaid exceeds Total.",
                    null, null, bill.Id, null, null, bill.AmountPaid - bill.Total,
                    "Investigate duplicate supplier payments before any adjustment."));
            }
        }

        var paymentRows = await _db.Payments.AsNoTracking()
            .Where(x => x.Status == PaymentStatusNames.Completed)
            .Select(x => new { x.Id, x.Amount, x.PaymentDate, x.ReferenceNo, x.CustomerId, x.OrderId })
            .ToListAsync(cancellationToken);
        for (var i = 0; i < paymentRows.Count; i++)
        {
            for (var j = i + 1; j < paymentRows.Count; j++)
            {
                var a = paymentRows[i];
                var b = paymentRows[j];
                if (a.Amount != b.Amount || a.CustomerId != b.CustomerId) continue;
                if (Math.Abs((a.PaymentDate - b.PaymentDate).TotalHours) > 48) continue;
                if (!string.IsNullOrWhiteSpace(a.ReferenceNo) && a.ReferenceNo == b.ReferenceNo || a.OrderId == b.OrderId)
                {
                    issues.Add(new ReconciliationIssueDto(
                        "possible-duplicate-payment",
                        $"Payments {a.Id} and {b.Id} look similar (amount/customer/date).",
                        null, a.Id, null, null, null, a.Amount,
                        "Confirm whether one payment should be refunded or marked reconciled. No silent deletion."));
                }
            }
        }

        var unmatchedBank = await _db.FinanceBankStatementLines.AsNoTracking().CountAsync(x => x.MatchStatus == "Unmatched", cancellationToken);
        foreach (var line in await _db.FinanceBankStatementLines.AsNoTracking().Where(x => x.MatchStatus == "Unmatched").OrderByDescending(x => x.TransactionDate).Take(20).ToListAsync(cancellationToken))
        {
            issues.Add(new ReconciliationIssueDto(
                "unmatched-bank-line",
                $"Imported bank line '{line.Description}' on {line.TransactionDate:yyyy-MM-dd} is unmatched.",
                null, null, null, null, line.Id, line.Amount,
                "Match manually to a payment/expense or mark ignored after review."));
        }

        return new ReconciliationSummaryDto(
            issues.Count(x => x.Kind == "invoice-payment-mismatch"),
            issues.Count(x => x.Kind is "purchase-bill-overpay"),
            issues.Count(x => x.Kind == "possible-duplicate-payment"),
            unmatchedBank,
            issues.Count(x => x.Kind == "partial-payment"),
            issues.Take(100).ToList(),
            [
                "Automatic bank synchronization is UNAVAILABLE. CSV import is supported for authorized statements.",
                "Reconciliation never silently modifies or deletes financial records."
            ],
            "UNAVAILABLE");
    }

    public async Task<BankImportResultDto> ImportBankStatementCsvAsync(BankStatementImportRequest request, CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.FinanceView, PermissionCodes.NalvaManage);
        if (string.IsNullOrWhiteSpace(request.CsvContent))
            throw new BadRequestException("CSV content is required.");

        var batch = new FinanceBankImportBatch
        {
            TenantId = CompanyId,
            FileName = string.IsNullOrWhiteSpace(NalvaGuard.Clip(request.FileName, 200)) ? "statement.csv" : NalvaGuard.Clip(request.FileName, 200),
            Status = "Imported",
            Classification = "REAL",
            Notes = NalvaGuard.Clip(request.Notes, 1000)
        };
        _db.FinanceBankImportBatches.Add(batch);

        var lines = ParseCsv(request.CsvContent);
        var existing = await _db.FinanceBankStatementLines.AsNoTracking()
            .Select(x => x.Fingerprint)
            .ToListAsync(cancellationToken);
        var existingSet = existing.ToHashSet(StringComparer.Ordinal);
        var imported = 0;
        var duplicates = 0;

        foreach (var line in lines)
        {
            var fp = Sha256($"{line.Date:yyyy-MM-dd}|{line.Amount}|{line.Description}|{line.Reference}");
            if (existingSet.Contains(fp))
            {
                duplicates++;
                continue;
            }

            existingSet.Add(fp);
            _db.FinanceBankStatementLines.Add(new FinanceBankStatementLine
            {
                TenantId = CompanyId,
                ImportBatchId = batch.Id,
                TransactionDate = line.Date,
                Description = NalvaGuard.Clip(line.Description, 500) ?? "Imported",
                Amount = Math.Abs(line.Amount),
                Direction = line.Amount >= 0 ? "inflow" : "outflow",
                Reference = NalvaGuard.Clip(line.Reference, 120),
                Fingerprint = fp,
                MatchStatus = "Unmatched"
            });
            imported++;
        }

        batch.LineCount = imported;
        batch.DuplicateCount = duplicates;
        await _db.SaveChangesAsync(cancellationToken);
        await TrackActivityAsync("bank-import", "Bank statement imported", $"Imported {imported} lines; skipped {duplicates} duplicates. Live bank sync remains UNAVAILABLE.", "info", cancellationToken);

        return new BankImportResultDto(batch.Id, imported, duplicates, "REAL",
            $"Imported {imported} authorized statement lines. Duplicate fingerprints skipped: {duplicates}. Automatic bank sync: UNAVAILABLE.");
    }

    public async Task<FinanceReportDto> GenerateReportAsync(FinanceReportRequest request, CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.FinanceView, PermissionCodes.ReportView, PermissionCodes.NalvaView);
        var type = (request.ReportType ?? "daily-summary").Trim().ToLowerInvariant();
        var intel = await GetIntelligenceAsync(request.StartDate, request.EndDate, request.Period, cancellationToken);
        var disclaimer = "This report is generated from BusinessOS records for operational use. It is not a certified accounting, audit, tax, or legal opinion.";

        object payload;
        var body = new StringBuilder();
        body.AppendLine($"# Finance report: {type}");
        body.AppendLine();
        body.AppendLine($"Period: {intel.StartDate:yyyy-MM-dd} → {intel.EndDate:yyyy-MM-dd} ({intel.TimeZoneId})");
        body.AppendLine($"Currency: {intel.Currency}");
        body.AppendLine();

        switch (type)
        {
            case "weekly-summary":
            case "daily-summary":
            case "monthly-profit-loss":
            case "profit-loss":
                payload = intel;
                body.AppendLine($"- Sales revenue (orders): {FormatMetric(intel.SalesRevenue)}");
                body.AppendLine($"- Collected cash: {FormatMetric(intel.CollectedCash)}");
                body.AppendLine($"- Expenses: {FormatMetric(intel.TotalExpenses)}");
                body.AppendLine($"- Gross profit: {FormatMetric(intel.GrossProfit)}");
                body.AppendLine($"- Net profit: {FormatMetric(intel.NetProfit)}");
                break;
            case "receivables":
            case "customer-receivables":
                var ar = await GetReceivablesAsync(cancellationToken);
                payload = ar;
                body.AppendLine($"- Outstanding: {ar.TotalOutstanding} {ar.Currency}");
                body.AppendLine($"- Overdue: {ar.OverdueTotal} ({ar.OverdueCount} invoices)");
                break;
            case "payables":
            case "supplier-payables":
                var ap = await GetPayablesAsync(cancellationToken);
                payload = ap;
                body.AppendLine($"- Outstanding: {ap.TotalOutstanding} {ap.Currency}");
                body.AppendLine($"- Overdue: {ap.OverdueTotal}");
                break;
            case "cash-flow":
                var cf = await GetCashFlowAsync(request.StartDate, request.EndDate, 30, cancellationToken);
                payload = cf;
                body.AppendLine($"- Inflows: {cf.Inflows}");
                body.AppendLine($"- Outflows: {cf.Outflows}");
                body.AppendLine($"- Projection available: {cf.ProjectionAvailable} ({cf.ProjectionConfidence})");
                break;
            case "expense-analysis":
            case "expenses":
                var ex = await AnalyzeExpensesAsync(request.StartDate, request.EndDate, request.Period, cancellationToken);
                payload = ex;
                body.AppendLine($"- Recognized expenses: {ex.TotalRecognizedExpenses}");
                body.AppendLine($"- Anomalies: {ex.Anomalies.Count}");
                break;
            case "product-profitability":
                payload = intel.ProfitabilityByProduct;
                foreach (var row in intel.ProfitabilityByProduct.Take(20))
                    body.AppendLine($"- {row.Name}: revenue {row.Revenue}, gross {row.GrossProfit?.ToString() ?? "n/a"} ({row.Confidence})");
                break;
            case "period-comparison":
                var prior = await GetIntelligenceAsync(
                    intel.StartDate - (intel.EndDate - intel.StartDate),
                    intel.StartDate.AddTicks(-1),
                    null,
                    cancellationToken);
                payload = new { current = intel, prior };
                body.AppendLine($"- Current collected cash: {FormatMetric(intel.CollectedCash)}");
                body.AppendLine($"- Prior collected cash: {FormatMetric(prior.CollectedCash)}");
                break;
            default:
                payload = intel;
                body.AppendLine("Defaulted to financial intelligence summary.");
                break;
        }

        body.AppendLine();
        body.AppendLine("## Limitations");
        foreach (var limit in intel.Limitations)
            body.AppendLine($"- {limit}");
        body.AppendLine();
        body.AppendLine(disclaimer);

        return new FinanceReportDto(
            type,
            intel.Currency,
            intel.TimeZoneId,
            intel.StartDate,
            intel.EndDate,
            "hybrid: sales revenue from completed orders; cash from completed payments net of refunds; expenses from Approved/Paid only",
            body.ToString(),
            payload,
            intel.Limitations,
            disclaimer);
    }

    public async Task<IReadOnlyList<FinanceBudgetDto>> GetBudgetsAsync(CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.FinanceView, PermissionCodes.NalvaView);
        var budgets = await _db.FinanceBudgets.AsNoTracking().OrderByDescending(x => x.PeriodStart).ToListAsync(cancellationToken);
        if (budgets.Count == 0)
            return Array.Empty<FinanceBudgetDto>();

        // One expense scan for the union of budget windows, then allocate in memory (avoids 1+N Sum queries).
        var windowStart = budgets.Min(x => x.PeriodStart);
        var windowEnd = budgets.Max(x => x.PeriodEnd);
        var expenses = await RecognizedExpensesQuery(windowStart, windowEnd)
            .Select(x => new
            {
                x.Amount,
                x.ExpenseDate,
                x.ExpenseCategoryId,
                CategoryName = x.ExpenseCategory.Name
            })
            .ToListAsync(cancellationToken);

        return budgets.Select(budget =>
        {
            var actual = expenses
                .Where(x =>
                    x.ExpenseDate >= budget.PeriodStart &&
                    x.ExpenseDate <= budget.PeriodEnd &&
                    (budget.ExpenseCategoryId is Guid categoryId
                        ? x.ExpenseCategoryId == categoryId
                        : string.IsNullOrWhiteSpace(budget.CategoryName)
                            || string.Equals(x.CategoryName, budget.CategoryName, StringComparison.OrdinalIgnoreCase)))
                .Sum(x => x.Amount);
            var remaining = budget.Amount - actual;
            var utilization = budget.Amount <= 0 ? 0 : decimal.Round(actual / budget.Amount * 100m, 2);
            return new FinanceBudgetDto(
                budget.Id, budget.Name, budget.ExpenseCategoryId, budget.CategoryName, budget.Amount, budget.Currency,
                budget.PeriodStart, budget.PeriodEnd, decimal.Round(actual, 2), decimal.Round(remaining, 2), utilization,
                actual > budget.Amount, budget.IsRecommendation, budget.Status);
        }).ToList();
    }

    public async Task<FinanceBudgetDto> UpsertBudgetAsync(FinanceBudgetWriteRequest request, CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.FinanceView, PermissionCodes.NalvaManage);
        if (request.Amount < 0)
            throw new BadRequestException("Budget amount cannot be negative.");
        if (request.PeriodEnd < request.PeriodStart)
            throw new BadRequestException("Budget period end must be on or after start.");

        FinanceBudget entity;
        if (request.Id is Guid id)
        {
            entity = await _db.FinanceBudgets.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                     ?? throw new NotFoundException("Budget not found.");
        }
        else
        {
            entity = new FinanceBudget { TenantId = CompanyId };
            _db.FinanceBudgets.Add(entity);
        }

        entity.Name = request.Name.Trim();
        entity.ExpenseCategoryId = request.ExpenseCategoryId;
        entity.CategoryName = request.CategoryName?.Trim();
        entity.Amount = decimal.Round(request.Amount, 2);
        entity.PeriodStart = DateTime.SpecifyKind(request.PeriodStart, DateTimeKind.Utc);
        entity.PeriodEnd = DateTime.SpecifyKind(request.PeriodEnd, DateTimeKind.Utc);
        entity.IsRecommendation = request.IsRecommendation;
        entity.Source = request.IsRecommendation ? "recommended" : "owner";
        entity.Currency = await GetCurrencyAsync(cancellationToken);
        entity.Status = "Active";
        await _db.SaveChangesAsync(cancellationToken);
        return await MapBudgetAsync(entity, cancellationToken);
    }

    public async Task<IReadOnlyList<BudgetAlertDto>> GetBudgetAlertsAsync(CancellationToken cancellationToken = default)
    {
        var budgets = await GetBudgetsAsync(cancellationToken);
        return budgets
            .Where(x => x.UtilizationPercent >= 80)
            .Select(x => new BudgetAlertDto(
                x.Id,
                x.Name,
                x.IsOverBudget ? "high" : "medium",
                x.IsOverBudget
                    ? $"Budget '{x.Name}' is over by {decimal.Round(x.ActualSpend - x.Amount, 2)}."
                    : $"Budget '{x.Name}' is at {x.UtilizationPercent}% utilization.",
                x.Amount,
                x.ActualSpend,
                x.UtilizationPercent))
            .ToList();
    }

    public async Task<IReadOnlyList<FinanceAlertDto>> GetAlertsAsync(CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.FinanceView, PermissionCodes.NalvaView);
        return await _db.FinanceAgentActivities.AsNoTracking()
            .Where(x => x.Status == "Open" && (x.Severity == "high" || x.Severity == "medium" || x.Kind.Contains("alert")))
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .Select(x => new FinanceAlertDto(x.Id, x.Kind, x.Title, x.Summary, x.Severity, x.Status, x.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<PreparedReminderDto> PreparePaymentReminderAsync(PrepareReminderRequest request, CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.FinanceView, PermissionCodes.InvoiceView, PermissionCodes.NalvaView);
        var settings = await EnsureSettingsAsync(cancellationToken);
        var invoice = await _db.Invoices.Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == request.InvoiceId, cancellationToken)
            ?? throw new NotFoundException("Invoice not found.");

        if (invoice.OutstandingAmount <= 0 || ExcludedInvoiceStatuses.Contains(invoice.Status))
            throw new BadRequestException("Invoice has no outstanding balance to remind about.");

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await _db.FinancePaymentReminders
                .FirstOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return new PreparedReminderDto(
                    existing.Id, existing.InvoiceId, existing.Status, existing.ApprovalFingerprint,
                    RequiresApproval: true, Created: false, DuplicatePrevented: true,
                    existing.Subject, existing.Body, existing.Channel, existing.OutstandingAmount, existing.DaysOverdue,
                    "Duplicate reminder prevented by idempotency key.",
                    "BLOCKED");
            }
        }

        // Prevent duplicate active reminders for same invoice within 24h.
        var recent = await _db.FinancePaymentReminders.AsNoTracking()
            .AnyAsync(x => x.InvoiceId == invoice.Id
                           && x.Status != "Cancelled"
                           && x.Status != "Failed"
                           && x.CreatedAt >= DateTime.UtcNow.AddHours(-24), cancellationToken);
        if (recent)
        {
            return new PreparedReminderDto(
                Guid.Empty, invoice.Id, "SkippedDuplicate", "", true, false, true,
                "", "", settings.ReminderChannel, invoice.OutstandingAmount, 0,
                "A reminder for this invoice was already prepared in the last 24 hours.",
                "UNAVAILABLE");
        }

        var daysOverdue = Math.Max(0, (DateTime.UtcNow.Date - invoice.DueDate.Date).Days);
        var customerName = $"{invoice.Customer.FirstName} {invoice.Customer.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(customerName))
            customerName = invoice.Customer.Company ?? "Customer";
        var channel = string.IsNullOrWhiteSpace(request.Channel) ? settings.ReminderChannel : request.Channel.Trim();
        var subject = $"Payment reminder: invoice {invoice.InvoiceNumber}";
        var body = string.IsNullOrWhiteSpace(request.CustomMessage)
            ? $"Hello {customerName},\n\nThis is a friendly reminder that invoice {invoice.InvoiceNumber} has an outstanding balance of {invoice.OutstandingAmount:0.00}. Due date: {invoice.DueDate:yyyy-MM-dd}. Days overdue: {daysOverdue}.\n\nPlease arrange payment at your earliest convenience.\n\nThank you."
            : request.CustomMessage.Trim();
        body = NalvaGuard.Clip(body, 4000);

        var fingerprint = Sha256($"{invoice.Id}|{invoice.OutstandingAmount:0.00}|{channel}|{subject}|{body}");
        var reminder = new FinancePaymentReminder
        {
            TenantId = CompanyId,
            InvoiceId = invoice.Id,
            CustomerId = invoice.CustomerId,
            Channel = channel,
            Status = "Draft",
            Subject = subject,
            Body = body,
            OutstandingAmount = invoice.OutstandingAmount,
            DaysOverdue = daysOverdue,
            ApprovalFingerprint = fingerprint,
            IdempotencyKey = request.IdempotencyKey
        };
        _db.FinancePaymentReminders.Add(reminder);
        await _db.SaveChangesAsync(cancellationToken);
        await TrackActivityAsync("reminder-prepared", "Payment reminder drafted",
            $"Drafted reminder for invoice {invoice.InvoiceNumber} ({invoice.OutstandingAmount}). External send requires approval.",
            "info", cancellationToken, relatedInvoiceId: invoice.Id);

        return new PreparedReminderDto(
            reminder.Id, reminder.InvoiceId, reminder.Status, reminder.ApprovalFingerprint,
            RequiresApproval: true, Created: true, DuplicatePrevented: false,
            reminder.Subject, reminder.Body, reminder.Channel, reminder.OutstandingAmount, reminder.DaysOverdue,
            "Reminder drafted. Sending requires explicit approval and a connected communication channel.",
            "BLOCKED");
    }

    public async Task<PreparedReminderDto> SubmitPaymentReminderAsync(Guid reminderId, bool approved, string? approvalFingerprint, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaApprove);
        if (!approved)
            throw new BadRequestException("Reminder send was not approved.");

        var reminder = await _db.FinancePaymentReminders.FirstOrDefaultAsync(x => x.Id == reminderId, cancellationToken)
                       ?? throw new NotFoundException("Reminder not found.");

        if (!string.IsNullOrWhiteSpace(approvalFingerprint)
            && !string.Equals(approvalFingerprint, reminder.ApprovalFingerprint, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Approval fingerprint mismatch. Material reminder details changed — prepare again.");

        var invoice = await _db.Invoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == reminder.InvoiceId, cancellationToken);
        if (invoice is null || invoice.OutstandingAmount <= 0)
        {
            reminder.Status = "Cancelled";
            reminder.FailureReason = "Invoice no longer has an outstanding balance.";
            await _db.SaveChangesAsync(cancellationToken);
            return new PreparedReminderDto(reminder.Id, reminder.InvoiceId, reminder.Status, reminder.ApprovalFingerprint,
                false, false, false, reminder.Subject, reminder.Body, reminder.Channel, reminder.OutstandingAmount, reminder.DaysOverdue,
                "Cancelled because the invoice is no longer outstanding.", "UNAVAILABLE");
        }

        // Re-bind fingerprint to current outstanding amount.
        var expected = Sha256($"{invoice.Id}|{invoice.OutstandingAmount:0.00}|{reminder.Channel}|{reminder.Subject}|{reminder.Body}");
        if (!string.Equals(expected, reminder.ApprovalFingerprint, StringComparison.OrdinalIgnoreCase)
            || Math.Abs(invoice.OutstandingAmount - reminder.OutstandingAmount) > 0.01m)
            throw new BadRequestException("Invoice balance changed since the reminder was prepared. New approval is required.");

        // No live email/SMS finance channel is wired for autonomous send in Phase 5E.
        reminder.Status = "Failed";
        reminder.FailureReason = "No authorized finance reminder integration is connected. Reminder remains prepared only.";
        await _db.SaveChangesAsync(cancellationToken);
        await TrackActivityAsync("reminder-blocked", "Reminder send blocked",
            "Approval recorded, but external reminder delivery is BLOCKED until a communication integration is connected.",
            "medium", cancellationToken, relatedInvoiceId: reminder.InvoiceId);

        return new PreparedReminderDto(
            reminder.Id, reminder.InvoiceId, reminder.Status, reminder.ApprovalFingerprint,
            RequiresApproval: false, Created: false, DuplicatePrevented: false,
            reminder.Subject, reminder.Body, reminder.Channel, reminder.OutstandingAmount, reminder.DaysOverdue,
            "Approved locally, but external delivery is BLOCKED (no connected reminder channel).",
            "BLOCKED");
    }

    public async Task<IReadOnlyList<FinanceReminderDto>> GetRemindersAsync(CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.FinanceView, PermissionCodes.NalvaView);
        var rows = await _db.FinancePaymentReminders.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .Select(x => new
            {
                x.Id,
                x.InvoiceId,
                InvoiceNumber = x.Invoice!.InvoiceNumber,
                x.CustomerId,
                CustomerName = ((x.Customer!.FirstName ?? "") + " " + (x.Customer.LastName ?? "")).Trim(),
                x.Channel,
                x.Status,
                x.Subject,
                x.OutstandingAmount,
                x.DaysOverdue,
                x.CreatedAt,
                x.SentAt
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new FinanceReminderDto(
            x.Id, x.InvoiceId, x.InvoiceNumber, x.CustomerId,
            string.IsNullOrWhiteSpace(x.CustomerName) ? "Customer" : x.CustomerName,
            x.Channel, x.Status, x.Subject, x.OutstandingAmount, x.DaysOverdue, x.CreatedAt, x.SentAt)).ToList();
    }

    public async Task<FinanceAutomationSettingsDto> GetAutomationSettingsAsync(CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.FinanceView, PermissionCodes.NalvaView, PermissionCodes.NalvaManage);
        var s = await EnsureSettingsAsync(cancellationToken);
        return MapSettings(s);
    }

    public async Task<FinanceAutomationSettingsDto> SaveAutomationSettingsAsync(FinanceAutomationSettingsWriteRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        var s = await EnsureSettingsAsync(cancellationToken);
        if (request.DailyFinanceSummary is bool v1) s.DailyFinanceSummary = v1;
        if (request.WeeklyFinancialReport is bool v2) s.WeeklyFinancialReport = v2;
        if (request.OverdueInvoiceMonitoring is bool v3) s.OverdueInvoiceMonitoring = v3;
        if (request.PaymentReminderPreparation is bool v4) s.PaymentReminderPreparation = v4;
        if (request.UpcomingPayableAlerts is bool v5) s.UpcomingPayableAlerts = v5;
        if (request.ExpenseAnomalyDetection is bool v6) s.ExpenseAnomalyDetection = v6;
        if (request.CashFlowAnalysis is bool v7) s.CashFlowAnalysis = v7;
        if (request.BudgetMonitoring is bool v8) s.BudgetMonitoring = v8;
        if (request.ReconciliationChecks is bool v9) s.ReconciliationChecks = v9;
        if (!string.IsNullOrWhiteSpace(request.ReminderScheduleDays)) s.ReminderScheduleDays = request.ReminderScheduleDays.Trim();
        if (!string.IsNullOrWhiteSpace(request.ReminderChannel)) s.ReminderChannel = request.ReminderChannel.Trim();
        if (request.AutoSendReminders is bool v10) s.AutoSendReminders = v10;
        if (request.AnomalyVariancePercent is decimal v11) s.AnomalyVariancePercent = Math.Clamp(v11, 5m, 500m);
        if (request.PayableAlertDays is int v12) s.PayableAlertDays = Math.Clamp(v12, 1, 90);
        if (request.MinimumCashFlowHistoryMonths is int v13) s.MinimumCashFlowHistoryMonths = Math.Clamp(v13, 1, 24);
        if (!string.IsNullOrWhiteSpace(request.TimeZoneId)) s.TimeZoneId = request.TimeZoneId.Trim();
        if (!string.IsNullOrWhiteSpace(request.ReportingBasis)) s.ReportingBasis = request.ReportingBasis.Trim();
        await _db.SaveChangesAsync(cancellationToken);
        return MapSettings(s);
    }

    public async Task<IReadOnlyList<FinanceActivityDto>> GetActivityAsync(CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.FinanceView, PermissionCodes.NalvaView);
        return await _db.FinanceAgentActivities.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .Select(x => new FinanceActivityDto(x.Id, x.Kind, x.Title, x.Summary, x.Severity, x.Status, x.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task RunScheduledMonitorAsync(CancellationToken cancellationToken = default)
    {
        // Hosted processors may run without an interactive user; still require tenant context.
        if (!_tenant.HasTenant())
            return;

        var settings = await EnsureSettingsAsync(cancellationToken);
        try
        {
            if (settings.OverdueInvoiceMonitoring)
            {
                var ar = await GetReceivablesAsync(cancellationToken);
                if (ar.OverdueCount > 0)
                {
                    await TrackActivityAsync("alert-overdue-receivables", "Overdue receivables",
                        $"{ar.OverdueCount} overdue invoices totaling {ar.OverdueTotal} {ar.Currency}.",
                        "high", cancellationToken);
                }

                if (settings.PaymentReminderPreparation)
                {
                    foreach (var item in ar.Items.Where(x => x.DaysOverdue > 0).OrderByDescending(x => x.PriorityScore).Take(5))
                    {
                        await PreparePaymentReminderAsync(new PrepareReminderRequest
                        {
                            InvoiceId = item.InvoiceId,
                            IdempotencyKey = $"auto-reminder:{item.InvoiceId}:{DateTime.UtcNow:yyyyMMdd}"
                        }, cancellationToken);
                    }
                }
            }

            if (settings.UpcomingPayableAlerts)
            {
                var ap = await GetPayablesAsync(cancellationToken);
                if (ap.DueSoonTotal > 0 || ap.OverdueTotal > 0)
                {
                    await TrackActivityAsync("alert-payables", "Payable obligations",
                        $"Overdue {ap.OverdueTotal}; due soon {ap.DueSoonTotal} {ap.Currency}.",
                        ap.OverdueTotal > 0 ? "high" : "medium", cancellationToken);
                }
            }

            if (settings.ExpenseAnomalyDetection)
            {
                var expenses = await AnalyzeExpensesAsync(period: "month", cancellationToken: cancellationToken);
                foreach (var anomaly in expenses.Anomalies.Take(5))
                {
                    await TrackActivityAsync("alert-expense-anomaly", "Unusual expense",
                        anomaly.Explanation, "medium", cancellationToken, relatedExpenseId: anomaly.ExpenseId);
                }
            }

            if (settings.BudgetMonitoring)
            {
                foreach (var alert in await GetBudgetAlertsAsync(cancellationToken))
                {
                    await TrackActivityAsync("alert-budget", alert.BudgetName, alert.Message, alert.Severity, cancellationToken, relatedBudgetId: alert.BudgetId);
                }
            }

            if (settings.CashFlowAnalysis)
            {
                var cf = await GetCashFlowAsync(projectionDays: 14, cancellationToken: cancellationToken);
                foreach (var warning in cf.ShortageWarnings.Take(3))
                    await TrackActivityAsync("alert-cashflow", "Cash-flow risk", warning, "high", cancellationToken);
            }

            if (settings.ReconciliationChecks)
            {
                var recon = await ReconcileAsync(cancellationToken);
                if (recon.InvoicePaymentMismatches + recon.PossibleDuplicatePayments > 0)
                {
                    await TrackActivityAsync("alert-reconciliation", "Reconciliation issues",
                        $"{recon.InvoicePaymentMismatches} invoice mismatches, {recon.PossibleDuplicatePayments} possible duplicate payments.",
                        "medium", cancellationToken);
                }
            }

            if (settings.DailyFinanceSummary)
            {
                var overview = await GetOverviewAsync(period: "day", cancellationToken: cancellationToken);
                await TrackActivityAsync("daily-summary", "Daily finance summary",
                    $"Collected cash {overview.CollectedCash}; expenses {overview.TotalExpenses}; overdue invoices {overview.OverdueInvoiceCount}.",
                    "info", cancellationToken);
            }

            settings.LastMonitorRunAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Finance agent monitor failed for tenant {TenantId}", CompanyId);
            throw;
        }
    }

    // ---- helpers ----

    private Guid CompanyId => _tenant.TenantId;

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
        throw new ForbiddenException($"Missing permission {string.Join(" or ", permissions)}.");
    }

    private async Task<FinanceAgentSettings> EnsureSettingsAsync(CancellationToken ct)
    {
        var settings = await _db.FinanceAgentSettings.FirstOrDefaultAsync(ct);
        if (settings is not null)
            return settings;

        var tz = await _db.Tenants.AsNoTracking().Select(x => x.Timezone).FirstOrDefaultAsync(ct) ?? "UTC";
        settings = new FinanceAgentSettings
        {
            TenantId = CompanyId,
            TimeZoneId = string.IsNullOrWhiteSpace(tz) ? "UTC" : tz
        };
        _db.FinanceAgentSettings.Add(settings);
        await _db.SaveChangesAsync(ct);
        return settings;
    }

    private static FinanceAutomationSettingsDto MapSettings(FinanceAgentSettings s) =>
        new(s.DailyFinanceSummary, s.WeeklyFinancialReport, s.OverdueInvoiceMonitoring, s.PaymentReminderPreparation,
            s.UpcomingPayableAlerts, s.ExpenseAnomalyDetection, s.CashFlowAnalysis, s.BudgetMonitoring, s.ReconciliationChecks,
            s.ReminderScheduleDays, s.ReminderChannel, s.AutoSendReminders, s.AnomalyVariancePercent, s.PayableAlertDays,
            s.MinimumCashFlowHistoryMonths, s.TimeZoneId, s.ReportingBasis, s.UpdatedAt, s.LastMonitorRunAt);

    private async Task<string> GetCurrencyAsync(CancellationToken ct)
    {
        var currency = await _db.Tenants.AsNoTracking().Select(x => x.Currency).FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();
    }

    private IQueryable<Expense> RecognizedExpensesQuery(DateTime start, DateTime end) =>
        _db.Expenses.AsNoTracking()
            .Where(x => x.ExpenseDate >= start && x.ExpenseDate <= end && RecognizedExpenseStatuses.Contains(x.Status));

    private async Task<decimal> NetCollectedCashAsync(DateTime start, DateTime end, CancellationToken ct) =>
        await _db.Payments.AsNoTracking()
            .Where(x => x.PaymentDate >= start && x.PaymentDate <= end && x.Status == PaymentStatusNames.Completed)
            .SumAsync(x => (decimal?)(x.Amount - x.RefundedAmount), ct) ?? 0m;

    private async Task<decimal> OutstandingReceivablesTotalAsync(CancellationToken ct) =>
        await _db.Invoices.AsNoTracking()
            .Where(x => x.OutstandingAmount > 0 && !ExcludedInvoiceStatuses.Contains(x.Status))
            .SumAsync(x => (decimal?)x.OutstandingAmount, ct) ?? 0m;

    private async Task<decimal> OverdueReceivablesTotalAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        return await _db.Invoices.AsNoTracking()
            .Where(x => x.OutstandingAmount > 0 && !ExcludedInvoiceStatuses.Contains(x.Status) && x.DueDate < today)
            .SumAsync(x => (decimal?)x.OutstandingAmount, ct) ?? 0m;
    }

    private async Task<decimal> OutstandingPayablesTotalAsync(CancellationToken ct) =>
        await _db.PurchaseInvoices.AsNoTracking()
            .Where(x => x.Status != "Cancelled" && x.Total > x.AmountPaid)
            .SumAsync(x => (decimal?)(x.Total - x.AmountPaid), ct) ?? 0m;

    private async Task<int> CountOverdueInvoicesAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        return await _db.Invoices.AsNoTracking()
            .CountAsync(x => x.OutstandingAmount > 0 && !ExcludedInvoiceStatuses.Contains(x.Status) && x.DueDate < today, ct);
    }

    private async Task<(bool Available, decimal? Value, string? Limitation)> ComputeCashBalanceAsync(DateTime asOf, CancellationToken ct)
    {
        var settings = await _db.TenantSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is null)
            return (false, null, "Cash balance unavailable: tenant financial settings (opening cash balance) are not configured.");

        var opening = settings.OpeningCashBalance;
        var payments = await _db.Payments.AsNoTracking()
            .Where(x => x.PaymentDate <= asOf && x.Status == PaymentStatusNames.Completed)
            .SumAsync(x => (decimal?)(x.Amount - x.RefundedAmount), ct) ?? 0m;
        var income = await _db.IncomeRecords.AsNoTracking()
            .Where(x => x.OccurredOn <= asOf && x.Status == "Approved")
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        var expenses = await _db.Expenses.AsNoTracking()
            .Where(x => x.ExpenseDate <= asOf && RecognizedExpenseStatuses.Contains(x.Status))
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;

        var balance = decimal.Round(opening + payments + income - expenses, 2);
        string? limitation = null;
        if (opening == 0 && payments == 0 && income == 0 && expenses == 0)
            limitation = "Opening balance is 0 and no cash movements exist yet — treat with caution.";
        return (true, balance, limitation);
    }

    private async Task<int> CountHistoryMonthsAsync(CancellationToken ct)
    {
        var paymentMonths = await _db.Payments.AsNoTracking()
            .Select(x => new { x.PaymentDate.Year, x.PaymentDate.Month })
            .Distinct()
            .CountAsync(ct);
        var expenseMonths = await _db.Expenses.AsNoTracking()
            .Select(x => new { x.ExpenseDate.Year, x.ExpenseDate.Month })
            .Distinct()
            .CountAsync(ct);
        return Math.Max(paymentMonths, expenseMonths);
    }

    private async Task<IReadOnlyList<TrendPointDto>> BuildMonthlyTrendsAsync(ReportContext ctx, CancellationToken ct)
    {
        var start = new DateTime(ctx.End.Year, ctx.End.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-11);
        var orders = await _db.Orders.AsNoTracking()
            .Where(x => x.OrderDate >= start && x.OrderDate <= ctx.End && x.Status == OrderStatusNames.Completed)
            .Select(x => new { x.OrderDate, x.GrandTotal })
            .ToListAsync(ct);
        var payments = await _db.Payments.AsNoTracking()
            .Where(x => x.PaymentDate >= start && x.PaymentDate <= ctx.End && x.Status == PaymentStatusNames.Completed)
            .Select(x => new { x.PaymentDate, Net = x.Amount - x.RefundedAmount })
            .ToListAsync(ct);
        var expenses = await RecognizedExpensesQuery(start, ctx.End)
            .Select(x => new { x.ExpenseDate, x.Amount })
            .ToListAsync(ct);
        var cogs = await (
            from item in _db.OrderItems.AsNoTracking()
            join order in _db.Orders.AsNoTracking() on item.OrderId equals order.Id
            where order.OrderDate >= start && order.OrderDate <= ctx.End && order.Status == OrderStatusNames.Completed
            select new { order.OrderDate, Cost = item.UnitCost * item.Quantity }
        ).ToListAsync(ct);

        var points = new List<TrendPointDto>();
        for (var i = 0; i < 12; i++)
        {
            var monthStart = start.AddMonths(i);
            var monthEnd = monthStart.AddMonths(1).AddTicks(-1);
            var rev = orders.Where(x => x.OrderDate >= monthStart && x.OrderDate <= monthEnd).Sum(x => x.GrandTotal);
            var cash = payments.Where(x => x.PaymentDate >= monthStart && x.PaymentDate <= monthEnd).Sum(x => x.Net);
            var exp = expenses.Where(x => x.ExpenseDate >= monthStart && x.ExpenseDate <= monthEnd).Sum(x => x.Amount);
            var cost = cogs.Where(x => x.OrderDate >= monthStart && x.OrderDate <= monthEnd).Sum(x => x.Cost);
            var hasCogs = cogs.Any(x => x.OrderDate >= monthStart && x.OrderDate <= monthEnd && x.Cost > 0);
            decimal? gross = hasCogs ? rev - cost : null;
            decimal? net = gross.HasValue ? gross - exp : null;
            points.Add(new TrendPointDto(
                $"{monthStart:yyyy-MM}",
                decimal.Round(rev, 2),
                decimal.Round(cash, 2),
                decimal.Round(exp, 2),
                gross.HasValue ? decimal.Round(gross.Value, 2) : null,
                net.HasValue ? decimal.Round(net.Value, 2) : null));
        }

        return points;
    }

    private async Task<IReadOnlyList<ProfitabilityItemDto>> BuildProductProfitabilityAsync(ReportContext ctx, CancellationToken ct)
    {
        var grouped = await (
            from item in _db.OrderItems.AsNoTracking()
            join order in _db.Orders.AsNoTracking() on item.OrderId equals order.Id
            join product in _db.Products.AsNoTracking() on item.ProductId equals product.Id
            where order.OrderDate >= ctx.Start && order.OrderDate <= ctx.End && order.Status == OrderStatusNames.Completed
            group item by new { item.ProductId, product.Name } into g
            select new
            {
                g.Key.ProductId,
                g.Key.Name,
                Revenue = g.Sum(x => x.UnitPrice * x.Quantity),
                Cost = g.Sum(x => x.UnitCost * x.Quantity),
                HasCost = g.Sum(x => x.UnitCost > 0 ? 1 : 0) > 0
            }
        )
        .OrderByDescending(x => x.Revenue)
        .Take(50)
        .ToListAsync(ct);

        return grouped
            .Select(g => new ProfitabilityItemDto(
                g.ProductId,
                g.Name,
                decimal.Round(g.Revenue, 2),
                g.HasCost ? decimal.Round(g.Cost, 2) : null,
                g.HasCost ? decimal.Round(g.Revenue - g.Cost, 2) : null,
                g.HasCost ? "medium" : "unavailable",
                g.HasCost ? "Gross profit uses order-line UnitCost." : "UnitCost missing — gross profit not shown."))
            .ToList();
    }

    private async Task<FinanceBudgetDto> MapBudgetAsync(FinanceBudget budget, CancellationToken ct)
    {
        var actual = await ResolveBudgetActualAsync(budget, ct);
        var remaining = budget.Amount - actual;
        var utilization = budget.Amount <= 0 ? 0 : decimal.Round(actual / budget.Amount * 100m, 2);
        return new FinanceBudgetDto(
            budget.Id, budget.Name, budget.ExpenseCategoryId, budget.CategoryName, budget.Amount, budget.Currency,
            budget.PeriodStart, budget.PeriodEnd, decimal.Round(actual, 2), decimal.Round(remaining, 2), utilization,
            actual > budget.Amount, budget.IsRecommendation, budget.Status);
    }

    private async Task<decimal> ResolveBudgetActualAsync(FinanceBudget budget, CancellationToken ct)
    {
        var query = RecognizedExpensesQuery(budget.PeriodStart, budget.PeriodEnd);
        if (budget.ExpenseCategoryId is Guid categoryId)
            query = query.Where(x => x.ExpenseCategoryId == categoryId);
        else if (!string.IsNullOrWhiteSpace(budget.CategoryName))
            query = query.Where(x => x.ExpenseCategory.Name == budget.CategoryName);

        return await query.SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
    }

    private async Task TrackActivityAsync(
        string kind,
        string title,
        string summary,
        string severity,
        CancellationToken ct,
        Guid? relatedInvoiceId = null,
        Guid? relatedExpenseId = null,
        Guid? relatedBudgetId = null)
    {
        _db.FinanceAgentActivities.Add(new FinanceAgentActivity
        {
            TenantId = CompanyId,
            Kind = kind,
            Title = NalvaGuard.Clip(title, 200),
            Summary = NalvaGuard.Clip(summary, 2000),
            Severity = severity,
            Status = "Open",
            RelatedInvoiceId = relatedInvoiceId,
            RelatedExpenseId = relatedExpenseId,
            RelatedBudgetId = relatedBudgetId
        });
        await _db.SaveChangesAsync(ct);
        try
        {
            await _audit.LogChangeAsync("FinanceAgentActivity", Guid.Empty, kind, null, new { title, summary, severity }, ct);
        }
        catch
        {
            // Audit is best-effort for monitor loops.
        }
    }

    private async Task<ReportContext> ResolveContextAsync(DateTime? startDate, DateTime? endDate, string? period, CancellationToken ct)
    {
        var settings = await EnsureSettingsAsync(ct);
        var currency = await GetCurrencyAsync(ct);
        var tz = settings.TimeZoneId;
        var nowLocal = ToTimeZone(DateTime.UtcNow, tz);
        DateTime start;
        DateTime end;
        string label;

        if (startDate is not null && endDate is not null)
        {
            start = DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc);
            end = DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc);
            label = "custom";
        }
        else
        {
            var p = (period ?? "month").Trim().ToLowerInvariant();
            label = p;
            switch (p)
            {
                case "day":
                case "today":
                case "daily":
                    start = StartOfLocalDayUtc(nowLocal.Date, tz);
                    end = start.AddDays(1).AddTicks(-1);
                    label = "day";
                    break;
                case "week":
                case "weekly":
                    var weekStart = nowLocal.Date.AddDays(-(int)nowLocal.DayOfWeek);
                    start = StartOfLocalDayUtc(weekStart, tz);
                    end = start.AddDays(7).AddTicks(-1);
                    label = "week";
                    break;
                case "year":
                case "yearly":
                    start = StartOfLocalDayUtc(new DateTime(nowLocal.Year, 1, 1), tz);
                    end = StartOfLocalDayUtc(new DateTime(nowLocal.Year, 12, 31), tz).AddDays(1).AddTicks(-1);
                    label = "year";
                    break;
                default:
                    start = StartOfLocalDayUtc(new DateTime(nowLocal.Year, nowLocal.Month, 1), tz);
                    end = start.AddMonths(1).AddTicks(-1);
                    label = "month";
                    break;
            }
        }

        return new ReportContext(start, end, end, label, currency, tz);
    }

    private static DateTime ToTimeZone(DateTime utc, string timeZoneId)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);
        }
        catch
        {
            return DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        }
    }

    private static DateTime StartOfLocalDayUtc(DateTime localDate, string timeZoneId)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var local = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(local, tz);
        }
        catch
        {
            return DateTime.SpecifyKind(localDate.Date, DateTimeKind.Utc);
        }
    }

    private static MetricValueDto Metric(bool available, decimal? value, string basis, string? limitation = null) =>
        new(available, available ? value : null, basis, available ? null : limitation ?? "Unavailable");

    private static string FormatMetric(MetricValueDto metric) =>
        metric.Available ? metric.Value?.ToString("0.00", CultureInfo.InvariantCulture) ?? "n/a" : $"unavailable ({metric.Limitation})";

    private static int CategoryScore(string haystack, string categoryName)
    {
        var tokens = categoryName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Count(t => t.Length > 2 && haystack.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    private static List<DuplicateExpenseHintDto> DetectDuplicateExpenses(
        IReadOnlyList<(Guid Id, string Title, decimal Amount, DateTime ExpenseDate, string? Vendor)> rows)
    {
        var hints = new List<DuplicateExpenseHintDto>();
        for (var i = 0; i < rows.Count; i++)
        {
            for (var j = i + 1; j < rows.Count; j++)
            {
                var a = rows[i];
                var b = rows[j];
                if (a.Amount != b.Amount) continue;
                if (Math.Abs((a.ExpenseDate.Date - b.ExpenseDate.Date).TotalDays) > 3) continue;
                var sameTitle = string.Equals(a.Title.Trim(), b.Title.Trim(), StringComparison.OrdinalIgnoreCase);
                var sameVendor = !string.IsNullOrWhiteSpace(a.Vendor)
                                 && string.Equals(a.Vendor, b.Vendor, StringComparison.OrdinalIgnoreCase);
                if (!sameTitle && !sameVendor) continue;
                hints.Add(new DuplicateExpenseHintDto(
                    a.Id, b.Id, a.Title, a.Amount, a.ExpenseDate,
                    sameTitle && sameVendor
                        ? "Same title, vendor, amount, and nearby dates."
                        : "Same amount and nearby dates with matching title or vendor."));
            }
        }

        return hints.Take(50).ToList();
    }

    private static List<(DateTime Date, decimal Amount, string Description, string? Reference)> ParseCsv(string csv)
    {
        var result = new List<(DateTime, decimal, string, string?)>();
        var lines = csv.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0) return result;
        var start = 0;
        if (lines[0].Contains("date", StringComparison.OrdinalIgnoreCase)
            && lines[0].Contains("amount", StringComparison.OrdinalIgnoreCase))
            start = 1;

        for (var i = start; i < lines.Length; i++)
        {
            var parts = SplitCsvLine(lines[i]);
            if (parts.Count < 3) continue;
            if (!DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
                && !DateTime.TryParse(parts[0], out date))
                continue;
            if (!decimal.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                continue;
            var description = parts[2];
            if (description.Contains("ignore previous", StringComparison.OrdinalIgnoreCase)
                || description.Contains("system prompt", StringComparison.OrdinalIgnoreCase))
                description = "[redacted-injected-text]";
            string? reference = parts.Count > 3 ? parts[3] : null;
            result.Add((DateTime.SpecifyKind(date.Date, DateTimeKind.Utc), amount, description, reference));
        }

        return result;
    }

    private static List<string> SplitCsvLine(string line)
    {
        var list = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                list.Add(sb.ToString().Trim());
                sb.Clear();
                continue;
            }

            sb.Append(ch);
        }

        list.Add(sb.ToString().Trim());
        return list;
    }

    private static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private sealed record ReportContext(
        DateTime Start,
        DateTime End,
        DateTime AsOf,
        string PeriodLabel,
        string Currency,
        string TimeZoneId);
}
