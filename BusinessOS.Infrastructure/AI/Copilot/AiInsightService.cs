using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI.Copilot;

public sealed class AiInsightService : IAiInsightService
{
    private readonly IApplicationDbContext _context;

    public AiInsightService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AiProactiveInsightDto>> GetProactiveInsightsAsync(CancellationToken cancellationToken = default)
    {
        var insights = new List<AiProactiveInsightDto>();
        var now = DateTime.UtcNow;

        var overdueInvoices = await _context.Invoices
            .CountAsync(i => i.OutstandingAmount > 0 && i.DueDate < now, cancellationToken);
        if (overdueInvoices > 0)
        {
            insights.Add(new AiProactiveInsightDto
            {
                Type = "LatePayment",
                Severity = overdueInvoices > 5 ? "high" : "medium",
                Title = "Overdue invoices",
                Message = $"{overdueInvoices} invoice(s) are past due. Follow up to protect cashflow.",
                ActionRoute = "/invoices",
                ActionLabel = "Review invoices"
            });
        }

        var overdueTasks = await _context.WorkTasks
            .CountAsync(t => t.DueDate < now && t.Status != WorkTaskStatus.Done, cancellationToken);
        if (overdueTasks > 0)
        {
            insights.Add(new AiProactiveInsightDto
            {
                Type = "ProjectDelay",
                Severity = "medium",
                Title = "Overdue tasks",
                Message = $"{overdueTasks} task(s) are overdue and may delay project delivery.",
                ActionRoute = "/orders",
                ActionLabel = "View projects"
            });
        }

        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = monthStart.AddMonths(-1);
        var thisMonthRevenue = await _context.Invoices
            .Where(i => i.InvoiceDate >= monthStart)
            .SumAsync(i => i.GrandTotal, cancellationToken);
        var lastMonthRevenue = await _context.Invoices
            .Where(i => i.InvoiceDate >= lastMonthStart && i.InvoiceDate < monthStart)
            .SumAsync(i => i.GrandTotal, cancellationToken);

        if (lastMonthRevenue > 0 && thisMonthRevenue < lastMonthRevenue * 0.85m)
        {
            var decline = ((lastMonthRevenue - thisMonthRevenue) / lastMonthRevenue) * 100;
            insights.Add(new AiProactiveInsightDto
            {
                Type = "RevenueDecline",
                Severity = "high",
                Title = "Revenue decline",
                Message = $"Revenue is down {decline:0.#}% compared to last month.",
                ActionRoute = "/analytics",
                ActionLabel = "View analytics"
            });
        }

        var thisMonthExpenses = await _context.Expenses
            .Where(e => e.ExpenseDate >= monthStart)
            .SumAsync(e => e.Amount, cancellationToken);
        var lastMonthExpenses = await _context.Expenses
            .Where(e => e.ExpenseDate >= lastMonthStart && e.ExpenseDate < monthStart)
            .SumAsync(e => e.Amount, cancellationToken);

        if (lastMonthExpenses > 0 && thisMonthExpenses > lastMonthExpenses * 1.2m)
        {
            insights.Add(new AiProactiveInsightDto
            {
                Type = "ExpenseSpike",
                Severity = "medium",
                Title = "Expense spike",
                Message = "Expenses increased more than 20% compared to last month.",
                ActionRoute = "/expenses",
                ActionLabel = "Review expenses"
            });
        }

        var inactiveCustomers = await _context.Customers
            .CountAsync(c => c.IsActive && !c.Orders.Any(o => o.OrderDate >= now.AddDays(-90)), cancellationToken);
        if (inactiveCustomers > 0)
        {
            insights.Add(new AiProactiveInsightDto
            {
                Type = "CustomerChurn",
                Severity = "low",
                Title = "Customer churn risk",
                Message = $"{inactiveCustomers} active customer(s) have not ordered in 90 days.",
                ActionRoute = "/customers",
                ActionLabel = "Review customers"
            });
        }

        return insights;
    }

    public async Task<AiDashboardCopilotDto> GetDashboardCopilotAsync(CancellationToken cancellationToken = default)
    {
        var insights = await GetProactiveInsightsAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var pendingTasks = await _context.WorkTasks
            .CountAsync(t => t.Status == WorkTaskStatus.Todo || t.Status == WorkTaskStatus.InProgress, cancellationToken);
        var overdueInvoices = await _context.Invoices
            .CountAsync(i => i.OutstandingAmount > 0 && i.DueDate < now, cancellationToken);

        var summary = insights.Count == 0
            ? "Your business looks healthy today. Review pending tasks and recent sales activity."
            : $"You have {insights.Count} priority insight(s). Start with overdue invoices and delayed work items.";

        var focusAreas = new List<AiSuggestionDto>();
        if (overdueInvoices > 0)
            focusAreas.Add(new() { Label = "Collect overdue payments", Message = "Show overdue invoices" });
        if (pendingTasks > 0)
            focusAreas.Add(new() { Label = "Review pending tasks", Message = "Show pending tasks" });
        focusAreas.Add(new() { Label = "Today's revenue", Message = "What is our revenue this month?" });

        return new AiDashboardCopilotDto
        {
            Summary = summary,
            Insights = insights,
            FocusAreas = focusAreas
        };
    }
}
