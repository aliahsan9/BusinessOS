using System.Text.Json;

namespace BusinessOS.Domain.Companies;

/// <summary>
/// Agency launch templates. They configure the shared CompanyOS application.
/// They are not separate products.
/// </summary>
public static class AgencyTemplateCatalog
{
    public static readonly IReadOnlyList<string> LaunchAgents = ["ceo", "sales", "marketing", "support", "software"];

    public static readonly IReadOnlyList<string> LaunchWorkflows =
    [
        "lead-qualification",
        "customer-onboarding",
        "follow-up",
        "project-operations",
        "customer-support-automation",
        "executive-reporting"
    ];

    public static readonly IReadOnlyList<string> KnownModules =
    [
        "customers", "projects", "tasks", "invoices", "ai", "products", "inventory", "orders", "payments"
    ];

    public static readonly IReadOnlyList<string> KnownWidgets =
    [
        "performance", "sales", "customers", "orders", "products", "projects",
        "employees", "ai", "automation", "insights", "activity", "tasks"
    ];

    public static readonly IReadOnlyList<AgencyTemplateDefinition> All =
    [
        Template(
            "web-development",
            "Web development agency",
            "Client work, delivery, and billing for a web team.",
            ["customers", "projects", "tasks", "invoices", "ai"],
            ["projects", "customers", "tasks", "ai", "automation", "insights"],
            ["Owner", "Admin", "Manager", "Employee", "Sales"],
            LaunchWorkflows,
            LaunchAgents,
            11),
        Template(
            "software-consultancy",
            "Software consultancy",
            "Advisory work, proposals, and client delivery.",
            ["customers", "projects", "invoices", "ai"],
            ["customers", "sales", "projects", "ai", "insights"],
            ["Owner", "Admin", "Manager", "Employee", "Sales", "Accountant"],
            [..LaunchWorkflows, "proposal-generation"],
            LaunchAgents,
            12),
        Template(
            "mobile-app",
            "Mobile app development agency",
            "App projects, client coordination, and delivery.",
            ["customers", "projects", "tasks", "invoices", "ai"],
            ["projects", "tasks", "customers", "ai", "insights"],
            ["Owner", "Admin", "Manager", "Employee", "Sales"],
            LaunchWorkflows,
            [..LaunchAgents, "qa"],
            13),
        Template(
            "ai-automation",
            "AI automation agency",
            "Automation projects, client onboarding, and an AI workforce.",
            ["customers", "projects", "tasks", "invoices", "ai"],
            ["ai", "automation", "projects", "customers", "insights"],
            ["Owner", "Admin", "Manager", "Employee", "Sales"],
            [..LaunchWorkflows, "lead-acquisition"],
            LaunchAgents,
            14),
        Template(
            "digital-marketing",
            "Digital marketing agency",
            "Campaigns, content drafts, and client reporting.",
            ["customers", "projects", "invoices", "ai"],
            ["customers", "sales", "ai", "automation", "insights"],
            ["Owner", "Admin", "Manager", "Employee", "Sales"],
            [..LaunchWorkflows, "content-generation", "social-campaign", "marketing-performance"],
            [..LaunchAgents, "content", "social"],
            15)
    ];

    public static bool TryGet(string? key, out AgencyTemplateDefinition? template)
    {
        template = null;
        if (string.IsNullOrWhiteSpace(key))
            return false;

        template = All.FirstOrDefault(x => x.Key.Equals(key.Trim(), StringComparison.OrdinalIgnoreCase));
        return template is not null;
    }

    public static IEnumerable<BusinessTypeDefinition> AsBusinessTypes() =>
        All.Select(x => new BusinessTypeDefinition(x.Key, x.Name, x.Description, x.ModulesJson, x.SortOrder));

    public static string ModuleLabel(string module) => module switch
    {
        "customers" => "Customers and CRM",
        "projects" => "Projects",
        "tasks" => "Tasks",
        "invoices" => "Invoices",
        "ai" => "AI workforce",
        "products" => "Products",
        "inventory" => "Inventory",
        "orders" => "Orders",
        "payments" => "Payments",
        _ => module
    };

    private static AgencyTemplateDefinition Template(
        string key,
        string name,
        string description,
        string[] modules,
        string[] widgets,
        string[] roles,
        IReadOnlyList<string> workflows,
        IReadOnlyList<string> agents,
        int sortOrder) =>
        new(key, name, description, modules, widgets, roles, workflows, agents, sortOrder);
}

public sealed record AgencyTemplateDefinition(
    string Key,
    string Name,
    string Description,
    IReadOnlyList<string> Modules,
    IReadOnlyList<string> Widgets,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Workflows,
    IReadOnlyList<string> Agents,
    int SortOrder)
{
    public string ModulesJson => JsonSerializer.Serialize(Modules);
}

public static class LaunchSettings
{
    public const string RequireInvitation = "launch.requireInvitation";
}

/// <summary>
/// Public description of the first commercial release. Wording here is what the product is allowed to claim.
/// </summary>
public static class LaunchNoticeCatalog
{
    public const string Product = "CompanyOS";
    public const string Audience = "Software development agencies";
    public const string Status = "Early access. The first release is for a small number of software agencies.";

    public static readonly IReadOnlyList<string> Available =
    [
        "Company registration, login, profiles, company switching, and roles",
        "CRM, leads, customers, projects, tasks, employees, and teams",
        "Finance, invoices, and proposals",
        "Client portal and a public company storefront",
        "Built-in AI CEO, sales, marketing, customer support, and software development agents",
        "Command center, task assignment, approvals, execution history, and estimated AI cost",
        "Workflows for lead qualification, customer onboarding, follow-ups, project tasks, support drafts, and executive reports",
        "Subscription plans, trials, quotas, invoices, and hosted checkout when a payment provider is configured"
    ];

    public static readonly IReadOnlyList<string> Limitations =
    [
        "A payment-provider webhook was not replayed against Stripe, Tap, or PayTabs in the release verification for this phase",
        "A database restore drill was not run",
        "A staging deployment was not run",
        "Some plan flags are not checked on every endpoint",
        "AI output is draft work. Sending mail, publishing, changing production code, and deploying stay behind approval",
        "An agency template records modules, widgets, roles, workflows, and agents. The sidebar still shows every page the signed-in role is allowed to open",
        "Other industries are not the audience for this release"
    ];

    public static readonly IReadOnlyList<string> NotInThisRelease =
    [
        "A third-party agent marketplace and execution of external agent code",
        "Paid agent add-ons and marketplace revenue",
        "A public directory of companies",
        "A shared service marketplace with checkout or commissions",
        "White-label custom domains",
        "A public developer API platform and SDKs",
        "Enterprise single sign-on and custom deployment"
    ];

    public const string DataHandling =
        "Each company is a separate tenant. Usage events store an event name and a time, not customer records. Feedback stores the message the user typed. A data export contains a bounded set of that company's own operational records and omits passwords, tokens, integration secrets, and payment methods.";

    public const string SubscriptionTerms =
        "Catalog prices can be changed by a platform administrator. Choosing a plan during agency setup does not charge a card. Checkout, renewal, and cancellation stay on the billing page. Trial length follows the selected plan when a trial is started from billing or by a platform administrator.";

    public const string Support =
        "Use in-app feedback for bugs, feature requests, satisfaction, and support. A platform administrator can suspend a company. Company deletion is a request that an administrator confirms. Confirmation deactivates the company and keeps the rows until an operational purge.";
}
