using BusinessOS.Application.Features.Help.DTOs;
using BusinessOS.Application.Features.Help.Services;

namespace BusinessOS.Infrastructure.Services;

public sealed class HelpService : IHelpService
{
    public Task<HelpCenterDto> GetHelpCenterAsync(CancellationToken cancellationToken = default)
    {
        var faqs = new List<HelpFaqDto>
        {
            new()
            {
                Id = "getting-started-1",
                Category = "Getting Started",
                Question = "What is BusinessOS?",
                Answer = "BusinessOS is an all-in-one business management platform for customers, projects, tasks, invoices, expenses, analytics, and reports."
            },
            new()
            {
                Id = "getting-started-2",
                Category = "Getting Started",
                Question = "How do I complete onboarding?",
                Answer = "After your first login, the onboarding wizard guides you through business setup, creating your first customer, project, task, and invoice. You can skip steps or resume later."
            },
            new()
            {
                Id = "customers-1",
                Category = "Customers",
                Question = "How do I create a customer?",
                Answer = "Navigate to Customers → New Customer. Fill in name, email, phone, and address, then save. Customers can be linked to orders and invoices."
            },
            new()
            {
                Id = "projects-1",
                Category = "Projects",
                Question = "How do projects work?",
                Answer = "Projects are managed as Orders. Create an order for a customer, set project dates and status, and track progress through the order lifecycle."
            },
            new()
            {
                Id = "tasks-1",
                Category = "Tasks",
                Question = "How do I create tasks?",
                Answer = "Tasks are order line items. When creating or editing an order, add products or services as line items. Each represents a deliverable task."
            },
            new()
            {
                Id = "invoices-1",
                Category = "Invoices",
                Question = "How do I create an invoice?",
                Answer = "Go to Invoices and generate from a completed order, or use the order detail page. Track payment status and outstanding balances from the invoice list."
            },
            new()
            {
                Id = "expenses-1",
                Category = "Expenses",
                Question = "How do I manage expenses?",
                Answer = "Use Expenses → New Expense to record costs. Assign categories and vendors. View expense trends in Analytics and Reports."
            },
            new()
            {
                Id = "analytics-1",
                Category = "Analytics",
                Question = "What analytics are available?",
                Answer = "Analytics includes revenue, expenses, profit trends, customer rankings, project status distribution, and task completion metrics."
            },
            new()
            {
                Id = "reports-1",
                Category = "Reports",
                Question = "How do reports work?",
                Answer = "Reports generates PDF documents for business summary, revenue, expenses, profit/loss, customers, projects, and tasks. Access report history from the Reports hub."
            },
            new()
            {
                Id = "settings-1",
                Category = "Settings",
                Question = "How do I configure AI assistant settings?",
                Answer = "Open Settings → General tab. Toggle Enable AI Assistant and Show Suggestions to control the floating BusinessOS AI widget."
            },
            new()
            {
                Id = "ai-1",
                Category = "AI Assistant",
                Question = "What can BusinessOS AI help with?",
                Answer = "BusinessOS AI answers questions about all modules, provides context-aware suggestions based on your current page, and supports smart search across customers, projects, invoices, and expenses."
            }
        };

        var documentation = new List<HelpDocSectionDto>
        {
            new()
            {
                Title = "Getting Started",
                Description = "Learn the basics of BusinessOS",
                Topics = ["Account setup", "Onboarding wizard", "Navigation overview", "Dark mode"]
            },
            new()
            {
                Title = "Customer Management",
                Description = "Manage your customer relationships",
                Topics = ["Create customers", "Customer details", "Customer analytics", "Link to orders"]
            },
            new()
            {
                Title = "Projects & Tasks",
                Description = "Track projects and deliverables",
                Topics = ["Create projects (orders)", "Add tasks (line items)", "Status workflow", "Project analytics"]
            },
            new()
            {
                Title = "Finance",
                Description = "Invoices, payments, and expenses",
                Topics = ["Create invoices", "Track payments", "Record expenses", "Profit & loss"]
            },
            new()
            {
                Title = "Analytics & Reports",
                Description = "Insights and document generation",
                Topics = ["Dashboard KPIs", "Analytics charts", "PDF reports", "Export data"]
            }
        };

        return Task.FromResult(new HelpCenterDto
        {
            Faqs = faqs,
            Documentation = documentation
        });
    }
}
