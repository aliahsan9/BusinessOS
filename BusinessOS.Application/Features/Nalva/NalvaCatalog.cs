using BusinessOS.Application.Common.Authorization;
using BusinessOS.Domain.Billing;

namespace BusinessOS.Application.Features.Nalva;

public static class NalvaStatuses
{
    public const string Inactive = "Inactive";
    public const string Active = "Active";
    public const string Pending = "Pending";
    /// <summary>Legacy enqueue status. Prefer <see cref="Assigned"/> for new tasks (Phase 8B).</summary>
    public const string Queued = "Queued";
    /// <summary>Task created and bound to an agent; waiting for a worker claim.</summary>
    public const string Assigned = "Assigned";
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

    /// <summary>Statuses a background worker may claim for execution.</summary>
    public static bool IsRunnable(string? status) =>
        status is Assigned or Queued;
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
    /// <summary>
    /// Max tasks executed in parallel within a single processor pass.
    /// Bounded so one busy tenant cannot monopolize the worker process.
    /// </summary>
    public const int MaxParallelTaskExecutions = 3;
    /// <summary>Stalled Running claims older than this are recovered after restart or crash.</summary>
    public const int ClaimLeaseSeconds = 180;
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
        Read("GetFinancialSummary", "Tenant finance intelligence: sales revenue, collected cash, expenses, profit, AR/AP. Not a statutory statement.", PermissionCodes.FinanceView),
        Read("GetProjectSummary", "Project and task counts.", PermissionCodes.ProjectView),
        Read("GetInventorySummary", "Stock totals and low-stock count.", PermissionCodes.InventoryView),
        Read("GetEmployeeSummary", "Employee counts only. No salaries or personal records.", PermissionCodes.EmployeeView),
        Read("GetAgentStatus", "Activation and recent execution status for this company's agents.", PermissionCodes.NalvaView),
        Read("GetAgentExecutionResults", "Recent execution summaries for this company.", PermissionCodes.NalvaView),
        Draft("CreateAgentTask", "Queue work for another active agent in this company.", PermissionCodes.NalvaManage),
        Draft("DelegateAgentTask", "Hand an objective to another active agent through the orchestrator.", PermissionCodes.NalvaManage),
        Read("GetCompanyProfile", "Company / marketing brand profile stored for this tenant.", PermissionCodes.OrganizationView),
        Read("GetMarketingBrandProfile", "Marketing brand profile seeded from tenant and storefront records.", PermissionCodes.NalvaView),
        Read("GetTargetCustomers", "Customers already stored for this company.", PermissionCodes.CustomerView),
        Read("GetCustomerSegments", "Saved customer segments.", PermissionCodes.CustomerView),
        Read("ResearchMarket", "Market note from company records and brand profile. External market feeds are not connected.", PermissionCodes.AnalyticsView),
        Read("ResearchCompetitors", "Competitor research from brand profile and authorized knowledge. No fabricated analytics.", PermissionCodes.NalvaView),
        Draft("GenerateMarketingStrategy", "Store a marketing strategy on a campaign grounded in brand facts.", PermissionCodes.NalvaManage),
        Draft("CreateContentDraft", "Store marketing or article copy as a draft content item.", PermissionCodes.NalvaManage),
        Draft("CreateContentCalendar", "Generate a multi-day content calendar of draft posts.", PermissionCodes.NalvaManage),
        Draft("CreateCampaignDraft", "Store a campaign draft. Nothing is published.", PermissionCodes.NalvaManage),
        Draft("UpsertBrandProfile", "Update marketing brand profile fields from supplied facts only.", PermissionCodes.NalvaManage),
        Draft("GenerateMarketingGraphic", "Generate a graphic when OpenAI images are configured, otherwise store a BriefOnly visual brief.", PermissionCodes.NalvaManage),
        Draft("ScheduleApprovedMarketingPost", "Schedule an approved content item for publish via sandbox or connected Meta.", PermissionCodes.NalvaManage),
        Read("SyncMarketingAnalytics", "Sync verified analytics snapshots or mark metrics unavailable. Never invents engagement.", PermissionCodes.NalvaView),
        Read("GenerateMarketingPerformanceReport", "Performance report from verified snapshots and content counts only.", PermissionCodes.AnalyticsView),
        Draft("ShareMarketingInsightsWithSales", "Store a structured MarketingInsight work item for sales. Not freeform chat.", PermissionCodes.NalvaManage),
        Read("GetCampaignAnalytics", "Campaign and content counts plus verified analytics snapshots. Not invented ad-platform metrics.", PermissionCodes.AnalyticsView),
        Read("SearchAuthorizedLeadSources", "Leads and customers already in this company.", PermissionCodes.LeadView),
        Read("ResearchExternalProspects", "Research prospects through the connected authorized provider or owner-enabled fixture. Does not invent contacts.", PermissionCodes.LeadView, PermissionCodes.LeadManage),
        Draft("QualifyLead", "Score a researched prospect or lead against configurable criteria. Scores are estimates.", PermissionCodes.LeadManage),
        Draft("CreateOutreachCampaign", "Create an outreach campaign record. Nothing is sent.", PermissionCodes.NalvaManage),
        Draft("GeneratePersonalizedOutreach", "Draft a personalized outreach email for a prospect. Sending still requires approval.", PermissionCodes.NalvaManage),
        Read("GetSalesAgentStatus", "Persisted Sales Agent metrics, provider status, and objective state.", PermissionCodes.NalvaView),
        Draft("AssignSalesObjective", "Assign a lead-generation objective to the Sales Agent.", PermissionCodes.NalvaManage),
        Draft("AssignMarketingObjective", "Assign a marketing campaign objective to the Marketing Agent.", PermissionCodes.NalvaManage),
        Draft("ProcessProspectReply", "Record a prospect reply, update CRM, and schedule follow-up.", PermissionCodes.LeadManage),
        Draft("GenerateSalesCampaignReport", "Store a campaign report from persisted Sales Agent records.", PermissionCodes.ReportView, PermissionCodes.NalvaView),
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
        Guarded("PublishSocialPost", "Ask to publish a social post. Meta Facebook Page feed publish runs when Meta is connected with pageId metadata; otherwise sandbox/no-op after approval.", "PublishSocialContent", "High", "prepared", PermissionCodes.NalvaManage),
        Guarded("SpendAdBudget", "Ask to spend an advertising budget. No ads account is connected.", "SpendAdvertisingBudget", "Critical", "prepared", PermissionCodes.FinanceView, PermissionCodes.NalvaManage),
        Read("SearchKnowledgeBase", "Search knowledge this agent is allowed to read.", PermissionCodes.NalvaView),
        Read("GetCustomerDetails", "One stored customer, without payment credentials.", PermissionCodes.CustomerView),
        Read("GetOrderDetails", "One stored order.", PermissionCodes.OrderView),
        Read("GetCustomerHistory", "Orders and follow-ups for one customer.", PermissionCodes.CustomerView),
        Draft("CreateSupportTicket", "Open a persisted support ticket from customer context.", PermissionCodes.NalvaManage),
        Draft("UpdateSupportTicket", "Update a persisted support ticket status, priority, or assignment.", PermissionCodes.NalvaManage),
        Draft("CreateResponseDraft", "Store an internal customer reply draft. It is not sent.", PermissionCodes.NalvaManage),
        Guarded("SendSupportReply", "Ask to send a support reply through a connected or sandbox channel. External WhatsApp/email sends require human approval.", "SendSupportReply", "High", "prepared", PermissionCodes.NalvaManage),
        Draft("EscalateTicket", "Escalate a support ticket and notify the owner.", PermissionCodes.NalvaManage),
        Draft("ResolveSupportTicket", "Mark a support ticket resolved and optionally schedule a follow-up.", PermissionCodes.NalvaManage),
        Draft("ScheduleSupportFollowUp", "Schedule a durable customer or employee support follow-up.", PermissionCodes.NalvaManage),
        Draft("ReceiveSupportMessage", "Ingest an inbound customer support message through the channel router.", PermissionCodes.NalvaManage),
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
        Read("GetExpenseRecords", "Stored expenses with anomaly and category insights from recognized Approved/Paid rows.", PermissionCodes.ExpenseView),
        Read("GetInvoiceRecords", "Stored invoices.", PermissionCodes.InvoiceView),
        Read("GetPaymentRecords", "Stored payments.", PermissionCodes.PaymentView),
        Read("GetOutstandingReceivables", "Open customer invoices with aging and overdue prioritization.", PermissionCodes.InvoiceView),
        Read("GetOutstandingPayables", "Outstanding supplier purchase invoices (not open POs).", PermissionCodes.PurchaseOrderView, PermissionCodes.FinanceView),
        Read("CalculateProfitability", "Gross/net profit from completed orders, COGS, and recognized expenses when data supports it.", PermissionCodes.FinanceView),
        Read("ForecastCashFlow", "Labeled cash-flow analysis and short-term projection when history and opening balance allow.", PermissionCodes.FinanceView),
        Draft("GenerateFinancialReport", "Generate an operational finance report from recorded totals with limitations disclosed.", PermissionCodes.FinanceView, PermissionCodes.ReportView),
        Read("AnalyzeExpenses", "Compare recognized expenses to the prior period, flag anomalies and duplicate hints.", PermissionCodes.ExpenseView, PermissionCodes.FinanceView),
        Read("GetFinanceIntelligence", "Detailed finance metrics distinguishing sales revenue from collected cash.", PermissionCodes.FinanceView),
        Read("GetReceivablesAging", "Invoice aging buckets and late-paying customers from outstanding balances.", PermissionCodes.InvoiceView, PermissionCodes.FinanceView),
        Read("GetPayablesSchedule", "Supplier payable priorities and recommended payment schedule (no funds moved).", PermissionCodes.FinanceView),
        Read("ReconcileFinancialRecords", "Compare invoices/payments/bills and imported bank lines; never silently alters records.", PermissionCodes.FinanceView),
        Read("GetBudgetStatus", "Owner-configured budgets versus actual recognized expenses.", PermissionCodes.FinanceView),
        Draft("PreparePaymentReminder", "Draft a personalized overdue payment reminder. Sending requires approval and a connected channel.", PermissionCodes.InvoiceView, PermissionCodes.NalvaManage),
        Guarded("SubmitPaymentReminder", "Send a prepared payment reminder after approval. Delivery is blocked without a connected channel.", "SendPaymentReminder", "High", "prepared", PermissionCodes.NalvaApprove),
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
        Read("ForecastSalesRevenue", "Statistical sales-revenue forecast from completed-order history with confidence labels. Predictions are not confirmed facts.", PermissionCodes.AnalyticsView, PermissionCodes.OrderView),
        Read("GetSalesIntelligence", "Sales intelligence: product performance, declining trends, conversion, and labeled revenue forecast.", PermissionCodes.AnalyticsView, PermissionCodes.OrderView),
        Read("GetCustomerIntelligence", "Customer segments, retention, and churn-risk estimates from completed orders.", PermissionCodes.CustomerView, PermissionCodes.AnalyticsView),
        Read("GetExecutiveDecisionBrief", "Data-backed decision brief for owner questions. Distinguishes actuals from forecasts.", PermissionCodes.AnalyticsView, PermissionCodes.NalvaView),
        Read("GetBusinessRisks", "Cross-domain business risks from inventory, finance, sales, and customer signals.", PermissionCodes.AnalyticsView, PermissionCodes.NalvaView),
        Read("GetInventoryLevels", "Current stock rows.", PermissionCodes.InventoryView),
        Read("GetLowStockProducts", "Products at or below their reorder level.", PermissionCodes.InventoryView),
        Read("GetInventoryRisks", "Low stock, out of stock, slow-moving, overstock, and discrepancy risks from recorded inventory and sales.", PermissionCodes.InventoryView),
        Read("GetProcurementOverview", "Inventory and procurement overview from recorded data.", PermissionCodes.InventoryView, PermissionCodes.PurchaseOrderView),
        Read("GetSupplierDetails", "One stored supplier.", PermissionCodes.SupplierView),
        Read("CompareSupplierPrices", "Stored purchase prices for a product plus labeled research provider results.", PermissionCodes.SupplierView, PermissionCodes.PurchaseOrderView),
        Read("ResearchSuppliers", "Compare stored suppliers and labeled research provider quotes for a product.", PermissionCodes.SupplierView),
        Read("GetPurchaseHistory", "Stored purchases.", PermissionCodes.PurchaseOrderView),
        Read("ForecastProductDemand", "Per-product demand and stock coverage when history is sufficient; otherwise labeled unavailable.", PermissionCodes.InventoryView),
        Read("GetProcurementRecommendations", "Reorder recommendations that account for inbound purchase orders.", PermissionCodes.InventoryView, PermissionCodes.PurchaseOrderView),
        Read("CalculateReorderRecommendations", "Alias of GetProcurementRecommendations for Inventory Manager reorder planning.", PermissionCodes.InventoryView, PermissionCodes.PurchaseOrderView),
        Read("GetDelayedDeliveries", "Open purchases past their expected delivery date.", PermissionCodes.PurchaseOrderView),
        Read("GetSlowMovingProducts", "Stocked products with no recent sales.", PermissionCodes.InventoryView),
        Read("GetProcurementBudgetNeed", "Estimated spend to cover current low and out-of-stock recommendations.", PermissionCodes.PurchaseOrderView, PermissionCodes.FinanceView),
        Draft("CreatePurchaseOrderDraft", "Create a draft purchase bound to supplier, quantity, and price. It is not sent to a supplier.", PermissionCodes.PurchaseOrderCreate),
        Draft("PreparePurchaseOrder", "Prepare a fingerprint-bound purchase draft for owner approval.", PermissionCodes.PurchaseOrderCreate),
        Guarded("CreatePurchaseOrder", "Create a pending purchase only after approval. It is not transmitted to a supplier.", "CreateSupplierPurchaseOrder", "High", "live", PermissionCodes.PurchaseOrderCreate),
        Guarded("SubmitPreparedPurchaseOrder", "Submit a prepared purchase after approval. Fingerprint must match.", "CreateSupplierPurchaseOrder", "High", "live", PermissionCodes.PurchaseOrderCreate),
        Draft("ReceivePurchaseOrderVerified", "Record verified received quantities and update stock. Supplier claims alone are refused.", PermissionCodes.PurchaseOrderUpdate, PermissionCodes.InventoryUpdate),
        Read("GetPendingPurchaseOrders", "Purchases that are still draft or pending.", PermissionCodes.PurchaseOrderView),
        Read("GetUpcomingTasks", "Open tasks with a due date.", PermissionCodes.TaskView),
        Draft("CreateTask", "Create a task on an existing project.", PermissionCodes.TaskCreate),
        Read("GetCalendarEvents", "Tasks due soon. No external calendar is connected.", PermissionCodes.TaskView),
        Guarded("CreateCalendarEvent", "Ask to create a calendar event. Uses Google Calendar when connected; otherwise stores an internal note. Invitations require approval.", "CreateCalendarEvent", "High", "prepared", PermissionCodes.NalvaManage),
        Guarded("SendWhatsAppMessage", "Ask to send an approved WhatsApp template or in-window text through the connected WhatsApp Business account.", "SendWhatsAppMessage", "High", "prepared", PermissionCodes.NalvaManage),
        Draft("SummarizeMeetingNotes", "Store a summary of supplied notes.", PermissionCodes.NalvaManage),
        Draft("CreateEmailDraft", "Store an email draft. It is not sent.", PermissionCodes.NalvaManage),
        Draft("CreateReminder", "Store an internal reminder.", PermissionCodes.NalvaManage),
        Read("GenerateDailySummary", "Tasks due and recent workforce results.", PermissionCodes.NalvaView),
        Read("GetAttendanceSummary", "Attendance counts for 30 days. No reasons or medical notes.", PermissionCodes.EmployeeView),
        Guarded("ConvertLead", "Convert a lead into a customer through the CRM service, only after approval.", "ConvertLead", "Medium", "live", PermissionCodes.LeadManage, PermissionCodes.DealManage, PermissionCodes.CustomerCreate),
        Draft("IngestKnowledge", "Add a knowledge source for this company and, when set, this agent.", PermissionCodes.NalvaManage),
        Draft("DeleteKnowledge", "Remove a knowledge source this company owns.", PermissionCodes.NalvaManage),
        Read("GetChurnRisk", "Customers with declining engagement (heuristic churn risk from completed-order silence). Labeled as prediction.", PermissionCodes.CustomerView),
        Read("FindDuplicateLeads", "Leads that share an email or phone.", PermissionCodes.LeadView),
        Read("GetOperationalAlerts", "Failed executions and pending approvals. No server access.", PermissionCodes.NalvaView),
        Read("RetrieveSharedBusinessContext", "Read tenant-scoped shared business context, policies, and prior verified decisions.", PermissionCodes.NalvaView),
        Draft("StoreSharedBusinessDecision", "Store a verified business decision or operational constraint for later agents.", PermissionCodes.NalvaManage),
        Read("VerifyOrderReadiness", "Verify an existing order against customer, stock, payment, and outstanding balance records.", PermissionCodes.OrderView, PermissionCodes.InventoryView, PermissionCodes.FinanceView),
        Draft("PrepareInvoiceDraft", "Prepare an invoice draft work item from verified customer and transaction details. Does not post ledger entries.", PermissionCodes.InvoiceView, PermissionCodes.NalvaManage),
        Draft("ClassifySupportComplaint", "Classify a support ticket complaint with urgency using stored ticket and order context.", PermissionCodes.NalvaManage),
        Draft("CoordinateWorkforceObjective", "Navla/CEO entrypoint: map an owner objective to a structured multi-agent workflow plan.", PermissionCodes.NalvaManage)
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
        "Inventory",
        "Procurement",
        "Administration"
    ];

    /// <summary>Phase 8B core AI employee keys (activatable workforce managers).</summary>
    public static readonly IReadOnlyList<string> Phase8BEmployeeKeys =
    [
        "sales",
        "marketing",
        "support",
        "inventory",
        "finance",
        "procurement"
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
            ["Monitor recorded performance", "Delegate work to active agents", "Surface overdue work and risks", "Prepare daily and weekly summaries", "Assign sales and marketing objectives", "Coordinate multi-agent business objectives"],
            ["GetCompanyOverview", "GetBusinessKPIs", "GetSalesSummary", "GetFinancialSummary", "GetProjectSummary", "GetInventorySummary", "GetEmployeeSummary", "GetAgentStatus", "CreateAgentTask", "DelegateAgentTask", "AssignSalesObjective", "AssignMarketingObjective", "GetSalesAgentStatus", "GetAgentExecutionResults", "GenerateDailySummary", "CoordinateWorkforceObjective", "GetExecutiveDecisionBrief", "GetBusinessRisks", "ForecastSalesRevenue", "GetCustomerIntelligence", "RetrieveSharedBusinessContext", "StoreSharedBusinessDecision"],
            ["executive-reporting", "customer-acquisition", "autonomous-lead-generation", "navla-marketing-campaign", "lead-to-customer-collaboration", "order-to-fulfillment", "low-stock-to-procurement", "complaint-to-resolution", "weekly-business-review"], true),
        Agent("marketing", "Marketing Manager", "Prepares marketing drafts from this company's brand profile, customers, and segments.", "Marketing Manager", "Marketing",
            ["Analyze business products and audience from stored brand and customer data", "Create marketing strategies grounded in brand facts", "Generate social media posts and campaign content as drafts", "Prepare promotional calendars", "Create platform-specific content", "Schedule publishing only through connected integrations after approval", "Analyze campaign performance from verified snapshots"],
            ["GetCompanyProfile", "GetMarketingBrandProfile", "UpsertBrandProfile", "GetTargetCustomers", "GetCustomerSegments", "ResearchMarket", "ResearchCompetitors", "GenerateMarketingStrategy", "CreateContentDraft", "CreateContentCalendar", "CreateCampaignDraft", "GenerateMarketingGraphic", "ScheduleApprovedMarketingPost", "SyncMarketingAnalytics", "GenerateMarketingPerformanceReport", "ShareMarketingInsightsWithSales", "GetCampaignAnalytics", "PublishSocialPost", "SpendAdBudget", "AssignMarketingObjective", "RetrieveSharedBusinessContext"],
            ["content-generation", "social-campaign", "competitor-research", "marketing-performance", "autonomous-marketing-weekly", "navla-marketing-campaign", "lead-to-customer-collaboration", "weekly-business-review"], true, true),
        Agent("sales", "Sales Manager", "Analyzes customers and sales history, qualifies CRM leads, and prepares approved outreach.", "Sales Manager", "Sales",
            ["Analyze customers and sales history from recorded CRM data", "Identify potential customers from authorized sources", "Find sales opportunities without inventing contacts", "Prepare customer outreach drafts", "Create quotations and proposals as drafts", "Follow up with leads through scheduled follow-ups", "Track the sales pipeline from persisted records", "Generate sales campaign reports from stored results"],
            ["SearchAuthorizedLeadSources", "ResearchExternalProspects", "ResearchProspect", "CreateLead", "GetLeadDetails", "UpdateLead", "QualifyLead", "CreateDeal", "CreateFollowUp", "CreateProposalDraft", "CreateQuotationDraft", "GetSalesPipeline", "GetConversionAnalytics", "GetCustomerSegments", "GetSalesIntelligence", "ForecastSalesRevenue", "GetChurnRisk", "GetCustomerIntelligence", "CreateEmailDraft", "CreateOutreachCampaign", "GeneratePersonalizedOutreach", "SendExternalEmail", "CreateCalendarEvent", "ProcessProspectReply", "GenerateSalesCampaignReport", "GetSalesAgentStatus", "AssignSalesObjective", "FindDuplicateLeads", "ConvertLead", "GetCompanyProfile", "GetCustomerDetails", "RetrieveSharedBusinessContext"],
            ["lead-qualification", "prospect-research", "proposal-generation", "follow-up", "quotation-generation", "lead-to-customer", "customer-acquisition", "autonomous-lead-generation", "navla-marketing-campaign", "lead-to-customer-collaboration", "order-to-fulfillment"], true),
        Agent("support", "Customer Support Manager", "Answers from authorized knowledge and stored orders, manages tickets, and escalates when needed.", "Customer Support Manager", "Customer Support",
            ["Answer customer questions using approved business knowledge", "Check order status from recorded orders", "Handle common complaints with stored context", "Draft support responses", "Escalate complex cases", "Maintain customer interaction history on tickets"],
            ["SearchKnowledgeBase", "GetCustomerDetails", "GetOrderDetails", "GetCustomerHistory", "CreateSupportTicket", "UpdateSupportTicket", "CreateResponseDraft", "SendSupportReply", "SendWhatsAppMessage", "ReceiveSupportMessage", "EscalateTicket", "ResolveSupportTicket", "ScheduleSupportFollowUp", "IssueRefund", "CreateFollowUp", "DelegateAgentTask", "ClassifySupportComplaint", "RetrieveSharedBusinessContext", "StoreSharedBusinessDecision"],
            ["customer-support-automation", "customer-onboarding", "complaint-to-resolution", "lead-to-customer-collaboration", "order-to-fulfillment"], true, true),
        Agent("software", "AI Software Development", "Turns stored project requirements into review drafts. It does not run code or deploy.", "Software development agent", "Software Development",
            ["Read stored project requirements", "Create development tasks", "Store specification and review drafts", "Refuse repository access and production deploys until a real integration and approval exist"],
            ["GetProjectDetails", "GetProjectRequirements", "CreateDevelopmentTask", "AnalyzeRepository", "ReviewCodeChanges", "GenerateCode", "GenerateUnitTests", "AnalyzePullRequest", "GenerateTechnicalDocumentation", "GenerateCodeReviewReport", "ExecuteProductionDeployment"],
            [], true),
        Agent("finance", "Finance Manager", "Monitors recorded finances, AR/AP, expenses, cash flow, and reconciliation. Sensitive money moves require approval. It does not post ledger entries or transfer funds.", "Finance Manager", "Finance",
            ["Analyze revenue and expenses from recorded totals", "Monitor outstanding payments and receivables aging", "Prepare financial summaries and operational reports", "Identify unusual expense transactions", "Track cash flow with confidence labels", "Generate financial reports from stored books", "Flag potential financial risks without inventing balances"],
            ["GetFinancialSummary", "GetFinanceIntelligence", "GetExpenseRecords", "AnalyzeExpenses", "GetInvoiceRecords", "GetPaymentRecords", "GetOutstandingReceivables", "GetReceivablesAging", "GetOutstandingPayables", "GetPayablesSchedule", "CalculateProfitability", "ForecastCashFlow", "ReconcileFinancialRecords", "GetBudgetStatus", "GenerateFinancialReport", "PreparePaymentReminder", "SubmitPaymentReminder", "PrepareInvoiceDraft", "VerifyOrderReadiness", "RetrieveSharedBusinessContext", "StoreSharedBusinessDecision"],
            ["executive-reporting", "financial-monitoring", "overdue-payment-followup", "lead-to-customer-collaboration", "order-to-fulfillment", "low-stock-to-procurement", "complaint-to-resolution", "weekly-business-review"], true),
        Agent("hr", "AI HR", "Supports recruiting and onboarding with human review. It does not hire or edit employee records.", "HR and recruitment agent", "Human Resources",
            ["Draft job and interview material", "Compare supplied qualifications without protected traits", "Prepare onboarding tasks", "Keep salary and emergency contacts out of results"],
            ["GetJobRequirements", "AnalyzeCandidateCV", "CompareCandidateQualifications", "CreateInterviewQuestionSet", "CreateOnboardingTask", "GetEmployeeDetails", "GenerateHRReport", "GetAttendanceSummary", "ChangeEmployeeRecord"],
            [], true),
        Agent("analyst", "AI Business Analyst", "Reports trends from recorded sales, stock, customers, projects, and expenses. Provides labeled forecasts when history allows.", "Business analyst", "Business Intelligence",
            ["Read recorded analytics", "Separate history from estimates", "Forecast sales with confidence labels", "Surface business risks and decision briefs", "Store a business report draft"],
            ["GetSalesAnalytics", "GetRevenueAnalytics", "GetInventoryAnalytics", "GetCustomerAnalytics", "GetProjectAnalytics", "GetExpenseAnalytics", "GenerateBusinessReport", "IdentifyBusinessTrends", "ForecastSalesRevenue", "GetSalesIntelligence", "GetCustomerIntelligence", "GetExecutiveDecisionBrief", "GetBusinessRisks", "GetChurnRisk", "RetrieveSharedBusinessContext", "StoreSharedBusinessDecision"],
            ["marketing-performance", "executive-reporting", "weekly-business-review"], true),
        Agent("inventory", "Inventory Manager", "Monitors stock levels, movement, and reorder need from recorded inventory. Does not create purchases — hands off to Procurement Manager.", "Inventory Manager", "Inventory",
            ["Monitor stock levels from recorded inventory rows", "Identify low-stock and out-of-stock products", "Analyze inventory movement and slow movers", "Recommend reorder quantities from recorded demand", "Identify slow-moving products", "Prepare structured restock context for Procurement", "Escalate purchase preparation via CreateAgentTask"],
            ["GetInventorySummary", "GetInventoryLevels", "GetLowStockProducts", "GetInventoryRisks", "GetInventoryAnalytics", "GetSlowMovingProducts", "ForecastProductDemand", "GetProcurementRecommendations", "CalculateReorderRecommendations", "CreateAgentTask", "DelegateAgentTask", "RetrieveSharedBusinessContext", "StoreSharedBusinessDecision"],
            ["inventory-automation", "procurement-daily-monitor", "low-stock-to-procurement", "order-to-fulfillment"], true),
        Agent("procurement", "Procurement Manager", "Compares suppliers, prepares approval-bound purchase orders, and tracks verified receiving. Uses Inventory Manager findings for restock need.", "Procurement Manager", "Procurement",
            ["Monitor purchasing requirements from inventory and open POs", "Identify suitable suppliers from business records", "Compare supplier prices from stored or labeled research", "Prepare fingerprint-bound purchase orders", "Track pending purchases and delayed deliveries", "Monitor procurement costs against recommendations", "Verify receiving before stock changes"],
            ["GetInventoryLevels", "GetLowStockProducts", "GetInventoryRisks", "GetProcurementOverview", "GetSupplierDetails", "CompareSupplierPrices", "ResearchSuppliers", "GetPurchaseHistory", "ForecastProductDemand", "GetProcurementRecommendations", "GetDelayedDeliveries", "GetSlowMovingProducts", "GetProcurementBudgetNeed", "CreatePurchaseOrderDraft", "PreparePurchaseOrder", "GetPendingPurchaseOrders", "CreatePurchaseOrder", "SubmitPreparedPurchaseOrder", "ReceivePurchaseOrderVerified", "VerifyOrderReadiness", "GetOrderDetails", "RetrieveSharedBusinessContext", "StoreSharedBusinessDecision"],
            ["inventory-automation", "procurement-daily-monitor", "delivery-followup", "low-stock-to-procurement", "order-to-fulfillment", "complaint-to-resolution"], true),
        Agent("assistant", "AI Executive Assistant", "Organizes internal tasks, reminders, and drafts. It does not send mail or invitations.", "Executive assistant", "Administration",
            ["List upcoming tasks", "Store reminders and calendar notes", "Draft emails and meeting summaries"],
            ["GetUpcomingTasks", "CreateTask", "GetCalendarEvents", "CreateCalendarEvent", "SummarizeMeetingNotes", "CreateEmailDraft", "CreateReminder", "GenerateDailySummary", "SendExternalEmail"],
            ["executive-reporting"], true),
        Agent("content", "Content Creator", "Drafts articles and marketing copy for human editing.", "Content creator", "Marketing",
            ["Draft articles, newsletters, and website copy from supplied notes and company knowledge"],
            ["GetCompanyProfile", "GetMarketingBrandProfile", "SearchKnowledgeBase", "CreateContentDraft", "CreateContentCalendar", "GenerateMarketingGraphic"],
            ["content-generation"], false, true),
        Agent("seo", "SEO Agent", "Prepares on-page recommendations from stored content. Search Console is not connected.", "SEO agent", "Marketing",
            ["Review stored content", "Draft keyword and on-page recommendations", "Avoid invented search volumes"],
            ["SearchKnowledgeBase", "GetCompanyProfile", "CreateContentDraft", "ResearchMarket"],
            ["seo-research"], false, true),
        Agent("social", "Social Media Agent", "Drafts platform-specific posts. Publishing waits for approval and a connected Meta Page or sandbox job.", "Social media agent", "Marketing",
            ["Draft posts", "Build a publishing calendar", "Schedule approved posts", "Report verified activity only"],
            ["GetMarketingBrandProfile", "CreateContentDraft", "CreateCampaignDraft", "CreateContentCalendar", "GenerateMarketingGraphic", "ScheduleApprovedMarketingPost", "GetCampaignAnalytics", "PublishSocialPost"],
            ["social-campaign", "autonomous-marketing-weekly"], false),
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
            ["GetInventoryLevels", "GetOrderDetails", "GetSalesSummary", "CreateContentDraft", "GetCustomerDetails", "VerifyOrderReadiness"],
            ["order-to-fulfillment"], false),
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
        new("customer-acquisition", "Customer acquisition", "Research authorized prospects, qualify leads, prepare outreach drafts, and report to the CEO. External sending stops for approval.", "Sales", "Manual",
        [
            Step("market-research", "marketing", "Summarize stored segments and customers. Do not invent a market.", "GetCustomerSegments", "ResearchMarket"),
            Step("lead-generation", "sales", "Research authorized prospects and create CRM leads without inventing contacts.", "ResearchExternalProspects", "SearchAuthorizedLeadSources", "FindDuplicateLeads"),
            Step("lead-qualification", "sales", "Qualify researched prospects with an explicit estimate score.", "QualifyLead", "GetLeadDetails", "UpdateLead"),
            Step("marketing-preparation", "marketing", "Draft campaign copy from the stored segments.", "CreateContentDraft", "CreateCampaignDraft"),
            Step("outreach-preparation", "sales", "Draft personalized outreach and request approval before any send.", "CreateOutreachCampaign", "GeneratePersonalizedOutreach", "CreateFollowUp", "SendExternalEmail"),
            Step("executive-report", "ceo", "Summarize the recorded pipeline and workforce activity.", "GetSalesSummary", "GetSalesAgentStatus", "GetAgentExecutionResults")
        ]),
        new("autonomous-lead-generation", "Autonomous lead generation", "Daily Sales Agent loop: research, qualify, draft outreach, and report. Sends still require approval.", "Sales", "Scheduled",
        [
            Step("status", "sales", "Read Sales Agent status and limits.", "GetSalesAgentStatus", "GetCompanyProfile"),
            Step("research", "sales", "Research a bounded batch of authorized prospects.", "ResearchExternalProspects"),
            Step("qualify", "sales", "Qualify newly researched prospects.", "QualifyLead", "FindDuplicateLeads"),
            Step("outreach", "sales", "Prepare personalized outreach drafts and stop before send.", "CreateOutreachCampaign", "GeneratePersonalizedOutreach", "SendExternalEmail"),
            Step("report", "sales", "Store a campaign report from persisted records.", "GenerateSalesCampaignReport")
        ], null, 1440),
        new("content-generation", "Content generation", "Draft content from the company profile and authorized knowledge.", "Marketing", "Manual",
        [
            Step("sources", "content", "Read the company profile and authorized knowledge.", "GetCompanyProfile", "SearchKnowledgeBase"),
            Step("draft", "content", "Store a content draft.", "CreateContentDraft")
        ]),
        new("social-campaign", "Social campaign", "Brand → strategy → calendar → content → graphic → publish approval.", "Marketing", "Manual",
        [
            Step("brand", "marketing", "Load or seed the brand profile.", "GetMarketingBrandProfile"),
            Step("strategy", "marketing", "Draft a strategy grounded in brand facts.", "GenerateMarketingStrategy"),
            Step("draft", "social", "Draft the campaign, calendar, and a content item.", "CreateCampaignDraft", "CreateContentCalendar", "CreateContentDraft"),
            Step("graphic", "social", "Prepare a graphic or visual brief.", "GenerateMarketingGraphic"),
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
        new("competitor-research", "Competitor research", "Use only competitor notes and brand profile competitors JSON.", "Marketing", "Manual",
        [
            Step("notes", "marketing", "Create a competitor research report from authorized sources.", "ResearchCompetitors")
        ]),
        new("marketing-performance", "Marketing performance", "Sync verified analytics and report recorded campaign activity.", "Marketing", "Manual",
        [
            Step("sync", "marketing", "Sync verified or unavailable analytics snapshots.", "SyncMarketingAnalytics"),
            Step("campaign-numbers", "marketing", "Read recorded campaign activity and verified snapshots.", "GetCampaignAnalytics", "GenerateMarketingPerformanceReport"),
            Step("sales-numbers", "analyst", "Read recorded sales analytics.", "GetSalesAnalytics", "IdentifyBusinessTrends"),
            Step("report", "analyst", "Store the report.", "GenerateBusinessReport")
        ]),
        new("autonomous-marketing-weekly", "Autonomous marketing weekly", "Weekly brand-aware content drafts that still stop before external publish.", "Marketing", "Scheduled",
        [
            Step("brand", "marketing", "Load brand profile.", "GetMarketingBrandProfile"),
            Step("calendar", "marketing", "Draft the next week of content.", "CreateContentCalendar", "CreateContentDraft"),
            Step("share", "marketing", "Share structured insights with sales.", "ShareMarketingInsightsWithSales"),
            Step("publish-check", "social", "Request approval before any publish.", "PublishSocialPost")
        ], null, 10080),
        new("navla-marketing-campaign", "Navla marketing campaign", "CEO assigns a marketing objective; sales shares segments; marketing drafts; publish waits for approval; then report back.", "Marketing", "Manual",
        [
            Step("ceo-objective", "ceo", "Understand the company context and assign a marketing objective.", "GetCompanyOverview", "AssignMarketingObjective"),
            Step("sales-segments", "sales", "Share stored customer segments with marketing.", "GetCustomerSegments"),
            Step("marketing-plan", "marketing", "Load brand, draft strategy, campaign, calendar, and content.", "GetMarketingBrandProfile", "GenerateMarketingStrategy", "CreateCampaignDraft", "CreateContentCalendar", "CreateContentDraft"),
            Step("marketing-graphic", "marketing", "Prepare a graphic or visual brief.", "GenerateMarketingGraphic"),
            Step("publish-check", "social", "Request approval before any publish.", "PublishSocialPost"),
            Step("marketing-report", "marketing", "Sync verified analytics and share insights with sales.", "SyncMarketingAnalytics", "GenerateMarketingPerformanceReport", "ShareMarketingInsightsWithSales"),
            Step("ceo-summary", "ceo", "Summarize workforce results for the owner.", "GetAgentExecutionResults", "GenerateDailySummary")
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
        new("inventory-automation", "Low stock monitoring", "Inventory Manager verifies low stock; Procurement compares suppliers and stops at purchase approval.", "Procurement", "Event",
        [
            Step("stock", "inventory", "Verify stock risks from recorded inventory.", "GetInventoryRisks", "GetLowStockProducts", "GetInventoryLevels"),
            Parallel("demand", "inventory", "Analyze demand only from recorded sales.", 1, "ForecastProductDemand", "CalculateReorderRecommendations"),
            Parallel("suppliers", "procurement", "Compare stored suppliers and labeled research quotes.", 1, "ResearchSuppliers", "CompareSupplierPrices"),
            Step("draft-po", "procurement", "Prepare a fingerprint-bound purchase draft. Do not transmit it.", "PreparePurchaseOrder"),
            Step("approve-po", "procurement", "Ask a person before submitting the pending purchase.", "SubmitPreparedPurchaseOrder")
        ], NalvaEventNames.StockBelowThreshold),
        new("procurement-daily-monitor", "Daily inventory monitor", "Inventory Manager scans stock risks; Procurement estimates spend need once per day.", "Procurement", "Scheduled",
        [
            Step("risks", "inventory", "Read inventory risks from recorded stock and sales.", "GetInventoryRisks", "GetInventorySummary"),
            Step("demand", "inventory", "Label demand insights from recorded history.", "ForecastProductDemand", "CalculateReorderRecommendations"),
            Step("budget", "procurement", "Estimate procurement spend from recommendations.", "GetProcurementBudgetNeed", "GetProcurementOverview")
        ], null, 1440),
        new("delivery-followup", "Delivery follow-up", "Surface delayed purchase deliveries for owner attention.", "Procurement", "Event",
        [
            Step("delayed", "procurement", "List delayed deliveries from expected dates on open purchases.", "GetDelayedDeliveries")
        ], NalvaEventNames.DeliveryDelayed),
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
        new("customer-support-automation", "Customer support", "Ingest an inbound message, answer from authorized knowledge or orders, and escalate complex cases.", "Customer Support", "Event",
        [
            Step("ingest", "support", "Receive the inbound support message and create or update the ticket.", "ReceiveSupportMessage", "CreateSupportTicket"),
            Step("knowledge", "support", "Search authorized knowledge for this company.", "SearchKnowledgeBase"),
            Step("context", "support", "Read permitted customer and order records.", "GetCustomerDetails", "GetOrderDetails", "GetCustomerHistory"),
            Step("reply", "support", "Send an authorized routine reply or store a draft.", "SendSupportReply", "CreateResponseDraft"),
            Step("escalate", "support", "Escalate complex, financial, or low-confidence cases. Do not refund.", "EscalateTicket")
        ], NalvaEventNames.SupportTicketCreated),
        new("overdue-payment-followup", "Overdue payment follow-up", "Read overdue receivables, draft a reminder, schedule a follow-up, and stop before sending external mail.", "Finance", "Event",
        [
            Step("receivables", "finance", "Read outstanding receivables and invoice records. Do not invent balances.", "GetOutstandingReceivables", "GetReceivablesAging", "GetInvoiceRecords"),
            Step("reminder-draft", "finance", "Prepare a fingerprint-bound payment reminder draft from the outstanding invoice balance.", "PreparePaymentReminder"),
            Step("send-check", "finance", "Request approval before any external reminder is sent. Delivery remains blocked without a connected channel.", "SubmitPaymentReminder"),
            Step("summary", "ceo", "Summarize recorded workforce activity for this company.", "GetAgentExecutionResults")
        ], NalvaEventNames.InvoiceOverdue),

        // Phase 5F — multi-agent collaboration workflows
        new("lead-to-customer-collaboration", "Lead to customer (collaboration)", "Sales researches and qualifies, marketing prepares content, outreach waits for approval, support handles replies, conversion and invoice drafts use verified records only.", "Sales", "Manual",
        [
            Step("research", "sales", "Research authorized prospects. Do not invent contacts.", "ResearchExternalProspects", "SearchAuthorizedLeadSources"),
            Step("qualify", "sales", "Qualify and score researched prospects with estimate labels.", "QualifyLead", "FindDuplicateLeads"),
            Step("dedupe", "ceo", "Confirm duplicate checks against CRM before outreach.", "GetAgentExecutionResults"),
            Step("marketing-content", "marketing", "Prepare personalized content drafts from stored segments when useful.", "GetCustomerSegments", "CreateContentDraft"),
            Step("outreach", "sales", "Prepare personalized outreach drafts and stop before send.", "CreateOutreachCampaign", "GeneratePersonalizedOutreach", "SendExternalEmail"),
            Step("reply-track", "sales", "Track replies and schedule follow-ups from persisted records.", "ProcessProspectReply", "CreateFollowUp"),
            Step("inbound-support", "support", "Handle inbound prospect inquiries through support tooling.", "ReceiveSupportMessage", "CreateSupportTicket", "CreateResponseDraft"),
            Step("convert", "sales", "Convert only after CRM approval. Do not claim conversion without records.", "ConvertLead"),
            Step("invoice", "finance", "Prepare an invoice draft only when verified transaction details exist.", "PrepareInvoiceDraft", "GetInvoiceRecords"),
            Step("sales-report", "sales", "Store a campaign report from persisted Sales Agent records.", "GenerateSalesCampaignReport"),
            Step("report", "ceo", "Report verified lead-to-customer outcomes and store the decision.", "GetAgentExecutionResults", "StoreSharedBusinessDecision")
        ]),
        new("order-to-fulfillment", "Customer order to fulfillment", "Support captures the request, sales and inventory verify, finance checks payment posture, fulfillment waits for approval, stock changes use verified inventory logic only.", "Sales", "Manual",
        [
            Step("capture", "support", "Capture and validate the customer order request.", "ReceiveSupportMessage", "CreateSupportTicket", "GetCustomerDetails"),
            Step("verify-customer", "sales", "Verify customer and related commercial context from CRM.", "GetCustomerDetails", "GetSalesPipeline"),
            Step("verify-order", "ecommerce", "Read the stored order. Do not invent order lines.", "GetOrderDetails", "VerifyOrderReadiness"),
            Step("stock", "procurement", "Check product availability from recorded inventory.", "GetInventoryLevels", "GetInventoryRisks"),
            Step("finance-check", "finance", "Verify pricing posture, payments, and outstanding balances from records.", "GetPaymentRecords", "GetOutstandingReceivables", "VerifyOrderReadiness"),
            Step("approval", "ceo", "Coordinate owner approval before fulfillment commitments.", "GetAgentStatus", "StoreSharedBusinessDecision"),
            Step("customer-update", "support", "Send authorized order updates or store drafts when channels are unavailable.", "CreateResponseDraft", "SendSupportReply"),
            Step("report", "ceo", "Report order status and blockers from verified step results.", "GetAgentExecutionResults", "GenerateDailySummary")
        ], NalvaEventNames.OrderPlaced),
        new("low-stock-to-procurement", "Low stock to procurement", "Inventory Manager detects shortage; Procurement compares suppliers; Finance reviews obligations; owner approves the exact PO; receiving updates stock only after verified quantities.", "Procurement", "Event",
        [
            Step("detect", "inventory", "Detect shortage from recorded stock levels and risks.", "GetInventoryRisks", "GetLowStockProducts", "GetInventoryLevels"),
            Step("reorder", "inventory", "Calculate reorder quantity from recommendations.", "CalculateReorderRecommendations", "ForecastProductDemand"),
            Step("pending-pos", "procurement", "Review open inbound purchase orders for the shortage.", "GetPendingPurchaseOrders"),
            Step("suppliers", "procurement", "Identify suppliers and compare quotations from stored or labeled research.", "ResearchSuppliers", "CompareSupplierPrices"),
            Step("budget", "procurement", "Estimate procurement spend from current recommendations.", "GetProcurementBudgetNeed"),
            Step("finance-impact", "finance", "Check financial implications and outstanding payables.", "GetOutstandingPayables", "GetFinancialSummary"),
            Step("recommend", "ceo", "Prepare a procurement recommendation for the owner.", "GetAgentExecutionResults", "StoreSharedBusinessDecision"),
            Step("approve-po", "procurement", "Submit the fingerprint-bound purchase only after owner approval.", "PreparePurchaseOrder", "SubmitPreparedPurchaseOrder"),
            Step("delivery", "procurement", "Track delayed deliveries from expected dates.", "GetDelayedDeliveries"),
            Step("receive", "procurement", "Verify received quantities before stock changes.", "ReceivePurchaseOrderVerified"),
            Step("payable", "finance", "Update payable context from verified purchase documents. Does not invent supplier invoices.", "GetOutstandingPayables", "PrepareInvoiceDraft"),
            Step("report", "ceo", "Report procurement outcome honestly.", "GetAgentExecutionResults", "GenerateDailySummary")
        ], NalvaEventNames.StockBelowThreshold),
        new("complaint-to-resolution", "Customer complaint to resolution", "Support classifies the complaint, inventory and finance contribute verified facts, refunds pause for approval, resolution is recorded.", "Customer Support", "Event",
        [
            Step("receive", "support", "Receive the complaint and identify customer and related order.", "ReceiveSupportMessage", "CreateSupportTicket", "GetCustomerDetails", "GetOrderDetails"),
            Step("history", "support", "Retrieve order and communication history.", "GetCustomerHistory", "SearchKnowledgeBase"),
            Step("classify", "support", "Classify complaint and urgency.", "ClassifySupportComplaint", "UpdateSupportTicket"),
            Parallel("inventory-check", "procurement", "Check product or fulfillment issues when relevant.", 1, "GetInventoryLevels", "GetOrderDetails"),
            Parallel("finance-check", "finance", "Check payment or invoice records when relevant.", 1, "GetPaymentRecords", "GetInvoiceRecords"),
            Step("assign", "ceo", "Assign required follow-up tasks and store shared context.", "CreateAgentTask", "StoreSharedBusinessDecision"),
            Step("resolution-draft", "support", "Prepare a resolution response draft.", "CreateResponseDraft", "EscalateTicket"),
            Step("refund-gate", "support", "Request owner approval for refunds or financial adjustments.", "IssueRefund"),
            Step("execute", "support", "Execute the authorized resolution and follow up.", "ResolveSupportTicket", "SendSupportReply", "ScheduleSupportFollowUp"),
            Step("report", "ceo", "Record the verified resolution outcome.", "GetAgentExecutionResults", "StoreSharedBusinessDecision")
        ], NalvaEventNames.SupportTicketCreated),
        new("weekly-business-review", "Weekly business review", "Collect verified metrics from sales, marketing, support, inventory, and finance; Navla consolidates risks and recommendations without inventing numbers.", "Executive Management", "Scheduled",
        [
            Parallel("sales-report", "sales", "Report actual leads, conversions, and sales from records.", 1, "GetSalesPipeline", "GetConversionAnalytics", "GetSalesAgentStatus", "GenerateSalesCampaignReport"),
            Parallel("marketing-report", "marketing", "Report campaign activity and available analytics.", 1, "SyncMarketingAnalytics", "GetCampaignAnalytics", "GenerateMarketingPerformanceReport"),
            Parallel("support-report", "support", "Report tickets and unresolved issues from persisted tickets.", 1, "GetCustomerHistory", "RetrieveSharedBusinessContext"),
            Parallel("inventory-report", "procurement", "Report low stock, overstock, and procurement risks.", 1, "GetInventoryRisks", "GetProcurementOverview", "GetDelayedDeliveries"),
            Parallel("finance-report", "finance", "Report revenue, expenses, receivables, payables, and cash-flow indicators.", 1, "GetFinancialSummary", "GetOutstandingReceivables", "GetOutstandingPayables", "ForecastCashFlow", "GenerateFinancialReport"),
            Step("consolidate", "analyst", "Consolidate recorded analytics and identify trends.", "IdentifyBusinessTrends", "GenerateBusinessReport"),
            Step("navla-report", "ceo", "Prepare the weekly business report for the owner from verified agent results.", "GetCompanyOverview", "GetBusinessKPIs", "GetAgentExecutionResults", "GenerateDailySummary", "StoreSharedBusinessDecision")
        ], null, 10080),

        // Phase 8D — Autopilot cross-agent scenarios
        new("revenue-growth-collaboration", "Revenue growth collaboration", "Sales finds opportunities from stored customers; Marketing drafts targeted campaigns; Support prepares FAQs; Finance measures actual revenue impact only from records.", "Sales", "Manual",
        [
            Step("sales-analysis", "sales", "Analyze stored customers and sales history for opportunities and inactive accounts.", "GetSalesPipeline", "GetCustomerSegments", "GetConversionAnalytics"),
            Step("sales-outreach", "sales", "Prepare personalized outreach drafts and stop before external send.", "GeneratePersonalizedOutreach", "CreateFollowUp", "SendExternalEmail"),
            Step("marketing-campaign", "marketing", "Draft a targeted campaign and content calendar from stored segments.", "GetCustomerSegments", "GenerateMarketingStrategy", "CreateCampaignDraft", "CreateContentCalendar", "CreateContentDraft"),
            Step("support-faq", "support", "Prepare product FAQ drafts from authorized knowledge.", "SearchKnowledgeBase", "CreateResponseDraft"),
            Step("sales-track", "sales", "Track leads and conversion metrics from persisted records.", "GetSalesPipeline", "GetConversionAnalytics", "GenerateSalesCampaignReport"),
            Step("finance-impact", "finance", "Measure actual revenue impact from recorded finance data only.", "GetFinancialSummary", "GetSalesSummary", "GenerateFinancialReport"),
            Step("ceo-summary", "ceo", "Report verified outcomes without inventing lift claims.", "GetAgentExecutionResults", "GenerateDailySummary", "StoreSharedBusinessDecision")
        ]),
        new("product-launch-collaboration", "New product launch collaboration", "Marketing prepares launch content; Inventory verifies stock; Sales prepares offers; Support prepares product info; Finance evaluates margin; owner approves launch.", "Marketing", "Manual",
        [
            Step("marketing-launch", "marketing", "Create launch campaign drafts and calendar from brand and product records.", "GetMarketingBrandProfile", "GenerateMarketingStrategy", "CreateCampaignDraft", "CreateContentCalendar", "CreateContentDraft", "GenerateMarketingGraphic"),
            Step("inventory-check", "inventory", "Verify stock availability from recorded inventory levels.", "GetInventoryLevels", "GetInventoryRisks", "GetLowStockProducts"),
            Step("sales-offers", "sales", "Prepare quotations and offers for existing customers only.", "CreateQuotationDraft", "CreateProposalDraft", "CreateOutreachCampaign"),
            Step("support-info", "support", "Prepare product information drafts from authorized knowledge.", "SearchKnowledgeBase", "CreateResponseDraft"),
            Step("finance-margin", "finance", "Evaluate pricing and margin from recorded costs and prices. Do not invent numbers.", "GetFinancialSummary", "GetExpenseRecords", "GenerateFinancialReport"),
            Step("publish-gate", "social", "Request approval before any external publish.", "PublishSocialPost"),
            Step("owner-approve", "ceo", "Store the launch decision for owner review.", "GetAgentExecutionResults", "StoreSharedBusinessDecision", "GenerateDailySummary")
        ])
    ];

    public static bool TryGet(string? key, out NalvaWorkflowTemplate? template)
    {
        template = All.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
        return template is not null;
    }
}
