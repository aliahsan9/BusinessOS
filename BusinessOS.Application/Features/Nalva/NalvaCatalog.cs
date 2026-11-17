using BusinessOS.Application.Common.Authorization;
using BusinessOS.Domain.Billing;

namespace BusinessOS.Application.Features.Nalva;

public static class NalvaStatuses
{
    public const string Inactive = "Inactive";
    public const string Active = "Active";
    public const string Pending = "Pending";
    public const string Queued = "Queued";
    public const string Running = "Running";
    public const string AwaitingApproval = "AwaitingApproval";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
    public const string Paused = "Paused";
    public const string DeadLetter = "DeadLetter";
    public const string Expired = "Expired";
    public const string Skipped = "Skipped";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string ChangesRequested = "ChangesRequested";
    public const string Healthy = "Healthy";
    public const string Degraded = "Degraded";
    public const string Unavailable = "Unavailable";
}

public static class NalvaLimits
{
    public const int MaxToolCalls = 12;
    public const int MaxDelegationDepth = 4;
    public const int MaxRetries = 3;
    public const int MaxExecutionSeconds = 120;
    public const int MaxTokensPerExecution = 8_000;
    public const int MaxConcurrentPerAgent = 2;
    public const int ShortTermMemoryCap = 20;
    public const int SummaryChars = 500;
    public const string PricingVersion = "2026-11";
    public const int MaxModelCalls = 3;
    public const int MaxEventDepth = 3;
    public const int MaxParallelSteps = 4;
    public const int ToolTimeoutSeconds = 30;
    public const decimal MaxEstimatedCostPerExecutionUsd = 1.00m;
    public const int TasksPerCompanyPerPass = 2;
    public const int TasksPerPass = 10;
}

public static class NalvaErrorClass
{
    public const string None = "None";
    public const string Transient = "Transient";
    public const string Permanent = "Permanent";
    public const string Uncertain = "Uncertain";
    public const string RateLimit = "RateLimit";
    public const string PermissionDenied = "PermissionDenied";
    public const string Validation = "Validation";
    public const string Timeout = "Timeout";
    public const string Integration = "Integration";
}

public sealed record NalvaToolDefinition(
    string Name,
    string Description,
    bool IsWrite,
    bool RequiresApproval,
    string? ApprovalAction,
    string Risk,
    bool ExternalEffect,
    string Integration,
    IReadOnlyList<string> Permissions);

public sealed record NalvaAgentDefinition(
    string Key,
    string Name,
    string Description,
    string Role,
    string Category,
    IReadOnlyList<string> Responsibilities,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Tools,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Workflows,
    string DefaultModel,
    IReadOnlyList<string> ApprovalActions,
    int DailyExecutionLimit,
    int DefaultAutonomy,
    bool IsCore,
    int Version,
    bool UsesKnowledge);

public sealed record NalvaModelPrice(
    string Model,
    string Kind,
    decimal InputUsdPerMillion,
    decimal OutputUsdPerMillion,
    int MaxTokens,
    bool SupportsTools);

public sealed record NalvaWorkflowStepTemplate(
    string Key,
    string AgentKey,
    string Objective,
    IReadOnlyList<string> Tools,
    int? ParallelGroup,
    string? Condition = null);

public sealed record NalvaWorkflowTemplate(
    string Key,
    string Name,
    string Description,
    string Category,
    string TriggerKind,
    IReadOnlyList<NalvaWorkflowStepTemplate> Steps,
    string? TriggerEvent = null,
    int? IntervalMinutes = null);

public static class NalvaToolCatalog
{
    private static NalvaToolDefinition Read(string name, string description, params string[] permissions) =>
        new(name, description, false, false, null, "Low", false, "live", permissions);

    private static NalvaToolDefinition Draft(string name, string description, params string[] permissions) =>
        new(name, description, true, false, null, "Low", false, "live", permissions);

    private static NalvaToolDefinition Guarded(
        string name,
        string description,
        string action,
        string risk,
        string integration,
        params string[] permissions) =>
        new(name, description, true, true, action, risk, true, integration, permissions);

    public static readonly IReadOnlyList<NalvaToolDefinition> All =
    [
        Read("GetCompanyOverview", "Counts and status from this company's records.", PermissionCodes.AnalyticsView),
        Read("GetBusinessKPIs", "Recorded sales, invoice, stock, and task counts.", PermissionCodes.AnalyticsView),
        Read("GetSalesSummary", "Order totals for the last 30 days.", PermissionCodes.OrderView),
        Read("GetFinancialSummary", "Recorded invoices, payments, and expenses. Not a statutory statement.", PermissionCodes.FinanceView),
        Read("GetProjectSummary", "Project and task counts.", PermissionCodes.ProjectView),
        Read("GetInventorySummary", "Stock totals and low-stock count.", PermissionCodes.InventoryView),
        Read("GetEmployeeSummary", "Employee counts only. No salaries or personal records.", PermissionCodes.EmployeeView),
        Read("GetAgentStatus", "Activation and recent execution status for this company's agents.", PermissionCodes.NalvaView),
        Read("GetAgentExecutionResults", "Recent execution summaries for this company.", PermissionCodes.NalvaView),
        Draft("CreateAgentTask", "Queue work for another active agent in this company.", PermissionCodes.NalvaManage),
        Draft("DelegateAgentTask", "Hand an objective to another active agent through the orchestrator.", PermissionCodes.NalvaManage),
        Read("GetCompanyProfile", "Company profile stored for this tenant.", PermissionCodes.OrganizationView),
        Read("GetTargetCustomers", "Customers already stored for this company.", PermissionCodes.CustomerView),
        Read("GetCustomerSegments", "Saved customer segments.", PermissionCodes.CustomerView),
        Read("ResearchMarket", "Segment and customer counts. External market feeds are not connected.", PermissionCodes.AnalyticsView),
        Read("ResearchCompetitors", "Authorized competitor notes in this company's knowledge. No open-web scrape.", PermissionCodes.NalvaView),
        Draft("GenerateMarketingStrategy", "Store a strategy draft grounded in company segments.", PermissionCodes.NalvaManage),
        Draft("CreateContentDraft", "Store marketing or article copy as a draft.", PermissionCodes.NalvaManage),
        Draft("CreateContentCalendar", "Store a content calendar draft.", PermissionCodes.NalvaManage),
        Draft("CreateCampaignDraft", "Store a campaign draft. Nothing is published.", PermissionCodes.NalvaManage),
        Read("GetCampaignAnalytics", "Counts of campaign drafts and related leads. Not an ad-platform report.", PermissionCodes.AnalyticsView),
        Read("SearchAuthorizedLeadSources", "Leads and customers already in this company.", PermissionCodes.LeadView),
        Read("ResearchProspect", "A stored lead or customer. Missing records are reported as missing.", PermissionCodes.LeadView),
        Draft("CreateLead", "Create a CRM lead from supplied fields. Duplicates are not inserted.", PermissionCodes.LeadManage),
        Draft("UpdateLead", "Update fields on an existing lead.", PermissionCodes.LeadManage),
        Draft("CreateDeal", "Create an open deal from supplied fields.", PermissionCodes.DealManage),
        Draft("CreateFollowUp", "Schedule a follow-up on a stored lead, customer, or deal.", PermissionCodes.LeadManage),
        Draft("CreateProposalDraft", "Store a proposal draft. It is not sent.", PermissionCodes.NalvaManage),
        Draft("CreateQuotationDraft", "Create a draft quotation when the customer already exists.", PermissionCodes.QuotationCreate),
        Read("GetSalesPipeline", "Lead and deal counts by status.", PermissionCodes.LeadView),
        Read("GetConversionAnalytics", "Converted leads compared with all leads.", PermissionCodes.LeadView),
        Read("GetLeadDetails", "One stored lead.", PermissionCodes.LeadView),
        Guarded("SendExternalEmail", "Ask to send an external email. No mailbox is connected, so nothing is sent.", "SendExternalEmail", "High", "prepared", PermissionCodes.NalvaManage),
        Guarded("PublishSocialPost", "Ask to publish a social post. No social account is connected.", "PublishSocialContent", "High", "prepared", PermissionCodes.NalvaManage),
        Guarded("SpendAdBudget", "Ask to spend an advertising budget. No ads account is connected.", "SpendAdvertisingBudget", "Critical", "prepared", PermissionCodes.FinanceView, PermissionCodes.NalvaManage),
        Read("SearchKnowledgeBase", "Search knowledge this agent is allowed to read.", PermissionCodes.NalvaView),
        Read("GetCustomerDetails", "One stored customer, without payment credentials.", PermissionCodes.CustomerView),
        Read("GetOrderDetails", "One stored order.", PermissionCodes.OrderView),
        Read("GetCustomerHistory", "Orders and follow-ups for one customer.", PermissionCodes.CustomerView),
        Draft("CreateSupportTicket", "Open an internal support work item.", PermissionCodes.NalvaManage),
        Draft("UpdateSupportTicket", "Update an internal support work item.", PermissionCodes.NalvaManage),
        Draft("CreateResponseDraft", "Store a customer reply draft. It is not sent.", PermissionCodes.NalvaManage),
        Draft("EscalateTicket", "Mark a support work item as escalated.", PermissionCodes.NalvaManage),
        Guarded("IssueRefund", "Ask to refund a payment. Nalva does not change the payment.", "IssueRefund", "Critical", "prepared", PermissionCodes.PaymentUpdate, PermissionCodes.NalvaApprove),
        Read("GetProjectDetails", "One stored project and its task counts.", PermissionCodes.ProjectView),
        Read("GetProjectRequirements", "The project description stored on the record.", PermissionCodes.ProjectView),
        Draft("CreateDevelopmentTask", "Create a project task when the project already exists.", PermissionCodes.TaskCreate),
        Read("AnalyzeRepository", "Report whether a repository integration is connected. No repository is read.", PermissionCodes.ProjectView),
        Draft("ReviewCodeChanges", "Store a review note for supplied text. Code is not executed.", PermissionCodes.NalvaManage),
        Draft("GenerateCode", "Store a specification draft. Nothing is written to a repository or executed.", PermissionCodes.NalvaManage),
        Draft("GenerateUnitTests", "Store a test-checklist draft. Tests are not executed.", PermissionCodes.NalvaManage),
        Read("AnalyzePullRequest", "Report whether a pull-request integration is connected.", PermissionCodes.ProjectView),
        Draft("GenerateTechnicalDocumentation", "Store a documentation draft from the stored project description.", PermissionCodes.NalvaManage),
        Draft("GenerateCodeReviewReport", "Store a review report draft.", PermissionCodes.NalvaManage),
        Guarded("ExecuteProductionDeployment", "Ask to deploy. No deployment target is connected.", "ExecuteProductionDeployment", "Critical", "prepared", PermissionCodes.NalvaManage),
        Read("GetExpenseRecords", "Stored expenses.", PermissionCodes.ExpenseView),
        Read("GetInvoiceRecords", "Stored invoices.", PermissionCodes.InvoiceView),
        Read("GetPaymentRecords", "Stored payments.", PermissionCodes.PaymentView),
        Read("GetOutstandingReceivables", "Invoices with an outstanding balance.", PermissionCodes.InvoiceView),
        Read("GetOutstandingPayables", "Purchase invoices and draft purchases still open.", PermissionCodes.PurchaseOrderView),
        Read("CalculateProfitability", "Recorded payments minus recorded expenses for 30 days.", PermissionCodes.FinanceView),
        Read("ForecastCashFlow", "A labeled estimate only when enough history exists.", PermissionCodes.FinanceView),
        Draft("GenerateFinancialReport", "Store a financial summary draft from recorded totals.", PermissionCodes.FinanceView, PermissionCodes.ReportView),
        Read("GetJobRequirements", "Stored job-requirement knowledge for this agent.", PermissionCodes.EmployeeView),
        Draft("AnalyzeCandidateCV", "Compare supplied CV text with job text. This is not a hiring decision.", PermissionCodes.NalvaManage),
        Draft("CompareCandidateQualifications", "Token overlap between supplied skills and requirements. Protected traits are ignored.", PermissionCodes.NalvaManage),
        Draft("CreateInterviewQuestionSet", "Store interview questions from the job text.", PermissionCodes.NalvaManage),
        Draft("CreateOnboardingTask", "Store an onboarding checklist item.", PermissionCodes.NalvaManage),
        Read("GetEmployeeDetails", "Directory fields for one employee. Salary and emergency contacts are omitted.", PermissionCodes.EmployeeView),
        Draft("GenerateHRReport", "Store a headcount report. It is not a personnel decision.", PermissionCodes.EmployeeView),
        Guarded("ChangeEmployeeRecord", "Ask to change an employee record. Nalva does not apply the change.", "ChangeEmployeeRecord", "High", "prepared", PermissionCodes.EmployeeManage),
        Read("GetSalesAnalytics", "Order totals grouped by status.", PermissionCodes.AnalyticsView),
        Read("GetRevenueAnalytics", "Recorded payment totals by month.", PermissionCodes.AnalyticsView, PermissionCodes.PaymentView),
        Read("GetInventoryAnalytics", "Stock value from cost price times quantity.", PermissionCodes.InventoryView),
        Read("GetCustomerAnalytics", "Customer counts and repeat buyers from orders.", PermissionCodes.CustomerView),
        Read("GetProjectAnalytics", "Project counts by status.", PermissionCodes.ProjectView),
        Read("GetExpenseAnalytics", "Expense totals by category.", PermissionCodes.ExpenseView),
        Draft("GenerateBusinessReport", "Store a business report from the analytics tools' recorded figures.", PermissionCodes.ReportView),
        Read("IdentifyBusinessTrends", "Compare this month's orders with the previous month.", PermissionCodes.AnalyticsView),
        Read("GetInventoryLevels", "Current stock rows.", PermissionCodes.InventoryView),
        Read("GetLowStockProducts", "Products at or below their reorder level.", PermissionCodes.InventoryView),
        Read("GetSupplierDetails", "One stored supplier.", PermissionCodes.SupplierView),
        Read("CompareSupplierPrices", "Stored purchase prices for a product.", PermissionCodes.SupplierView, PermissionCodes.PurchaseOrderView),
        Read("GetPurchaseHistory", "Stored purchases.", PermissionCodes.PurchaseOrderView),
        Read("ForecastProductDemand", "A labeled demand note from recent order lines, or a statement that history is too short.", PermissionCodes.InventoryView),
        Draft("CreatePurchaseOrderDraft", "Create a draft purchase. It is not sent to a supplier.", PermissionCodes.PurchaseOrderCreate),
        Guarded("CreatePurchaseOrder", "Create a pending purchase only after approval. It is not transmitted to a supplier.", "CreateSupplierPurchaseOrder", "High", "live", PermissionCodes.PurchaseOrderCreate),
        Read("GetPendingPurchaseOrders", "Purchases that are still draft or pending.", PermissionCodes.PurchaseOrderView),
        Read("GetUpcomingTasks", "Open tasks with a due date.", PermissionCodes.TaskView),
        Draft("CreateTask", "Create a task on an existing project.", PermissionCodes.TaskCreate),
        Read("GetCalendarEvents", "Tasks due soon. No external calendar is connected.", PermissionCodes.TaskView),
        Draft("CreateCalendarEvent", "Store an internal calendar note. No invitation is sent.", PermissionCodes.NalvaManage),
        Draft("SummarizeMeetingNotes", "Store a summary of supplied notes.", PermissionCodes.NalvaManage),
        Draft("CreateEmailDraft", "Store an email draft. It is not sent.", PermissionCodes.NalvaManage),
        Draft("CreateReminder", "Store an internal reminder.", PermissionCodes.NalvaManage),
        Read("GenerateDailySummary", "Tasks due and recent workforce results.", PermissionCodes.NalvaView),
        Read("GetAttendanceSummary", "Attendance counts for 30 days. No reasons or medical notes.", PermissionCodes.EmployeeView),
        Guarded("ConvertLead", "Convert a lead into a customer through the CRM service, only after approval.", "ConvertLead", "Medium", "live", PermissionCodes.LeadManage, PermissionCodes.DealManage, PermissionCodes.CustomerCreate),
        Draft("IngestKnowledge", "Add a knowledge source for this company and, when set, this agent.", PermissionCodes.NalvaManage),
        Draft("DeleteKnowledge", "Remove a knowledge source this company owns.", PermissionCodes.NalvaManage),
        Read("GetChurnRisk", "Customers with an older order and none in the last 90 days.", PermissionCodes.CustomerView),
        Read("FindDuplicateLeads", "Leads that share an email or phone.", PermissionCodes.LeadView),
        Read("GetOperationalAlerts", "Failed executions and pending approvals. No server access.", PermissionCodes.NalvaView)
    ];

    private static readonly Dictionary<string, NalvaToolDefinition> ByName = All
        .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

    public static NalvaToolDefinition Get(string name) =>
        ByName.TryGetValue(name, out var tool)
            ? tool
            : throw new InvalidOperationException($"Unknown Nalva tool '{name}'.");

    public static bool TryGet(string? name, out NalvaToolDefinition? tool)
    {
        tool = null;
        return !string.IsNullOrWhiteSpace(name) && ByName.TryGetValue(name, out tool);
    }
}

public static class NalvaAgentCatalog
{
    public static readonly IReadOnlyList<string> Categories =
    [
        "Executive Management",
        "Marketing",
        "Sales",
        "Customer Support",
        "Software Development",
        "Finance",
        "Human Resources",
        "Business Intelligence",
        "Procurement",
        "Administration"
    ];

    private static NalvaAgentDefinition Agent(
        string key,
        string name,
        string description,
        string role,
        string category,
        string[] responsibilities,
        string[] tools,
        string[] workflows,
        bool core,
        bool knowledge = false) 
    {
        var defs = tools.Select(NalvaToolCatalog.Get).ToArray();
        return new NalvaAgentDefinition(
            key,
            name,
            description,
            role,
            category,
            responsibilities,
            responsibilities,
            tools,
            defs.SelectMany(x => x.Permissions).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            workflows,
            "gpt-4o-mini",
            defs.Where(x => x.ApprovalAction is not null).Select(x => x.ApprovalAction!).Distinct().ToArray(),
            100,
            3,
            core,
            1,
            knowledge);
    }

    public static readonly IReadOnlyList<NalvaAgentDefinition> All =
    [
        Agent("ceo", "AI CEO", "Coordinates the workforce and reports on recorded company activity.", "Chief operations agent", "Executive Management",
            ["Monitor recorded performance", "Delegate work to active agents", "Surface overdue work and risks", "Prepare daily and weekly summaries"],
            ["GetCompanyOverview", "GetBusinessKPIs", "GetSalesSummary", "GetFinancialSummary", "GetProjectSummary", "GetInventorySummary", "GetEmployeeSummary", "GetAgentStatus", "CreateAgentTask", "DelegateAgentTask", "GetAgentExecutionResults", "GenerateDailySummary"],
            ["executive-reporting", "customer-acquisition"], true),
        Agent("marketing", "AI Marketing", "Prepares marketing drafts from this company's customers and segments.", "Marketing agent", "Marketing",
            ["Summarize stored segments", "Draft strategy, content, and campaigns", "Stop before any external publish or ad spend"],
            ["GetCompanyProfile", "GetTargetCustomers", "GetCustomerSegments", "ResearchMarket", "ResearchCompetitors", "GenerateMarketingStrategy", "CreateContentDraft", "CreateContentCalendar", "CreateCampaignDraft", "GetCampaignAnalytics", "PublishSocialPost", "SpendAdBudget"],
            ["content-generation", "social-campaign", "competitor-research", "marketing-performance"], true, true),
        Agent("sales", "AI Sales", "Works the CRM records this company already has and drafts outreach.", "Sales agent", "Sales",
            ["Review authorized leads", "Create leads only from supplied fields", "Draft proposals and quotations", "Request approval before external outreach"],
            ["SearchAuthorizedLeadSources", "ResearchProspect", "CreateLead", "GetLeadDetails", "UpdateLead", "CreateDeal", "CreateFollowUp", "CreateProposalDraft", "CreateQuotationDraft", "GetSalesPipeline", "GetConversionAnalytics", "CreateEmailDraft", "SendExternalEmail", "FindDuplicateLeads", "ConvertLead"],
            ["lead-qualification", "prospect-research", "proposal-generation", "follow-up", "quotation-generation", "lead-to-customer", "customer-acquisition"], true),
        Agent("support", "AI Customer Support", "Answers from authorized knowledge and stored orders, and drafts replies.", "Customer support agent", "Customer Support",
            ["Search authorized knowledge", "Read permitted customer and order records", "Draft replies and track internal tickets", "Escalate refunds instead of issuing them"],
            ["SearchKnowledgeBase", "GetCustomerDetails", "GetOrderDetails", "CreateSupportTicket", "UpdateSupportTicket", "CreateResponseDraft", "EscalateTicket", "GetCustomerHistory", "IssueRefund"],
            [], true, true),
        Agent("software", "AI Software Development", "Turns stored project requirements into review drafts. It does not run code or deploy.", "Software development agent", "Software Development",
            ["Read stored project requirements", "Create development tasks", "Store specification and review drafts", "Refuse repository access and production deploys until a real integration and approval exist"],
            ["GetProjectDetails", "GetProjectRequirements", "CreateDevelopmentTask", "AnalyzeRepository", "ReviewCodeChanges", "GenerateCode", "GenerateUnitTests", "AnalyzePullRequest", "GenerateTechnicalDocumentation", "GenerateCodeReviewReport", "ExecuteProductionDeployment"],
            [], true),
        Agent("finance", "AI Finance", "Reads recorded finance data and labels estimates. It does not post accounting entries.", "Finance agent", "Finance",
            ["Summarize recorded expenses, invoices, and payments", "Highlight outstanding balances", "Forecast only when history is sufficient", "Keep estimates separate from the books"],
            ["GetFinancialSummary", "GetExpenseRecords", "GetInvoiceRecords", "GetPaymentRecords", "GetOutstandingReceivables", "GetOutstandingPayables", "CalculateProfitability", "ForecastCashFlow", "GenerateFinancialReport"],
            ["executive-reporting"], true),
        Agent("hr", "AI HR", "Supports recruiting and onboarding with human review. It does not hire or edit employee records.", "HR and recruitment agent", "Human Resources",
            ["Draft job and interview material", "Compare supplied qualifications without protected traits", "Prepare onboarding tasks", "Keep salary and emergency contacts out of results"],
            ["GetJobRequirements", "AnalyzeCandidateCV", "CompareCandidateQualifications", "CreateInterviewQuestionSet", "CreateOnboardingTask", "GetEmployeeDetails", "GenerateHRReport", "GetAttendanceSummary", "ChangeEmployeeRecord"],
            [], true),
        Agent("analyst", "AI Business Analyst", "Reports trends from recorded sales, stock, customers, projects, and expenses.", "Business analyst", "Business Intelligence",
            ["Read recorded analytics", "Separate history from estimates", "Store a business report draft"],
            ["GetSalesAnalytics", "GetRevenueAnalytics", "GetInventoryAnalytics", "GetCustomerAnalytics", "GetProjectAnalytics", "GetExpenseAnalytics", "GenerateBusinessReport", "IdentifyBusinessTrends"],
            ["marketing-performance", "executive-reporting"], true),
        Agent("procurement", "AI Procurement", "Watches stock and prepares purchase drafts. Commitments wait for approval.", "Procurement and inventory agent", "Procurement",
            ["Find low stock", "Compare stored supplier prices", "Draft purchase orders", "Create a pending purchase only after approval"],
            ["GetInventoryLevels", "GetLowStockProducts", "GetSupplierDetails", "CompareSupplierPrices", "GetPurchaseHistory", "ForecastProductDemand", "CreatePurchaseOrderDraft", "GetPendingPurchaseOrders", "CreatePurchaseOrder"],
            [], true),
        Agent("assistant", "AI Executive Assistant", "Organizes internal tasks, reminders, and drafts. It does not send mail or invitations.", "Executive assistant", "Administration",
            ["List upcoming tasks", "Store reminders and calendar notes", "Draft emails and meeting summaries"],
            ["GetUpcomingTasks", "CreateTask", "GetCalendarEvents", "CreateCalendarEvent", "SummarizeMeetingNotes", "CreateEmailDraft", "CreateReminder", "GenerateDailySummary"],
            ["executive-reporting"], true),
        Agent("content", "Content Creator", "Drafts articles and marketing copy for human editing.", "Content creator", "Marketing",
            ["Draft articles, newsletters, and website copy from supplied notes and company knowledge"],
            ["GetCompanyProfile", "SearchKnowledgeBase", "CreateContentDraft", "CreateContentCalendar"],
            ["content-generation"], false, true),
        Agent("seo", "SEO Agent", "Prepares on-page recommendations from stored content. Search Console is not connected.", "SEO agent", "Marketing",
            ["Review stored content", "Draft keyword and on-page recommendations", "Avoid invented search volumes"],
            ["SearchKnowledgeBase", "GetCompanyProfile", "CreateContentDraft", "ResearchMarket"],
            ["seo-research"], false, true),
        Agent("social", "Social Media Agent", "Drafts platform-specific posts. Publishing waits for approval and a connected account.", "Social media agent", "Marketing",
            ["Draft posts", "Build a publishing schedule draft", "Report company draft activity, not platform engagement"],
            ["CreateContentDraft", "CreateCampaignDraft", "CreateContentCalendar", "GetCampaignAnalytics", "PublishSocialPost"],
            ["social-campaign"], false),
        Agent("email", "Email Marketing Agent", "Drafts newsletters and segments. Sending waits for approval.", "Email marketing agent", "Marketing",
            ["Draft newsletters", "Read saved segments", "Prepare follow-up drafts"],
            ["GetCustomerSegments", "CreateEmailDraft", "CreateCampaignDraft", "GetCampaignAnalytics", "SendExternalEmail"],
            ["email-marketing"], false),
        Agent("proposal", "Proposal Agent", "Drafts proposals and quotations from stored customers.", "Proposal agent", "Sales",
            ["Draft proposals", "Create draft quotations for existing customers"],
            ["GetCustomerDetails", "CreateProposalDraft", "CreateQuotationDraft", "GetSalesPipeline"],
            ["proposal-generation", "quotation-generation"], false),
        Agent("legal", "Legal Assistant", "Organizes documents the company stored. It is not a lawyer and does not give legal advice.", "Legal assistant", "Administration",
            ["Summarize stored documents", "Point at clauses the text contains", "Refuse to act as counsel"],
            ["SearchKnowledgeBase", "IngestKnowledge", "CreateContentDraft"],
            [], false, true),
        Agent("qa", "Quality Assurance Agent", "Drafts test checklists and bug notes from stored project text. It does not run the suite.", "Quality assurance agent", "Software Development",
            ["Draft test cases", "Store bug-report notes", "Keep execution out of the application server"],
            ["GetProjectDetails", "GetProjectRequirements", "CreateDevelopmentTask", "GenerateUnitTests", "GenerateCodeReviewReport"],
            [], false),
        Agent("knowledge", "Knowledge Manager", "Files and retires knowledge for this company, with agent or shared scope.", "Knowledge manager", "Administration",
            ["Ingest documents", "Find duplicate titles", "Delete sources the company owns"],
            ["SearchKnowledgeBase", "IngestKnowledge", "DeleteKnowledge", "ResearchCompetitors"],
            [], false, true),
        Agent("ecommerce", "E-commerce Agent", "Helps with stored products, orders, and listing drafts.", "E-commerce agent", "Sales",
            ["Read stock and orders", "Draft product descriptions", "Avoid inventing orders"],
            ["GetInventoryLevels", "GetOrderDetails", "GetSalesSummary", "CreateContentDraft", "GetCustomerDetails"],
            [], false),
        Agent("success", "Customer Success Agent", "Flags renewal and inactivity from stored orders and follow-ups.", "Customer success agent", "Customer Support",
            ["List customers with no recent orders", "Draft renewal reminders", "Read stored history"],
            ["GetChurnRisk", "GetCustomerHistory", "GetCustomerDetails", "CreateFollowUp", "CreateReminder"],
            [], false),
        Agent("data-entry", "Data Entry Agent", "Creates and checks CRM records from supplied fields.", "Data entry agent", "Administration",
            ["Create leads from supplied fields", "Detect duplicates", "Refuse missing required fields"],
            ["CreateLead", "UpdateLead", "FindDuplicateLeads", "GetLeadDetails"],
            [], false),
        Agent("it-ops", "IT Operations Agent", "Summarizes workforce failures and pending approvals. It has no server administration rights.", "IT operations agent", "Administration",
            ["List failed executions", "List pending approvals", "Stay off the host operating system"],
            ["GetOperationalAlerts", "GetAgentStatus", "GetAgentExecutionResults"],
            [], false)
    ];

    private static readonly Dictionary<string, NalvaAgentDefinition> ByKey = All
        .ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);

    public static bool TryGet(string? key, out NalvaAgentDefinition? agent)
    {
        agent = null;
        return !string.IsNullOrWhiteSpace(key) && ByKey.TryGetValue(key.Trim(), out agent);
    }

    public static NalvaAgentDefinition Get(string key) =>
        TryGet(key, out var agent) ? agent! : throw new InvalidOperationException($"Unknown Nalva agent '{key}'.");
}

public static class NalvaModelCatalog
{
    public static readonly IReadOnlyList<NalvaModelPrice> Prices =
    [
        new("gpt-4o-mini", "chat", 0.15m, 0.60m, 128_000, true),
        new("gpt-4o", "chat", 2.50m, 10.00m, 128_000, true),
        new("text-embedding-3-small", "embedding", 0.02m, 0m, 8_191, false)
    ];

    public static NalvaModelPrice? Find(string? model) =>
        Prices.FirstOrDefault(x => string.Equals(x.Model, model, StringComparison.OrdinalIgnoreCase));

    public static decimal EstimateUsd(string? model, int inputTokens, int outputTokens)
    {
        var price = Find(model) ?? Prices[0];
        return AiUsageCost.Estimate(inputTokens, outputTokens, price.InputUsdPerMillion, price.OutputUsdPerMillion);
    }
}

public static class NalvaWorkflowCatalog
{
    private static NalvaWorkflowStepTemplate Step(string key, string agent, string objective, params string[] tools) =>
        new(key, agent, objective, tools, null);

    private static NalvaWorkflowStepTemplate When(string key, string agent, string objective, string condition, params string[] tools) =>
        new(key, agent, objective, tools, null, condition);

    private static NalvaWorkflowStepTemplate Parallel(string key, string agent, string objective, int group, params string[] tools) =>
        new(key, agent, objective, tools, group);

    public static readonly IReadOnlyList<NalvaWorkflowTemplate> All =
    [
        new("customer-acquisition", "Customer acquisition", "Research stored segments, prepare lead and outreach drafts, and report to the CEO. External sending stops for approval.", "Sales", "Manual",
        [
            Step("market-research", "marketing", "Summarize stored segments and customers. Do not invent a market.", "GetCustomerSegments", "ResearchMarket"),
            Step("lead-generation", "sales", "List authorized lead sources and create a lead only when name and email were supplied.", "SearchAuthorizedLeadSources", "FindDuplicateLeads", "CreateLead"),
            Step("lead-qualification", "sales", "Qualify a lead only when an id was supplied and the record has an email.", "GetLeadDetails", "UpdateLead"),
            Step("marketing-preparation", "marketing", "Draft campaign copy from the stored segments.", "CreateContentDraft", "CreateCampaignDraft"),
            Step("outreach-preparation", "sales", "Draft outreach and request approval before any send.", "CreateEmailDraft", "CreateFollowUp", "SendExternalEmail"),
            Step("executive-report", "ceo", "Summarize the recorded pipeline and workforce activity.", "GetSalesSummary", "GetAgentExecutionResults")
        ]),
        new("content-generation", "Content generation", "Draft content from the company profile and authorized knowledge.", "Marketing", "Manual",
        [
            Step("sources", "content", "Read the company profile and authorized knowledge.", "GetCompanyProfile", "SearchKnowledgeBase"),
            Step("draft", "content", "Store a content draft.", "CreateContentDraft")
        ]),
        new("social-campaign", "Social campaign", "Draft a social campaign and stop before publishing.", "Marketing", "Manual",
        [
            Step("draft", "social", "Draft the campaign and calendar.", "CreateCampaignDraft", "CreateContentCalendar"),
            Step("publish-check", "social", "Request approval before any publish.", "PublishSocialPost")
        ]),
        new("seo-research", "SEO research", "Recommend from stored content only.", "Marketing", "Manual",
        [
            Step("review", "seo", "Review stored knowledge and draft recommendations.", "SearchKnowledgeBase", "CreateContentDraft")
        ]),
        new("email-marketing", "Email marketing", "Draft an email and stop before sending.", "Marketing", "Manual",
        [
            Step("segment", "email", "Read saved segments and draft the email.", "GetCustomerSegments", "CreateEmailDraft"),
            Step("send-check", "email", "Request approval before any send.", "SendExternalEmail")
        ]),
        new("competitor-research", "Competitor research", "Use only competitor notes stored for this company.", "Marketing", "Manual",
        [
            Step("notes", "marketing", "Search authorized competitor knowledge.", "ResearchCompetitors")
        ]),
        new("marketing-performance", "Marketing performance", "Report recorded campaign drafts and sales.", "Marketing", "Manual",
        [
            Step("numbers", "analyst", "Read recorded sales and draft activity.", "GetSalesAnalytics", "GetCampaignAnalytics"),
            Step("report", "analyst", "Store the report.", "GenerateBusinessReport")
        ]),
        new("lead-qualification", "Lead qualification", "Qualify one supplied lead.", "Sales", "Manual",
        [
            Step("qualify", "sales", "Read the lead and update qualification only when the id is supplied.", "GetLeadDetails", "UpdateLead")
        ]),
        new("prospect-research", "Prospect research", "Read one stored prospect.", "Sales", "Manual",
        [
            Step("prospect", "sales", "Read the stored prospect.", "ResearchProspect")
        ]),
        new("proposal-generation", "Proposal generation", "Draft a proposal and quotation for an existing customer.", "Sales", "Manual",
        [
            Step("customer", "proposal", "Read the customer.", "GetCustomerDetails"),
            Step("proposal", "proposal", "Store the proposal and draft quotation.", "CreateProposalDraft", "CreateQuotationDraft")
        ]),
        new("follow-up", "Follow-up", "Schedule a follow-up on a stored record.", "Sales", "Manual",
        [
            Step("follow-up", "sales", "Create the follow-up from supplied ids.", "CreateFollowUp")
        ]),
        new("quotation-generation", "Quotation generation", "Draft a quotation for an existing customer.", "Sales", "Manual",
        [
            Step("quote", "proposal", "Create the draft quotation.", "CreateQuotationDraft")
        ]),
        new("lead-to-customer", "Lead to customer", "Convert a lead only after a person approves.", "Sales", "Manual",
        [
            Step("inspect", "sales", "Read the lead.", "GetLeadDetails"),
            Step("convert", "sales", "Ask for approval before conversion.", "ConvertLead")
        ]),
        new("executive-reporting", "Executive reporting", "Build a daily report from recorded totals.", "Executive Management", "Manual",
        [
            Step("collect", "ceo", "Read recorded company totals.", "GetCompanyOverview", "GetBusinessKPIs", "GetSalesSummary", "GetFinancialSummary"),
            Step("report", "ceo", "Summarize workforce activity.", "GetAgentStatus", "GenerateDailySummary")
        ]),
        new("customer-onboarding", "Customer onboarding", "Validate a new customer, draft a welcome message, and segment only when marketing consent is present.", "Customer Support", "Event",
        [
            Step("customer", "support", "Read the customer record and draft a welcome reply. Do not send it.", "GetCustomerDetails", "CreateResponseDraft"),
            When("segment", "marketing", "Read eligible segments only when marketingConsent is true.", "marketingConsent == true", "GetCustomerSegments"),
            Step("onboarding-plan", "success", "Create an internal onboarding follow-up from the supplied customer.", "GetCustomerDetails", "CreateFollowUp"),
            Step("record", "ceo", "Record the onboarding in the company overview.", "GetCompanyOverview")
        ], NalvaEventNames.CustomerRegistered),
        new("lead-acquisition", "Lead acquisition", "Research a supplied prospect, qualify the lead, draft a proposal, and stop before external outreach.", "Sales", "Event",
        [
            Step("research", "sales", "Read the stored prospect and lead. Do not invent company facts.", "ResearchProspect", "GetLeadDetails"),
            Step("qualify", "sales", "Update qualification only when a lead id was supplied.", "UpdateLead", "FindDuplicateLeads"),
            When("proposal", "proposal", "Draft a proposal when the lead priority is High. Do not send it.", "priority == High", "CreateProposalDraft"),
            Step("outreach", "sales", "Draft outreach and request approval before any send.", "CreateEmailDraft", "SendExternalEmail"),
            Step("follow-up", "sales", "Schedule an internal follow-up.", "CreateFollowUp"),
            Step("metrics", "ceo", "Read workforce results for this company.", "GetAgentExecutionResults")
        ], NalvaEventNames.LeadCreated),
        new("inventory-automation", "Low stock monitoring", "Verify low stock, review suppliers, and stop at a purchase approval.", "Procurement", "Event",
        [
            Step("stock", "procurement", "Verify stock that is below its reorder level.", "GetLowStockProducts", "GetInventoryLevels"),
            Parallel("demand", "analyst", "Read recorded inventory analytics.", 1, "GetInventoryAnalytics"),
            Parallel("suppliers", "procurement", "Review stored suppliers and prices.", 1, "GetSupplierDetails", "CompareSupplierPrices"),
            Step("draft-po", "procurement", "Store a purchase draft. Do not transmit it.", "CreatePurchaseOrderDraft"),
            Step("approve-po", "procurement", "Ask a person before creating a pending purchase.", "CreatePurchaseOrder")
        ], NalvaEventNames.StockBelowThreshold),
        new("financial-monitoring", "Financial monitoring", "Read recorded finance data, flag outstanding invoices, and draft an executive summary.", "Finance", "Scheduled",
        [
            Parallel("books", "finance", "Read recorded finance, receivables, and expenses.", 1, "GetFinancialSummary", "GetOutstandingReceivables", "GetExpenseRecords"),
            Parallel("trends", "analyst", "Read recorded expense analytics.", 1, "GetExpenseAnalytics", "IdentifyBusinessTrends"),
            Step("report", "finance", "Store a financial report draft.", "GenerateFinancialReport"),
            Step("notify", "ceo", "Prepare the daily summary from recorded totals.", "GenerateDailySummary")
        ], null, 1440),
        new("project-operations", "Overdue project task", "Review an overdue project task, notify through an internal task, and record the result.", "Software Development", "Event",
        [
            Step("project", "ceo", "Read the recorded project summary.", "GetProjectSummary"),
            Step("follow-up", "assistant", "Create an internal follow-up task and reminder.", "CreateTask", "CreateReminder"),
            Step("summary", "ceo", "Summarize recorded workforce activity.", "GetAgentExecutionResults")
        ], NalvaEventNames.TaskOverdue),
        new("customer-support-automation", "Customer support", "Classify a new ticket from authorized knowledge and draft a reply. Sending stays behind approval.", "Customer Support", "Event",
        [
            Step("knowledge", "knowledge", "Search authorized knowledge for this company.", "SearchKnowledgeBase"),
            Step("draft", "support", "Draft a reply from stored knowledge and customer context.", "SearchKnowledgeBase", "GetCustomerDetails", "CreateResponseDraft"),
            Step("escalate", "support", "Escalate the internal ticket when the case is complex. Do not refund.", "EscalateTicket")
        ], NalvaEventNames.SupportTicketCreated)
    ];

    public static bool TryGet(string? key, out NalvaWorkflowTemplate? template)
    {
        template = All.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
        return template is not null;
    }
}
