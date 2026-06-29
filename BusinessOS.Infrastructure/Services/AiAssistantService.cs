using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Application.Features.Tenant.Services;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Services;

public sealed class AiAssistantService : IAiAssistantService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFeatureFlagService _featureFlags;
    private readonly ITenantLimitService _limitService;
    private readonly ILlmChatClient _llmChat;
    private readonly ILogger<AiAssistantService> _logger;

    public AiAssistantService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IFeatureFlagService featureFlags,
        ITenantLimitService limitService,
        ILlmChatClient llmChat,
        ILogger<AiAssistantService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _featureFlags = featureFlags;
        _limitService = limitService;
        _llmChat = llmChat;
        _logger = logger;
    }

    public async Task<AiChatResponse> ChatAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        await _featureFlags.EnsureFeatureEnabledAsync(FeatureFlags.AiAssistant, cancellationToken);
        await _limitService.EnsureWithinLimitAsync("ai", cancellationToken);

        var message = request.Message.Trim();
        var page = request.CurrentPage?.Trim().ToLowerInvariant() ?? string.Empty;
        var searchQuery = request.SearchQuery?.Trim();

        var searchResults = string.IsNullOrWhiteSpace(searchQuery)
            ? []
            : await SearchAsync(searchQuery, cancellationToken);

        var reply = await ResolveReplyAsync(message, page, cancellationToken);
        var suggestions = GetContextSuggestions(page);
        var quickActions = GetQuickActions();

        await SaveConversationAsync(message, reply, cancellationToken);
        await _limitService.IncrementAiUsageAsync(cancellationToken);

        return new AiChatResponse
        {
            Reply = reply,
            Suggestions = suggestions,
            QuickActions = quickActions,
            SearchResults = searchResults
        };
    }

    private async Task SaveConversationAsync(
        string prompt,
        string response,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var tenantId = _currentUserService.TenantId;

            if (userId is null || tenantId is null)
                return;

            _context.AIConversations.Add(new AIConversation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                UserId = userId,
                Prompt = prompt.Length > 4000 ? prompt[..4000] : prompt,
                Response = response.Length > 8000 ? response[..8000] : response
            });

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist AI conversation");
        }
    }

    private async Task<IReadOnlyList<AiSearchResultDto>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var term = query.ToLowerInvariant();
        var results = new List<AiSearchResultDto>();

        var customers = await _context.Customers
            .Where(x =>
                (x.FirstName + " " + x.LastName).ToLower().Contains(term)
                || x.Email.ToLower().Contains(term))
            .OrderBy(x => x.FirstName)
            .Take(5)
            .Select(x => new { x.Id, x.FirstName, x.LastName, x.Email })
            .ToListAsync(cancellationToken);

        results.AddRange(customers.Select(c => new AiSearchResultDto
        {
            Type = "Customer",
            Id = c.Id.ToString(),
            Title = $"{c.FirstName} {c.LastName}".Trim(),
            Subtitle = c.Email,
            Route = $"/customers/{c.Id}"
        }));

        var orders = await _context.Orders
            .Where(x => x.OrderNumber.ToLower().Contains(term))
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .Select(x => new { x.Id, x.OrderNumber, x.Status })
            .ToListAsync(cancellationToken);

        results.AddRange(orders.Select(o => new AiSearchResultDto
        {
            Type = "Project",
            Id = o.Id.ToString(),
            Title = o.OrderNumber,
            Subtitle = o.Status.ToString(),
            Route = $"/orders/{o.Id}"
        }));

        var invoices = await _context.Invoices
            .Where(x => x.InvoiceNumber.ToLower().Contains(term))
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .Select(x => new { x.Id, x.InvoiceNumber, x.Status })
            .ToListAsync(cancellationToken);

        results.AddRange(invoices.Select(i => new AiSearchResultDto
        {
            Type = "Invoice",
            Id = i.Id.ToString(),
            Title = i.InvoiceNumber,
            Subtitle = i.Status.ToString(),
            Route = $"/invoices/{i.Id}"
        }));

        var expenses = await _context.Expenses
            .Where(x => x.Title.ToLower().Contains(term)
                || (x.Description != null && x.Description.ToLower().Contains(term))
                || (x.Vendor != null && x.Vendor.ToLower().Contains(term)))
            .OrderByDescending(x => x.ExpenseDate)
            .Take(5)
            .Select(x => new { x.Id, x.Title, x.Vendor })
            .ToListAsync(cancellationToken);

        results.AddRange(expenses.Select(e => new AiSearchResultDto
        {
            Type = "Expense",
            Id = e.Id.ToString(),
            Title = e.Title,
            Subtitle = e.Vendor,
            Route = $"/expenses/{e.Id}"
        }));

        return results.Take(15).ToList();
    }

    private async Task<string> ResolveReplyAsync(
        string message,
        string page,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(message)
            && _llmChat.IsConfigured
            && _currentUserService.UserId is not null
            && _currentUserService.TenantId is not null)
        {
            try
            {
                var llmReply = await _llmChat.GenerateReplyAsync(
                    _currentUserService.TenantId.Value,
                    _currentUserService.UserId,
                    message,
                    page,
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(llmReply))
                {
                    return llmReply;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cursor AI request failed; falling back to built-in assistant replies");
            }
        }

        return ResolveReply(message, page);
    }

    private static string ResolveReply(string message, string page)
    {
        var normalized = message.ToLowerInvariant();

        if (ContainsAny(normalized, "customer", "client"))
            return "To create a customer, go to Customers → New Customer. Enter name, email, phone, and address. Customers can be linked to orders, invoices, and analytics reports.";

        if (ContainsAny(normalized, "project", "order"))
            return "Projects in BusinessOS are managed as Orders. Go to Orders → New Order, select a customer, add line items (tasks), set dates and status. Track revenue and progress from Analytics.";

        if (ContainsAny(normalized, "task", "line item"))
            return "Tasks are order line items. When creating or editing an order, add products/services as line items. Each line item represents a task with quantity, price, and status tracking.";

        if (ContainsAny(normalized, "invoice", "billing", "payment"))
            return "Create invoices from completed orders via Invoices, or generate from the order detail page. Track payment status, send reminders, and view outstanding invoices on the Invoices list.";

        if (ContainsAny(normalized, "expense", "cost", "spending"))
            return "Manage expenses under Expenses → New Expense. Categorize spending, attach vendors, and review expense analytics on the Analytics and Reports pages.";

        if (ContainsAny(normalized, "report", "pdf", "export"))
            return "Open Reports to generate business summaries, revenue, expense, and profit/loss PDFs. Use Reports → Data Reports for tabular exports and history.";

        if (ContainsAny(normalized, "analytics", "chart", "kpi", "metric"))
            return "The Analytics page shows revenue, expenses, profit trends, customer insights, and project/task breakdowns. Use date filters to adjust the reporting period.";

        if (ContainsAny(normalized, "dashboard", "overview", "home"))
            return "The Dashboard provides a real-time overview: sales KPIs, recent orders, inventory alerts, and quick links to key modules. Customize your workflow from Settings.";

        if (ContainsAny(normalized, "setting", "preference", "theme", "currency"))
            return "Open Settings to configure business profile, currency, tax, invoice preferences, notifications, appearance (dark mode), and AI assistant options.";

        if (ContainsAny(normalized, "help", "start", "onboard", "begin"))
            return "Welcome to BusinessOS! Complete the onboarding wizard for a guided setup, or ask me about any module. Visit /help for FAQs and documentation.";

        if (!string.IsNullOrEmpty(page))
        {
            return page switch
            {
                var p when p.Contains("customer") =>
                    "You're on the Customers page. Create new customers, view details, or check customer analytics. Need help creating one? Just ask!",
                var p when p.Contains("order") =>
                    "You're viewing Projects (Orders). Create a new project, manage tasks as line items, and track status through completion.",
                var p when p.Contains("invoice") =>
                    "You're on Invoices. Generate invoices from orders, track payments, and monitor outstanding balances.",
                var p when p.Contains("expense") =>
                    "You're on Expenses. Record business costs, categorize spending, and review expense trends in Analytics.",
                var p when p.Contains("analytics") =>
                    "You're on Analytics. Explore revenue, expenses, profit, customer rankings, and project/task metrics.",
                var p when p.Contains("report") =>
                    "You're on Reports. Generate PDF reports for business summary, revenue, expenses, and more.",
                var p when p.Contains("dashboard") =>
                    "You're on the Dashboard. This is your command center for daily business operations.",
                var p when p.Contains("setting") =>
                    "You're in Settings. Configure your business profile, preferences, and AI assistant here.",
                _ =>
                    "I'm BusinessOS AI. Ask me how to create customers, projects, tasks, invoices, manage expenses, or use analytics and reports."
            };
        }

        return "I'm BusinessOS AI, your guide to the platform. Ask about customers, projects, tasks, invoices, expenses, analytics, reports, or settings. Use quick actions below for common tasks.";
    }

    private static IReadOnlyList<AiSuggestionDto> GetContextSuggestions(string page)
    {
        if (page.Contains("order") || page.Contains("project"))
        {
            return
            [
                new() { Label = "Create new project", Message = "How do I create a new project?" },
                new() { Label = "Manage project tasks", Message = "How do I add tasks to a project?" },
                new() { Label = "Track project revenue", Message = "How do I track project revenue?" }
            ];
        }

        if (page.Contains("invoice"))
        {
            return
            [
                new() { Label = "Generate invoice", Message = "How do I generate an invoice?" },
                new() { Label = "Track payments", Message = "How do I track invoice payments?" },
                new() { Label = "Outstanding invoices", Message = "How do I view outstanding invoices?" }
            ];
        }

        if (page.Contains("customer"))
        {
            return
            [
                new() { Label = "Add customer", Message = "How do I create a customer?" },
                new() { Label = "Customer analytics", Message = "How do customer analytics work?" }
            ];
        }

        if (page.Contains("expense"))
        {
            return
            [
                new() { Label = "Record expense", Message = "How do I create an expense?" },
                new() { Label = "Expense reports", Message = "How do expense reports work?" }
            ];
        }

        if (page.Contains("analytics"))
        {
            return
            [
                new() { Label = "Revenue trends", Message = "How does revenue tracking work?" },
                new() { Label = "Project analytics", Message = "How do project analytics work?" }
            ];
        }

        return
        [
            new() { Label = "Getting started", Message = "Help me get started with BusinessOS" },
            new() { Label = "Create customer", Message = "How do I create a customer?" },
            new() { Label = "Create invoice", Message = "How do I create an invoice?" }
        ];
    }

    private static IReadOnlyList<AiQuickActionDto> GetQuickActions() =>
    [
        new() { Label = "Create Customer", Route = "/customers/new", Icon = "🤝" },
        new() { Label = "Create Project", Route = "/orders/new", Icon = "📋" },
        new() { Label = "Create Task", Route = "/orders/new", Icon = "✅" },
        new() { Label = "Create Invoice", Route = "/invoices", Icon = "🧾" },
        new() { Label = "Generate Report", Route = "/reports", Icon = "📄" },
        new() { Label = "Open Analytics", Route = "/analytics", Icon = "📈" }
    ];

    private static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
}
