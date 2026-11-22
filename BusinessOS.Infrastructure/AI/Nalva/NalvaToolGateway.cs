using System.Text.Json;
using System.Text.Json.Nodes;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Integrations;
using BusinessOS.Application.Features.Marketing;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Application.Features.Operations.Services;
using BusinessOS.Application.Features.BusinessIntelligence;
using BusinessOS.Application.Features.FinanceAgent;
using BusinessOS.Application.Features.ProcurementAgent;
using BusinessOS.Application.Features.SalesAgent;
using BusinessOS.Application.Features.Support;
using BusinessOS.Application.Features.Enterprise;
using BusinessOS.Application.Features.Autopilot;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.AI.SalesAgent;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI.Nalva;

public interface INalvaToolGateway
{
    Task<NalvaToolOutcome> ExecuteAsync(
        string toolName,
        string argsJson,
        string agentKey,
        Guid executionId,
        string delegationChain,
        int depth,
        bool approvalGranted,
        CancellationToken cancellationToken = default);
}

public sealed class NalvaToolGateway : INalvaToolGateway
{
    private readonly BusinessOSDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly ICrmOperationsService _crm;
    private readonly INalvaEventOutbox? _events;
    private readonly IIntegrationPlatform? _integrations;
    private readonly ISupportAgentService? _support;
    private readonly ISalesAgentToolOperations? _sales;
    private readonly IInventoryProcurementAgentService? _procurement;
    private readonly IMarketingOperationsService? _marketing;
    private readonly IFinanceAgentService? _finance;
    private readonly ISalesIntelligenceService? _salesIntel;
    private readonly ICustomerIntelligenceService? _customerIntel;
    private readonly IExecutiveIntelligenceService? _executive;
    private readonly IBusinessIntelligenceService? _bi;
    private readonly IEnterpriseAgentGovernanceService? _agentGovernance;
    private readonly IApprovalGovernanceService? _approvalGovernance;
    private readonly IAutonomySafetyEngine? _safety;

    public NalvaToolGateway(
        BusinessOSDbContext db,
        ITenantProvider tenant,
        ICrmOperationsService crm,
        INalvaEventOutbox? events = null,
        IIntegrationPlatform? integrations = null,
        ISupportAgentService? support = null,
        ISalesAgentToolOperations? sales = null,
        IInventoryProcurementAgentService? procurement = null,
        IMarketingOperationsService? marketing = null,
        IFinanceAgentService? finance = null,
        ISalesIntelligenceService? salesIntel = null,
        ICustomerIntelligenceService? customerIntel = null,
        IExecutiveIntelligenceService? executive = null,
        IBusinessIntelligenceService? bi = null,
        IEnterpriseAgentGovernanceService? agentGovernance = null,
        IApprovalGovernanceService? approvalGovernance = null,
        IAutonomySafetyEngine? safety = null)
    {
        _db = db;
        _tenant = tenant;
        _crm = crm;
        _events = events;
        _integrations = integrations;
        _support = support;
        _sales = sales;
        _procurement = procurement;
        _marketing = marketing;
        _finance = finance;
        _salesIntel = salesIntel;
        _customerIntel = customerIntel;
        _executive = executive;
        _bi = bi;
        _agentGovernance = agentGovernance;
        _approvalGovernance = approvalGovernance;
        _safety = safety;
    }

    public async Task<NalvaToolOutcome> ExecuteAsync(
        string toolName,
        string argsJson,
        string agentKey,
        Guid executionId,
        string delegationChain,
        int depth,
        bool approvalGranted,
        CancellationToken cancellationToken = default)
    {
        if (!_tenant.HasTenant())
            return Fail("Company context is required.");
        if (!NalvaToolCatalog.TryGet(toolName, out var tool) || tool is null)
            return Fail("That tool is not registered.");
        if (NalvaGuard.RejectsArguments(argsJson, out var rejected))
            return Fail(rejected);

        var args = NalvaArgs.Parse(argsJson);

        if (_agentGovernance is not null)
        {
            try
            {
                await _agentGovernance.EnsureToolAllowedAsync(
                    agentKey,
                    tool.Name,
                    NalvaArgs.GuidOf(args, "departmentId"),
                    cancellationToken);
            }
            catch (ForbiddenException ex)
            {
                return Fail(ex.Message);
            }
        }

        AutonomyDecision? safetyDecision = null;
        if (_safety is not null)
        {
            safetyDecision = await _safety.EvaluateToolAsync(agentKey, tool.Name, argsJson, approvalGranted, cancellationToken);
            if (safetyDecision.Blocked)
                return Fail(safetyDecision.Reason);
        }

        var settings = await _db.NalvaCompanySettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var extraApproval = settings is not null
            && NalvaJson.Strings(settings.ExtraApprovalToolsJson).Contains(tool.Name, StringComparer.OrdinalIgnoreCase);
        var forceExternalApproval = settings?.RequireApprovalForExternalActions != false
            && (tool.ExternalEffect || tool.Integration is "gmail" or "meta" or "external" or "prepared");
        var requiresApproval = tool.RequiresApproval || extraApproval || (forceExternalApproval && tool.IsWrite && tool.ExternalEffect)
            || (safetyDecision?.RequireApproval == true && !approvalGranted);

        if (_approvalGovernance is not null && requiresApproval && !approvalGranted)
        {
            await _approvalGovernance.EvaluateAndEnqueueAsync(new ApprovalEvaluateRequest
            {
                TriggerType = ApprovalTriggerTypes.AgentTool,
                SubjectType = "NalvaTool",
                SubjectId = executionId.ToString(),
                Title = $"Agent {agentKey} requests {tool.Name}",
                PayloadJson = argsJson,
                ToolName = tool.Name,
                Amount = NalvaArgs.Dec(args, "amount")
            }, cancellationToken);
        }

        if (requiresApproval && !approvalGranted)
        {
            return new NalvaToolOutcome
            {
                ApprovalRequired = true,
                ApprovalAction = tool.ApprovalAction ?? tool.Name,
                Risk = tool.Risk,
                Summary = $"Waiting for a person to approve {tool.ApprovalAction ?? tool.Name}.",
                Data = new JsonObject
                {
                    ["tool"] = tool.Name,
                    ["action"] = tool.ApprovalAction ?? tool.Name,
                    ["risk"] = tool.Risk
                }
            };
        }

        if (tool.RequiresApproval && tool.Integration == "prepared")
        {
            if (tool.Name == "SendExternalEmail" && _sales is not null)
            {
                var outreach = await _sales.TrySendOutreachEmailAsync(args, argsJson, true, cancellationToken);
                if (outreach is not null)
                    return outreach;
            }
            if (tool.Name == "PublishSocialPost" && _marketing is not null)
            {
                var scheduled = await MarketingPublishAfterApproval(args, cancellationToken);
                if (scheduled is not null)
                    return scheduled;
            }
            if (tool.Name == "SendSupportReply")
                return await SendSupportReplyTool(args, cancellationToken);
            if (tool.Name == "CreateCalendarEvent")
                return await CreateCalendarEventTool(agentKey, executionId, args, argsJson, cancellationToken);
            if (tool.Name == "SendWhatsAppMessage")
                return await SendWhatsAppMessageTool(argsJson, cancellationToken);
            if (tool.Name == "SubmitPaymentReminder" && _finance is not null)
            {
                var reminderId = NalvaArgs.GuidOf(args, "reminderId") ?? NalvaArgs.GuidOf(args, "id");
                if (reminderId is null)
                    return Fail("reminderId is required to submit a payment reminder.");
                var submitted = await _finance.SubmitPaymentReminderAsync(
                    reminderId.Value,
                    approved: true,
                    NalvaArgs.Str(args, "approvalFingerprint"),
                    NalvaArgs.Str(args, "idempotencyKey"),
                    cancellationToken);
                return Ok(submitted.Summary, JsonSerializer.SerializeToNode(submitted)?.AsObject() ?? new JsonObject
                {
                    ["reminderId"] = submitted.ReminderId.ToString(),
                    ["status"] = submitted.Status,
                    ["integrationClassification"] = submitted.IntegrationClassification
                });
            }
            var live = await TryLiveIntegrationAsync(tool.Name, argsJson, cancellationToken);
            if (live is not null)
                return live;
            return Ok(
                "Approved. No external system is connected, so Nalva did not send, publish, charge, refund, deploy, or change the record.",
                new JsonObject
                {
                    ["performed"] = false,
                    ["integration"] = "prepared",
                    ["action"] = tool.ApprovalAction
                });
        }

        try
        {
            return tool.Name switch
            {
                "GetCompanyOverview" => await CompanyOverview(cancellationToken),
                "GetBusinessKPIs" => await Kpis(cancellationToken),
                "GetSalesSummary" => await SalesSummary(cancellationToken),
                "GetFinancialSummary" => await FinanceOverviewTool(cancellationToken),
                "GetFinanceIntelligence" => await FinanceIntelligenceTool(cancellationToken),
                "AnalyzeExpenses" => await AnalyzeExpensesTool(cancellationToken),
                "GetReceivablesAging" => await ReceivablesAgingTool(cancellationToken),
                "GetPayablesSchedule" => await PayablesScheduleTool(cancellationToken),
                "ReconcileFinancialRecords" => await ReconcileTool(cancellationToken),
                "GetBudgetStatus" => await BudgetStatusTool(cancellationToken),
                "PreparePaymentReminder" => await PrepareReminderTool(args, cancellationToken),
                "GetProjectSummary" => await ProjectSummary(cancellationToken),
                "GetInventorySummary" => await InventorySummary(cancellationToken),
                "GetEmployeeSummary" => await EmployeeSummary(cancellationToken),
                "GetAgentStatus" => await AgentStatus(cancellationToken),
                "GetAgentExecutionResults" => await ExecutionResults(cancellationToken),
                "CreateAgentTask" or "DelegateAgentTask" => await Delegate(tool.Name, args, agentKey, delegationChain, depth, cancellationToken),
                "GetCompanyProfile" or "GetMarketingBrandProfile" => await CompanyProfile(cancellationToken),
                "GetTargetCustomers" => await Customers(cancellationToken),
                "GetCustomerSegments" => await Segments(cancellationToken),
                "ResearchMarket" => await ResearchMarket(agentKey, cancellationToken),
                "ResearchCompetitors" => await ResearchCompetitors(agentKey, cancellationToken),
                "GenerateMarketingStrategy" => await MarketingStrategy(agentKey, args, cancellationToken),
                "CreateContentDraft" => await MarketingContentDraft(agentKey, executionId, args, cancellationToken),
                "CreateContentCalendar" => await MarketingCalendar(agentKey, args, cancellationToken),
                "CreateCampaignDraft" => await MarketingCampaignDraft(agentKey, args, cancellationToken),
                "GetCampaignAnalytics" => await CampaignAnalytics(cancellationToken),
                "UpsertBrandProfile" => await UpsertBrandProfile(args, cancellationToken),
                "GenerateMarketingGraphic" => await MarketingGraphic(args, cancellationToken),
                "ScheduleApprovedMarketingPost" => await ScheduleMarketingPost(args, cancellationToken),
                "SyncMarketingAnalytics" => await SyncMarketingAnalytics(args, cancellationToken),
                "GenerateMarketingPerformanceReport" => await MarketingPerformanceReport(args, cancellationToken),
                "ShareMarketingInsightsWithSales" => await ShareMarketingInsights(agentKey, args, cancellationToken),
                "AssignMarketingObjective" => await AssignMarketingObjective(agentKey, executionId, args, cancellationToken),
                "SearchAuthorizedLeadSources" => await LeadSources(cancellationToken),
                "ResearchExternalProspects" => _sales is null
                    ? Ok("Sales research operations are unavailable.", new JsonObject { ["researched"] = 0 })
                    : await _sales.ResearchExternalProspectsAsync(agentKey, executionId, args, cancellationToken),
                "QualifyLead" => _sales is null
                    ? Ok("Sales qualification is unavailable.", new JsonObject { ["qualified"] = false })
                    : await _sales.QualifyProspectAsync(args, cancellationToken),
                "CreateOutreachCampaign" => _sales is null
                    ? Ok("Outreach campaigns are unavailable.", new JsonObject { ["created"] = false })
                    : await _sales.CreateOutreachCampaignAsync(agentKey, executionId, args, cancellationToken),
                "GeneratePersonalizedOutreach" => _sales is null
                    ? Ok("Personalized outreach is unavailable.", new JsonObject { ["created"] = false })
                    : await _sales.GeneratePersonalizedOutreachAsync(agentKey, executionId, args, cancellationToken),
                "GetSalesAgentStatus" => _sales is null
                    ? Ok("Sales Agent status is unavailable.", new JsonObject())
                    : await _sales.GetSalesAgentStatusAsync(cancellationToken),
                "AssignSalesObjective" => _sales is null
                    ? Ok("Sales objectives are unavailable.", new JsonObject { ["assigned"] = false })
                    : await _sales.AssignSalesObjectiveAsync(agentKey, executionId, args, cancellationToken),
                "ProcessProspectReply" => _sales is null
                    ? Ok("Prospect reply processing is unavailable.", new JsonObject { ["processed"] = false })
                    : await _sales.ProcessProspectReplyAsync(args, cancellationToken),
                "GenerateSalesCampaignReport" => _sales is null
                    ? Ok("Sales campaign reports are unavailable.", new JsonObject())
                    : await _sales.GenerateSalesCampaignReportAsync(agentKey, executionId, args, cancellationToken),
                "ResearchProspect" or "GetLeadDetails" => await LeadDetails(args, cancellationToken),
                "CreateLead" => await CreateLead(args, cancellationToken),
                "UpdateLead" => await UpdateLead(args, cancellationToken),
                "CreateDeal" => await CreateDeal(args, cancellationToken),
                "CreateFollowUp" => await CreateFollowUp(agentKey, args, cancellationToken),
                "CreateProposalDraft" => await Draft(agentKey, executionId, "Proposal", "Proposal draft", "Proposal draft. It was not sent.", cancellationToken),
                "CreateQuotationDraft" => await QuotationDraft(args, cancellationToken),
                "GetSalesPipeline" => await Pipeline(cancellationToken),
                "GetConversionAnalytics" => await Conversion(cancellationToken),
                "ForecastSalesRevenue" => await ForecastSales(cancellationToken),
                "GetSalesIntelligence" => await SalesIntel(cancellationToken),
                "GetCustomerIntelligence" => await CustomerIntel(cancellationToken),
                "GetExecutiveDecisionBrief" => await ExecutiveBrief(args, cancellationToken),
                "GetBusinessRisks" => await BusinessRisks(cancellationToken),
                "SendExternalEmail" or "PublishSocialPost" or "SpendAdBudget" or "IssueRefund" or "ExecuteProductionDeployment" or "ChangeEmployeeRecord" =>
                    Ok("No external effect was performed.", new JsonObject { ["performed"] = false }),
                "SearchKnowledgeBase" or "GetJobRequirements" => await KnowledgeSearch(agentKey, NalvaArgs.Str(args, "query"), cancellationToken),
                "GetCustomerDetails" => await CustomerDetails(args, cancellationToken),
                "GetOrderDetails" => await OrderDetails(args, cancellationToken),
                "GetCustomerHistory" => await CustomerHistory(args, cancellationToken),
                "CreateSupportTicket" => await SupportTicket(agentKey, executionId, args, cancellationToken),
                "UpdateSupportTicket" => await UpdateSupportTicketTool(args, cancellationToken),
                "CreateResponseDraft" => await CreateSupportResponseDraft(agentKey, executionId, args, cancellationToken),
                "SendSupportReply" => await SendSupportReplyTool(args, cancellationToken),
                "EscalateTicket" => await EscalateSupportTicketTool(args, cancellationToken),
                "ResolveSupportTicket" => await ResolveSupportTicketTool(args, cancellationToken),
                "ScheduleSupportFollowUp" => await ScheduleSupportFollowUpTool(args, cancellationToken),
                "ReceiveSupportMessage" => await ReceiveSupportMessageTool(args, cancellationToken),
                "GetProjectDetails" or "GetProjectRequirements" => await ProjectDetails(args, tool.Name == "GetProjectRequirements", cancellationToken),
                "CreateDevelopmentTask" or "CreateTask" => await CreateTask(args, cancellationToken),
                "AnalyzeRepository" or "AnalyzePullRequest" => await RepositoryRead(tool.Name, argsJson, cancellationToken),
                "ReviewCodeChanges" or "GenerateCode" or "GenerateUnitTests" or "GenerateTechnicalDocumentation" or "GenerateCodeReviewReport" =>
                    await Draft(agentKey, executionId, "Engineering", tool.Name, EngineeringBody(tool.Name, args), cancellationToken),
                "GetExpenseRecords" => await AnalyzeExpensesTool(cancellationToken),
                "GetInvoiceRecords" => await Invoices(cancellationToken),
                "GetPaymentRecords" => await Payments(cancellationToken),
                "GetOutstandingReceivables" => await ReceivablesAgingTool(cancellationToken),
                "GetOutstandingPayables" => await PayablesScheduleTool(cancellationToken),
                "CalculateProfitability" => await FinanceIntelligenceTool(cancellationToken),
                "ForecastCashFlow" => await CashFlowTool(cancellationToken),
                "GenerateFinancialReport" => await FinanceReportTool(args, cancellationToken),
                "GenerateBusinessReport" or "GenerateHRReport" or "GenerateDailySummary" => await Report(agentKey, executionId, tool.Name, cancellationToken),
                "AnalyzeCandidateCV" or "CompareCandidateQualifications" => CompareText(args),
                "CreateInterviewQuestionSet" => await Draft(agentKey, executionId, "Interview", "Interview questions", InterviewBody(args), cancellationToken),
                "CreateOnboardingTask" => await Draft(agentKey, executionId, "Onboarding", NalvaArgs.Str(args, "title") ?? "Onboarding item", "Checklist item for a person to complete. This is not a hiring decision.", cancellationToken),
                "GetEmployeeDetails" => await EmployeeDetails(args, cancellationToken),
                "GetAttendanceSummary" => await Attendance(cancellationToken),
                "GetSalesAnalytics" => await SalesSummary(cancellationToken),
                "GetRevenueAnalytics" => await Revenue(cancellationToken),
                "GetInventoryAnalytics" or "GetInventoryLevels" or "GetLowStockProducts" => await InventoryDetails(tool.Name, cancellationToken),
                "GetInventoryRisks" => await ProcurementJson(async () => await _procurement!.GetInventoryRisksAsync(cancellationToken), "risks"),
                "GetProcurementOverview" => await ProcurementJson(async () => await _procurement!.GetOverviewAsync(cancellationToken), "overview"),
                "GetCustomerAnalytics" => await CustomerAnalytics(cancellationToken),
                "GetProjectAnalytics" => await ProjectSummary(cancellationToken),
                "GetExpenseAnalytics" => await ExpenseAnalytics(cancellationToken),
                "IdentifyBusinessTrends" => await Trends(cancellationToken),
                "GetSupplierDetails" => await SupplierDetails(args, cancellationToken),
                "CompareSupplierPrices" or "ResearchSuppliers" => await ResearchSuppliers(args, cancellationToken),
                "GetPurchaseHistory" or "GetPendingPurchaseOrders" => await Purchases(tool.Name == "GetPendingPurchaseOrders", cancellationToken),
                "ForecastProductDemand" => await ForecastDemand(args, cancellationToken),
                "GetProcurementRecommendations" or "CalculateReorderRecommendations" => await ProcurementJson(async () => await _procurement!.GetRecommendationsAsync(cancellationToken), "recommendations"),
                "GetDelayedDeliveries" => await DelayedDeliveries(cancellationToken),
                "GetSlowMovingProducts" => await SlowMoving(cancellationToken),
                "GetProcurementBudgetNeed" => await ProcurementBudget(cancellationToken),
                "CreatePurchaseOrderDraft" or "PreparePurchaseOrder" => await PreparePurchase(args, cancellationToken),
                "CreatePurchaseOrder" or "SubmitPreparedPurchaseOrder" => await SubmitPurchase(args, approvalGranted, cancellationToken),
                "ReceivePurchaseOrderVerified" => await ReceiveVerified(args, cancellationToken),
                "GetUpcomingTasks" or "GetCalendarEvents" => await Tasks(cancellationToken),
                "CreateCalendarEvent" => await CreateCalendarEventTool(agentKey, executionId, args, argsJson, cancellationToken),
                "SendWhatsAppMessage" => await SendWhatsAppMessageTool(argsJson, cancellationToken),
                "SummarizeMeetingNotes" => await Draft(agentKey, executionId, "Meeting", "Meeting summary", NalvaGuard.Clip(NalvaArgs.Str(args, "notes"), 2000), cancellationToken),
                "CreateEmailDraft" => await Draft(agentKey, executionId, "Email", NalvaArgs.Str(args, "subject") ?? "Email draft", NalvaArgs.Str(args, "body") ?? "Email draft. It was not sent.", cancellationToken),
                "CreateReminder" => await Draft(agentKey, executionId, "Reminder", NalvaArgs.Str(args, "title") ?? "Reminder", NalvaArgs.Str(args, "body") ?? "Internal reminder.", cancellationToken),
                "ConvertLead" => await Convert(args, cancellationToken),
                "IngestKnowledge" => await Ingest(agentKey, args, cancellationToken),
                "DeleteKnowledge" => await DeleteKnowledge(agentKey, args, cancellationToken),
                "GetChurnRisk" => await Churn(cancellationToken),
                "FindDuplicateLeads" => await Duplicates(cancellationToken),
                "GetOperationalAlerts" => await Alerts(cancellationToken),
                "RetrieveSharedBusinessContext" => await RetrieveSharedContext(args, cancellationToken),
                "StoreSharedBusinessDecision" => await StoreSharedDecision(agentKey, executionId, args, cancellationToken),
                "VerifyOrderReadiness" => await VerifyOrderReadiness(args, cancellationToken),
                "PrepareInvoiceDraft" => await PrepareInvoiceDraft(agentKey, executionId, args, cancellationToken),
                "ClassifySupportComplaint" => await ClassifySupportComplaint(args, cancellationToken),
                "CoordinateWorkforceObjective" => CoordinateObjective(args),
                _ => Fail("This tool is registered but has no executor.")
            };
        }
        catch (AppException ex)
        {
            return Fail(ex.Message);
        }
    }

    private Guid CompanyId => _tenant.TenantId;

    private async Task<NalvaToolOutcome> CompanyOverview(CancellationToken ct)
    {
        var data = new JsonObject
        {
            ["customers"] = await _db.Customers.CountAsync(ct),
            ["products"] = await _db.Products.CountAsync(ct),
            ["orders"] = await _db.Orders.CountAsync(ct),
            ["openLeads"] = await _db.Leads.CountAsync(x => x.Status != LeadStatuses.Converted && x.Status != LeadStatuses.Lost, ct),
            ["source"] = "company records"
        };
        return Ok("Company overview from stored records.", data);
    }

    private async Task<NalvaToolOutcome> Kpis(CancellationToken ct)
    {
        var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var data = new JsonObject
        {
            ["ordersThisMonth"] = await _db.Orders.CountAsync(x => x.OrderDate >= month, ct),
            ["orderValueThisMonth"] = await _db.Orders.Where(x => x.OrderDate >= month).SumAsync(x => (decimal?)x.GrandTotal, ct) ?? 0m,
            ["outstandingInvoices"] = await _db.Invoices.CountAsync(x => x.OutstandingAmount > 0, ct),
            ["lowStock"] = await _db.Products.CountAsync(x => x.IsActive && x.CurrentStock <= x.ReorderLevel, ct),
            ["openTasks"] = await _db.WorkTasks.CountAsync(x => x.Status != WorkTaskStatus.Done && x.Status != WorkTaskStatus.Cancelled, ct),
            ["historical"] = true
        };
        return Ok("KPIs from recorded activity.", data);
    }

    private async Task<NalvaToolOutcome> SalesSummary(CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-30);
        var rows = await _db.Orders.AsNoTracking().Where(x => x.OrderDate >= since).Select(x => new { x.Status, x.GrandTotal }).ToListAsync(ct);
        var byStatus = new JsonObject();
        foreach (var group in rows.GroupBy(x => x.Status))
            byStatus[group.Key] = group.Count();
        return Ok("Sales summary for the last 30 days.", new JsonObject
        {
            ["orders"] = rows.Count,
            ["total"] = rows.Sum(x => x.GrandTotal),
            ["byStatus"] = byStatus,
            ["periodDays"] = 30
        });
    }

    private async Task<NalvaToolOutcome> FinanceOverviewTool(CancellationToken ct)
    {
        if (_finance is null)
            return Fail("Finance agent service is unavailable.");
        var overview = await _finance.GetOverviewAsync(period: "month", cancellationToken: ct);
        return Ok("Finance overview from tenant records. Not a statutory statement.", JsonSerializer.SerializeToNode(overview)?.AsObject() ?? new JsonObject());
    }

    private async Task<NalvaToolOutcome> FinanceIntelligenceTool(CancellationToken ct)
    {
        if (_finance is null)
            return Fail("Finance agent service is unavailable.");
        var intel = await _finance.GetIntelligenceAsync(period: "month", cancellationToken: ct);
        return Ok("Finance intelligence with explicit formulas and limitations.", JsonSerializer.SerializeToNode(intel)?.AsObject() ?? new JsonObject());
    }

    private async Task<NalvaToolOutcome> AnalyzeExpensesTool(CancellationToken ct)
    {
        if (_finance is null)
            return Fail("Finance agent service is unavailable.");
        var data = await _finance.AnalyzeExpensesAsync(period: "month", cancellationToken: ct);
        return Ok("Expense analysis from Approved/Paid expenses only.", JsonSerializer.SerializeToNode(data)?.AsObject() ?? new JsonObject());
    }

    private async Task<NalvaToolOutcome> ReceivablesAgingTool(CancellationToken ct)
    {
        if (_finance is null)
            return Fail("Finance agent service is unavailable.");
        var data = await _finance.GetReceivablesAsync(ct);
        return Ok("Receivables aging from invoice outstanding balances.", JsonSerializer.SerializeToNode(data)?.AsObject() ?? new JsonObject());
    }

    private async Task<NalvaToolOutcome> PayablesScheduleTool(CancellationToken ct)
    {
        if (_finance is null)
            return Fail("Finance agent service is unavailable.");
        var data = await _finance.GetPayablesAsync(ct);
        return Ok("Supplier payables from purchase invoices. Open POs are not counted as payables.", JsonSerializer.SerializeToNode(data)?.AsObject() ?? new JsonObject());
    }

    private async Task<NalvaToolOutcome> CashFlowTool(CancellationToken ct)
    {
        if (_finance is null)
            return Fail("Finance agent service is unavailable.");
        var data = await _finance.GetCashFlowAsync(projectionDays: 30, cancellationToken: ct);
        return Ok("Cash-flow analysis with labeled projection confidence.", JsonSerializer.SerializeToNode(data)?.AsObject() ?? new JsonObject());
    }

    private async Task<NalvaToolOutcome> ReconcileTool(CancellationToken ct)
    {
        if (_finance is null)
            return Fail("Finance agent service is unavailable.");
        var data = await _finance.ReconcileAsync(ct);
        return Ok("Reconciliation summary. Records were not modified.", JsonSerializer.SerializeToNode(data)?.AsObject() ?? new JsonObject());
    }

    private async Task<NalvaToolOutcome> BudgetStatusTool(CancellationToken ct)
    {
        if (_finance is null)
            return Fail("Finance agent service is unavailable.");
        var budgets = await _finance.GetBudgetsAsync(ct);
        var alerts = await _finance.GetBudgetAlertsAsync(ct);
        return Ok("Owner-configured budget status.", new JsonObject
        {
            ["budgets"] = JsonSerializer.SerializeToNode(budgets),
            ["alerts"] = JsonSerializer.SerializeToNode(alerts)
        });
    }

    private async Task<NalvaToolOutcome> PrepareReminderTool(JsonElement args, CancellationToken ct)
    {
        if (_finance is null)
            return Fail("Finance agent service is unavailable.");
        var invoiceId = NalvaArgs.GuidOf(args, "invoiceId") ?? NalvaArgs.GuidOf(args, "id");
        if (invoiceId is null)
            return Fail("invoiceId is required.");
        var prepared = await _finance.PreparePaymentReminderAsync(new PrepareReminderRequest
        {
            InvoiceId = invoiceId.Value,
            Channel = NalvaArgs.Str(args, "channel"),
            CustomMessage = NalvaArgs.Str(args, "message") ?? NalvaArgs.Str(args, "customMessage"),
            IdempotencyKey = NalvaArgs.Str(args, "idempotencyKey")
        }, ct);
        return Ok(prepared.Summary, JsonSerializer.SerializeToNode(prepared)?.AsObject() ?? new JsonObject());
    }

    private async Task<NalvaToolOutcome> FinanceReportTool(JsonElement args, CancellationToken ct)
    {
        if (_finance is null)
            return Fail("Finance agent service is unavailable.");
        var report = await _finance.GenerateReportAsync(new FinanceReportRequest
        {
            ReportType = NalvaArgs.Str(args, "reportType") ?? NalvaArgs.Str(args, "type") ?? "monthly-profit-loss",
            Period = NalvaArgs.Str(args, "period") ?? "month"
        }, ct);
        return Ok(report.BodyMarkdown, new JsonObject
        {
            ["reportType"] = report.ReportType,
            ["currency"] = report.Currency,
            ["startDate"] = report.StartDate.ToString("O"),
            ["endDate"] = report.EndDate.ToString("O"),
            ["calculationBasis"] = report.CalculationBasis,
            ["disclaimer"] = report.Disclaimer,
            ["limitations"] = new JsonArray(report.Limitations.Select(x => (JsonNode)x).ToArray())
        });
    }

    private async Task<NalvaToolOutcome> ProjectSummary(CancellationToken ct)
    {
        var projects = await _db.Projects.AsNoTracking().Select(x => x.Status).ToListAsync(ct);
        var tasks = await _db.WorkTasks.CountAsync(x => x.Status != WorkTaskStatus.Done && x.Status != WorkTaskStatus.Cancelled, ct);
        return Ok("Project summary from stored records.", new JsonObject
        {
            ["projects"] = projects.Count,
            ["openTasks"] = tasks
        });
    }

    private async Task<NalvaToolOutcome> InventorySummary(CancellationToken ct)
    {
        var summary = await _db.Products.AsNoTracking()
            .Where(x => x.IsActive)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Products = g.Count(),
                Units = g.Sum(x => x.CurrentStock),
                LowStock = g.Sum(x => x.CurrentStock <= x.ReorderLevel ? 1 : 0)
            })
            .FirstOrDefaultAsync(ct);

        return Ok("Inventory summary from product stock.", new JsonObject
        {
            ["products"] = summary?.Products ?? 0,
            ["units"] = summary?.Units ?? 0m,
            ["lowStock"] = summary?.LowStock ?? 0
        });
    }

    private async Task<NalvaToolOutcome> EmployeeSummary(CancellationToken ct) =>
        Ok("Employee counts only.", new JsonObject
        {
            ["active"] = await _db.Employees.CountAsync(x => x.IsActive, ct),
            ["inactive"] = await _db.Employees.CountAsync(x => !x.IsActive, ct),
            ["includesSalaries"] = false
        });

    private async Task<NalvaToolOutcome> AgentStatus(CancellationToken ct)
    {
        var rows = await _db.NalvaCompanyAgents.AsNoTracking().Select(x => new { x.AgentKey, x.Status }).ToListAsync(ct);
        var list = new JsonArray();
        foreach (var row in rows)
            list.Add(new JsonObject { ["key"] = row.AgentKey, ["status"] = row.Status });
        return Ok("Agent activation for this company.", new JsonObject { ["agents"] = list });
    }

    private async Task<NalvaToolOutcome> ExecutionResults(CancellationToken ct)
    {
        var rows = await _db.NalvaExecutions.AsNoTracking().OrderByDescending(x => x.StartedAt).Take(10)
            .Select(x => new { x.AgentKey, x.Status, x.Summary }).ToListAsync(ct);
        var list = new JsonArray();
        foreach (var row in rows)
            list.Add(new JsonObject { ["agent"] = row.AgentKey, ["status"] = row.Status, ["summary"] = row.Summary });
        return Ok("Recent execution summaries.", new JsonObject { ["executions"] = list });
    }

    private async Task<NalvaToolOutcome> Delegate(string tool, JsonElement args, string agentKey, string chain, int depth, CancellationToken ct)
    {
        var target = NalvaArgs.Str(args, "agentKey") ?? NalvaArgs.Str(args, "targetAgent");
        if (string.IsNullOrWhiteSpace(target))
            return Fail("Delegation needs an agent key.");
        if (depth + 1 > NalvaLimits.MaxDelegationDepth)
            return Fail("Delegation depth limit reached.");
        if (chain.Split('>', StringSplitOptions.RemoveEmptyEntries).Contains(target, StringComparer.OrdinalIgnoreCase)
            || string.Equals(target, agentKey, StringComparison.OrdinalIgnoreCase))
            return Fail("That delegation would loop back to an agent already in this chain.");

        var companyAgent = await _db.NalvaCompanyAgents.AsNoTracking().FirstOrDefaultAsync(x => x.AgentKey == target, ct);
        if (companyAgent is null || companyAgent.Status != NalvaStatuses.Active)
            return Fail("The target agent is not active in this company.");
        if (companyAgent.TenantId != CompanyId)
            return Fail("Cross-company delegation was blocked.");

        var requested = NalvaArgs.Strings(args, "tools");
        var granted = NalvaJson.Strings(companyAgent.ToolsJson);
        if (requested.Any(toolName => !granted.Contains(toolName, StringComparer.OrdinalIgnoreCase)))
            return Fail("The target agent is not allowed to use one of the requested tools.");

        return Ok("Delegation accepted by the orchestrator.", new JsonObject
        {
            ["delegation"] = true,
            ["mode"] = tool == "CreateAgentTask" ? "queue" : "inline",
            ["targetAgent"] = companyAgent.AgentKey,
            ["objective"] = NalvaArgs.Str(args, "objective") ?? "Delegated work",
            ["tools"] = new JsonArray(requested.Select(x => (JsonNode)x!).ToArray())
        });
    }

    private async Task<NalvaToolOutcome> CompanyProfile(CancellationToken ct)
    {
        if (_marketing is not null)
        {
            var brand = await _marketing.GetOrSeedBrandProfileAsync(ct);
            return Ok("Marketing brand profile seeded from company records.", new JsonObject
            {
                ["found"] = true,
                ["id"] = brand.Id.ToString(),
                ["name"] = brand.BusinessName,
                ["businessName"] = brand.BusinessName,
                ["industry"] = brand.Industry,
                ["website"] = brand.Website,
                ["brandColorsJson"] = brand.BrandColorsJson,
                ["productsServicesJson"] = brand.ProductsServicesJson,
                ["sourceNotes"] = brand.SourceNotes,
                ["logoUrl"] = brand.LogoUrl
            });
        }

        var company = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(ct);
        if (company is null)
            return Ok("No company profile row was found for this tenant.", new JsonObject { ["found"] = false });
        return Ok("Company profile.", new JsonObject
        {
            ["found"] = true,
            ["name"] = company.Name,
            ["businessType"] = company.BusinessType,
            ["country"] = company.Country,
            ["currency"] = company.Currency
        });
    }

    private async Task<NalvaToolOutcome> Customers(CancellationToken ct)
    {
        var rows = await _db.Customers.AsNoTracking().OrderBy(x => x.LastName).Take(20)
            .Select(x => new { x.Id, x.FirstName, x.LastName, x.Company, x.Status }).ToListAsync(ct);
        return List("customers", rows.Select(x => new JsonObject
        {
            ["id"] = x.Id.ToString(),
            ["name"] = $"{x.FirstName} {x.LastName}".Trim(),
            ["company"] = x.Company,
            ["status"] = x.Status
        }));
    }

    private async Task<NalvaToolOutcome> Segments(CancellationToken ct)
    {
        var rows = await _db.CustomerSegments.AsNoTracking().Select(x => new { x.Id, x.Name, x.Industry }).ToListAsync(ct);
        return List("segments", rows.Select(x => new JsonObject
        {
            ["id"] = x.Id.ToString(),
            ["name"] = x.Name,
            ["industry"] = x.Industry
        }));
    }

    private async Task<NalvaToolOutcome> ResearchMarket(string agentKey, CancellationToken ct)
    {
        if (_marketing is not null)
        {
            var report = await _marketing.CreateResearchReportAsync(new CreateResearchReportRequest
            {
                Kind = "Market",
                Title = "Market research",
                AgentKey = agentKey
            }, ct);
            return Ok(report.Body, new JsonObject
            {
                ["reportId"] = report.Id.ToString(),
                ["kind"] = report.Kind,
                ["fabricated"] = report.Fabricated,
                ["sourcesJson"] = report.SourcesJson,
                ["externalResearch"] = false
            });
        }

        var segments = await _db.CustomerSegments.CountAsync(ct);
        var customers = await _db.Customers.CountAsync(ct);
        return Ok("Market note from this company's records. No external market feed was used.", new JsonObject
        {
            ["segments"] = segments,
            ["customers"] = customers,
            ["externalResearch"] = false,
            ["estimate"] = false
        });
    }

    private async Task<NalvaToolOutcome> ResearchCompetitors(string agentKey, CancellationToken ct)
    {
        if (_marketing is not null)
        {
            var report = await _marketing.CreateResearchReportAsync(new CreateResearchReportRequest
            {
                Kind = "Competitor",
                Title = "Competitor research",
                AgentKey = agentKey
            }, ct);
            return Ok(report.Body, new JsonObject
            {
                ["reportId"] = report.Id.ToString(),
                ["kind"] = report.Kind,
                ["fabricated"] = report.Fabricated,
                ["competitorNamesJson"] = report.CompetitorNamesJson,
                ["sourcesJson"] = report.SourcesJson
            });
        }

        return await KnowledgeSearch(agentKey, "competitor", ct);
    }

    private async Task<NalvaToolOutcome> LeadSources(CancellationToken ct)
    {
        var leads = await _db.Leads.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(20)
            .Select(x => new { x.Id, x.Name, x.Email, x.Source, x.Status }).ToListAsync(ct);
        return List("leads", leads.Select(x => new JsonObject
        {
            ["id"] = x.Id.ToString(),
            ["name"] = x.Name,
            ["email"] = x.Email,
            ["source"] = x.Source,
            ["status"] = x.Status
        }));
    }

    private async Task<NalvaToolOutcome> LeadDetails(JsonElement args, CancellationToken ct)
    {
        var id = NalvaArgs.GuidOf(args, "leadId") ?? NalvaArgs.GuidOf(args, "id");
        if (id is null)
            return Ok("No lead id was supplied.", new JsonObject { ["found"] = false });
        var lead = await _db.Leads.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (lead is null || lead.TenantId != CompanyId)
            return Ok("That lead was not found in this company.", new JsonObject { ["found"] = false });
        return Ok("Stored lead.", new JsonObject
        {
            ["found"] = true,
            ["id"] = lead.Id.ToString(),
            ["name"] = lead.Name,
            ["email"] = lead.Email,
            ["company"] = lead.CompanyName,
            ["status"] = lead.Status,
            ["qualified"] = lead.IsQualified
        });
    }

    private async Task<NalvaToolOutcome> CreateLead(JsonElement args, CancellationToken ct)
    {
        var name = NalvaArgs.Str(args, "name");
        var email = NalvaArgs.Str(args, "email")?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name))
            return Ok("No name was supplied, so no lead was created.", new JsonObject { ["created"] = false });
        if (!string.IsNullOrWhiteSpace(email))
        {
            var existing = await _db.Leads.AsNoTracking().FirstOrDefaultAsync(x => x.Email != null && x.Email.ToLower() == email, ct);
            if (existing is not null)
                return Ok("A lead with that email already exists.", new JsonObject { ["created"] = false, ["leadId"] = existing.Id.ToString(), ["duplicate"] = true });
        }

        var lead = new Lead
        {
            Name = name.Trim(),
            Email = email,
            Phone = NalvaArgs.Str(args, "phone"),
            CompanyName = NalvaArgs.Str(args, "company"),
            Source = "Nalva",
            Status = LeadStatuses.New,
            Notes = NalvaGuard.Clip(NalvaArgs.Str(args, "notes"), 1000)
        };
        _db.Leads.Add(lead);
        await _db.SaveChangesAsync(ct);
        return Ok("Lead created in this company.", new JsonObject { ["created"] = true, ["leadId"] = lead.Id.ToString(), ["duplicate"] = false });
    }

    private async Task<NalvaToolOutcome> UpdateLead(JsonElement args, CancellationToken ct)
    {
        var id = NalvaArgs.GuidOf(args, "leadId") ?? NalvaArgs.GuidOf(args, "id");
        if (id is null)
            return Ok("No lead id was supplied, so no lead was updated.", new JsonObject { ["updated"] = false });
        var lead = await _db.Leads.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (lead is null || lead.TenantId != CompanyId)
            return Fail("That lead was not found in this company.");
        var status = NalvaArgs.Str(args, "status");
        if (!string.IsNullOrWhiteSpace(status))
            lead.Status = status.Trim();
        if (NalvaArgs.Bool(args, "qualified") == true)
        {
            if (string.IsNullOrWhiteSpace(lead.Email))
                return Ok("The lead has no email, so it was not marked qualified.", new JsonObject { ["updated"] = false, ["leadId"] = lead.Id.ToString() });
            lead.IsQualified = true;
            lead.Status = LeadStatuses.Qualified;
        }
        var notes = NalvaArgs.Str(args, "notes");
        if (!string.IsNullOrWhiteSpace(notes))
            lead.Notes = NalvaGuard.Clip(notes, 1000);
        await _db.SaveChangesAsync(ct);
        return Ok("Lead updated.", new JsonObject { ["updated"] = true, ["leadId"] = lead.Id.ToString(), ["qualified"] = lead.IsQualified });
    }

    private async Task<NalvaToolOutcome> CreateDeal(JsonElement args, CancellationToken ct)
    {
        var title = NalvaArgs.Str(args, "title");
        if (string.IsNullOrWhiteSpace(title))
            return Ok("No deal title was supplied, so no deal was created.", new JsonObject { ["created"] = false });
        var deal = new Deal
        {
            Title = title.Trim(),
            LeadId = NalvaArgs.GuidOf(args, "leadId"),
            CustomerId = NalvaArgs.GuidOf(args, "customerId"),
            Value = NalvaArgs.Dec(args, "value") ?? 0m,
            Status = DealStatuses.Open,
            Notes = "Created by Nalva from supplied fields."
        };
        _db.Deals.Add(deal);
        await _db.SaveChangesAsync(ct);
        return Ok("Open deal created.", new JsonObject { ["created"] = true, ["dealId"] = deal.Id.ToString() });
    }

    private async Task<NalvaToolOutcome> CreateFollowUp(string agentKey, JsonElement args, CancellationToken ct)
    {
        var follow = new FollowUp
        {
            Title = NalvaArgs.Str(args, "title") ?? "Follow up",
            DueAt = DateTime.UtcNow.AddDays(2),
            LeadId = NalvaArgs.GuidOf(args, "leadId"),
            CustomerId = NalvaArgs.GuidOf(args, "customerId"),
            DealId = NalvaArgs.GuidOf(args, "dealId"),
            Notes = $"Prepared by {agentKey}.",
            Status = FollowUpStatuses.Open
        };
        _db.FollowUps.Add(follow);
        await _db.SaveChangesAsync(ct);
        return Ok("Follow-up scheduled inside this company.", new JsonObject { ["created"] = true, ["followUpId"] = follow.Id.ToString() });
    }

    private async Task<NalvaToolOutcome> QuotationDraft(JsonElement args, CancellationToken ct)
    {
        var customerId = NalvaArgs.GuidOf(args, "customerId");
        if (customerId is null)
            return Ok("No customer id was supplied, so no quotation was created.", new JsonObject { ["created"] = false });
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customerId, ct);
        if (customer is null || customer.TenantId != CompanyId)
            return Ok("That customer was not found, so no quotation was created.", new JsonObject { ["created"] = false });
        var quote = new Quotation
        {
            CustomerId = customer.Id,
            QuotationNumber = $"QT-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            QuotationDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddDays(14),
            Status = "Draft",
            Notes = "Draft prepared by Nalva. Not sent."
        };
        _db.Quotations.Add(quote);
        await _db.SaveChangesAsync(ct);
        return Ok("Draft quotation created. It was not sent.", new JsonObject { ["created"] = true, ["quotationId"] = quote.Id.ToString(), ["sent"] = false });
    }

    private async Task<NalvaToolOutcome> Pipeline(CancellationToken ct)
    {
        var leads = await _db.Leads.AsNoTracking().GroupBy(x => x.Status).Select(x => new { x.Key, Count = x.Count() }).ToListAsync(ct);
        var deals = await _db.Deals.CountAsync(x => x.Status == DealStatuses.Open, ct);
        var list = new JsonArray();
        foreach (var row in leads)
            list.Add(new JsonObject { ["status"] = row.Key, ["count"] = row.Count });
        return Ok("Pipeline from stored leads.", new JsonObject { ["leads"] = list, ["openDeals"] = deals });
    }

    private async Task<NalvaToolOutcome> Conversion(CancellationToken ct)
    {
        if (_salesIntel is not null)
        {
            var data = await _salesIntel.GetConversionAsync(ct);
            return Ok("Conversion from stored leads and orders.", JsonSerializer.SerializeToNode(data)?.AsObject() ?? new JsonObject());
        }

        var total = await _db.Leads.CountAsync(ct);
        var converted = await _db.Leads.CountAsync(x => x.Status == LeadStatuses.Converted, ct);
        return Ok("Conversion from stored leads.", new JsonObject
        {
            ["leads"] = total,
            ["converted"] = converted,
            ["rate"] = total == 0 ? 0 : decimal.Round((decimal)converted / total, 4)
        });
    }

    private async Task<NalvaToolOutcome> ForecastSales(CancellationToken ct)
    {
        if (_salesIntel is null)
            return Fail("Sales intelligence is unavailable.");
        var data = await _salesIntel.ForecastSalesAsync(1, ct);
        return Ok(
            data.Available
                ? "Labeled sales revenue forecast from completed-order history. Prediction â€” not a confirmed fact."
                : "Sales forecast unavailable due to insufficient history. No figure was invented.",
            JsonSerializer.SerializeToNode(data)?.AsObject() ?? new JsonObject());
    }

    private async Task<NalvaToolOutcome> SalesIntel(CancellationToken ct)
    {
        if (_salesIntel is null)
            return Fail("Sales intelligence is unavailable.");
        var data = await _salesIntel.GetIntelligenceAsync(period: "month", cancellationToken: ct);
        return Ok("Sales intelligence from recorded orders and leads.", JsonSerializer.SerializeToNode(data)?.AsObject() ?? new JsonObject());
    }

    private async Task<NalvaToolOutcome> CustomerIntel(CancellationToken ct)
    {
        if (_customerIntel is null)
            return Fail("Customer intelligence is unavailable.");
        var data = await _customerIntel.GetIntelligenceAsync(ct);
        return Ok("Customer intelligence from completed-order history. Churn risks are estimates.", JsonSerializer.SerializeToNode(data)?.AsObject() ?? new JsonObject());
    }

    private async Task<NalvaToolOutcome> ExecutiveBrief(JsonElement args, CancellationToken ct)
    {
        if (_executive is null)
            return Fail("Executive intelligence is unavailable.");
        var question = NalvaArgs.Str(args, "question") ?? NalvaArgs.Str(args, "q") ?? "How is my business performing?";
        var data = await _executive.AnswerAsync(question, ct);
        return Ok(
            data.ContainsPredictions
                ? "Decision brief includes labeled predictions. Not confirmed financial facts."
                : "Decision brief from recorded tenant data.",
            JsonSerializer.SerializeToNode(data)?.AsObject() ?? new JsonObject());
    }

    private async Task<NalvaToolOutcome> BusinessRisks(CancellationToken ct)
    {
        if (_bi is null)
            return Fail("Business intelligence is unavailable.");
        var data = await _bi.GetRisksAsync(ct);
        return Ok("Business risks from recorded inventory, finance, sales, and customer signals.", JsonSerializer.SerializeToNode(data)?.AsObject() ?? new JsonObject());
    }

    private async Task<NalvaToolOutcome> KnowledgeSearch(string agentKey, string? query, CancellationToken ct)
    {
        if (_support is not null && string.Equals(agentKey, SupportAgentKeys.Support, StringComparison.OrdinalIgnoreCase))
        {
            var supportKnowledge = await _support.SearchKnowledgeAsync(query, ct);
            var supportSources = supportKnowledge["sources"] as JsonArray ?? [];
            return Ok(supportSources.Count == 0 ? "No authorized knowledge matched." : "Authorized knowledge.", supportKnowledge);
        }

        var rows = await _db.NalvaKnowledgeSources.AsNoTracking()
            .Where(x => x.AgentKey == null || x.AgentKey == agentKey)
            .ToListAsync(ct);
        var tokens = string.IsNullOrWhiteSpace(query)
            ? Array.Empty<string>()
            : query.Split([' ', '\t', '\r', '\n', ',', '.', '?', '!'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => x.Length > 2)
                .Select(x => x.ToLowerInvariant())
                .Distinct()
                .ToArray();
        if (tokens.Length > 0)
        {
            rows = rows
                .Select(row =>
                {
                    var haystack = $"{row.Title} {row.Content}".ToLowerInvariant();
                    var score = tokens.Count(token => haystack.Contains(token, StringComparison.Ordinal));
                    return (row, score);
                })
                .Where(x => x.score > 0)
                .OrderByDescending(x => x.score)
                .Select(x => x.row)
                .ToList();
        }
        var list = new JsonArray();
        foreach (var row in rows.Take(5))
        {
            if (row.TenantId != CompanyId)
                continue;
            list.Add(new JsonObject
            {
                ["id"] = row.Id.ToString(),
                ["title"] = row.Title,
                ["agentKey"] = row.AgentKey,
                ["excerpt"] = NalvaGuard.Clip(row.Content, 240),
                ["source"] = "authorized-knowledge"
            });
        }
        var data = new JsonObject { ["sources"] = list, ["authorizedOnly"] = true, ["invented"] = false };
        if (string.Equals(agentKey, "legal", StringComparison.OrdinalIgnoreCase))
            data["disclaimer"] = "This organizes stored documents. It is not legal advice.";
        return Ok(list.Count == 0 ? "No authorized knowledge matched." : "Authorized knowledge.", data);
    }

    private async Task<NalvaToolOutcome> CustomerDetails(JsonElement args, CancellationToken ct)
    {
        var id = NalvaArgs.GuidOf(args, "customerId") ?? NalvaArgs.GuidOf(args, "id");
        if (id is null)
            return Ok("No customer id was supplied.", new JsonObject { ["found"] = false });
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (customer is null || customer.TenantId != CompanyId)
            return Ok("That customer was not found in this company.", new JsonObject { ["found"] = false });
        return Ok("Stored customer.", new JsonObject
        {
            ["found"] = true,
            ["id"] = customer.Id.ToString(),
            ["name"] = $"{customer.FirstName} {customer.LastName}".Trim(),
            ["email"] = customer.Email,
            ["company"] = customer.Company,
            ["status"] = customer.Status
        });
    }

    private async Task<NalvaToolOutcome> OrderDetails(JsonElement args, CancellationToken ct)
    {
        var id = NalvaArgs.GuidOf(args, "orderId") ?? NalvaArgs.GuidOf(args, "id");
        var number = NalvaArgs.Str(args, "orderNumber");
        var customerId = NalvaArgs.GuidOf(args, "customerId");
        Order? order = null;
        if (id is Guid orderId)
            order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (order is null && !string.IsNullOrWhiteSpace(number))
            order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.OrderNumber == number, ct);
        if (order is null || order.TenantId != CompanyId)
            return Ok("That order was not found in this company.", new JsonObject { ["found"] = false });
        if (customerId is Guid requiredCustomer && order.CustomerId != requiredCustomer)
            return Ok("That order does not belong to the authorized customer.", new JsonObject { ["found"] = false, ["authorized"] = false });
        return Ok("Stored order.", new JsonObject
        {
            ["found"] = true,
            ["authorized"] = true,
            ["id"] = order.Id.ToString(),
            ["customerId"] = order.CustomerId.ToString(),
            ["number"] = order.OrderNumber,
            ["status"] = order.Status,
            ["total"] = order.GrandTotal,
            ["paymentStatus"] = order.PaymentStatus,
            ["shippingAddress"] = order.ShippingAddress
        });
    }

    private async Task<NalvaToolOutcome> CustomerHistory(JsonElement args, CancellationToken ct)
    {
        var id = NalvaArgs.GuidOf(args, "customerId");
        if (id is null)
            return Ok("No customer id was supplied.", new JsonObject { ["found"] = false });
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (customer is null || customer.TenantId != CompanyId)
            return Ok("That customer was not found in this company.", new JsonObject { ["found"] = false });
        var orders = await _db.Orders.AsNoTracking().Where(x => x.CustomerId == id).CountAsync(ct);
        var followUps = await _db.FollowUps.AsNoTracking().Where(x => x.CustomerId == id).CountAsync(ct);
        var openTickets = await _db.SupportTickets.AsNoTracking().CountAsync(x =>
            x.CustomerId == id &&
            x.Status != SupportTicketStatuses.Resolved &&
            x.Status != SupportTicketStatuses.Closed, ct);
        var complaints = await _db.SupportTickets.AsNoTracking().CountAsync(x =>
            x.CustomerId == id && x.Category == "Complaint", ct);
        return Ok("Stored customer history.", new JsonObject
        {
            ["found"] = true,
            ["customerId"] = id.ToString(),
            ["orders"] = orders,
            ["followUps"] = followUps,
            ["openSupportTickets"] = openTickets,
            ["complaintTickets"] = complaints
        });
    }

    private async Task<NalvaToolOutcome> ProjectDetails(JsonElement args, bool requirementsOnly, CancellationToken ct)
    {
        var id = NalvaArgs.GuidOf(args, "projectId") ?? NalvaArgs.GuidOf(args, "id");
        if (id is null)
            return Ok("No project id was supplied.", new JsonObject { ["found"] = false });
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (project is null || project.TenantId != CompanyId)
            return Ok("That project was not found in this company.", new JsonObject { ["found"] = false });
        var open = await _db.WorkTasks.CountAsync(x => x.ProjectId == project.Id && x.Status != WorkTaskStatus.Done, ct);
        return Ok("Stored project.", new JsonObject
        {
            ["found"] = true,
            ["id"] = project.Id.ToString(),
            ["name"] = project.Name,
            ["description"] = requirementsOnly ? project.Description : project.Description,
            ["status"] = project.Status.ToString(),
            ["openTasks"] = open
        });
    }

    private async Task<NalvaToolOutcome> CreateTask(JsonElement args, CancellationToken ct)
    {
        var projectId = NalvaArgs.GuidOf(args, "projectId");
        var title = NalvaArgs.Str(args, "title");
        if (projectId is null || string.IsNullOrWhiteSpace(title))
            return Ok("A project id and title are required, so no task was created.", new JsonObject { ["created"] = false });
        var project = await _db.Projects.FirstOrDefaultAsync(x => x.Id == projectId, ct);
        if (project is null || project.TenantId != CompanyId)
            return Fail("That project was not found in this company.");
        var task = new WorkTask
        {
            ProjectId = project.Id,
            Title = title.Trim(),
            Description = NalvaGuard.Clip(NalvaArgs.Str(args, "description"), 2000),
            Status = WorkTaskStatus.Todo,
            Priority = 2
        };
        _db.WorkTasks.Add(task);
        await _db.SaveChangesAsync(ct);
        return Ok("Task created on the stored project.", new JsonObject { ["created"] = true, ["taskId"] = task.Id.ToString() });
    }

    private async Task<NalvaToolOutcome> Expenses(CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-30);
        var total = await _db.Expenses.Where(x => x.ExpenseDate >= since).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        var count = await _db.Expenses.CountAsync(x => x.ExpenseDate >= since, ct);
        return Ok("Recorded expenses for 30 days.", new JsonObject { ["count"] = count, ["total"] = total, ["isEstimate"] = false });
    }

    private async Task<NalvaToolOutcome> Invoices(CancellationToken ct)
    {
        var count = await _db.Invoices.CountAsync(ct);
        var outstanding = await _db.Invoices.SumAsync(x => (decimal?)x.OutstandingAmount, ct) ?? 0m;
        return Ok("Recorded invoices.", new JsonObject { ["count"] = count, ["outstanding"] = outstanding });
    }

    private async Task<NalvaToolOutcome> Payments(CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-30);
        var total = await _db.Payments.Where(x => x.PaymentDate >= since).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        return Ok("Recorded payments for 30 days.", new JsonObject { ["total"] = total });
    }

    private async Task<NalvaToolOutcome> Receivables(CancellationToken ct)
    {
        var rows = await _db.Invoices.AsNoTracking().Where(x => x.OutstandingAmount > 0).Select(x => new { x.InvoiceNumber, x.OutstandingAmount, x.Status }).Take(20).ToListAsync(ct);
        return List("invoices", rows.Select(x => new JsonObject
        {
            ["number"] = x.InvoiceNumber,
            ["outstanding"] = x.OutstandingAmount,
            ["status"] = x.Status
        }));
    }

    private async Task<NalvaToolOutcome> Payables(CancellationToken ct)
    {
        var purchases = await _db.Purchases.CountAsync(x => x.Status == "Draft" || x.Status == "Pending", ct);
        var invoices = await _db.PurchaseInvoices.Where(x => x.Total > x.AmountPaid).SumAsync(x => (decimal?)(x.Total - x.AmountPaid), ct) ?? 0m;
        return Ok("Open payables from stored purchases.", new JsonObject { ["openPurchases"] = purchases, ["purchaseInvoiceBalance"] = invoices });
    }

    private async Task<NalvaToolOutcome> Profit(CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-30);
        var payments = await _db.Payments.Where(x => x.PaymentDate >= since).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        var expenses = await _db.Expenses.Where(x => x.ExpenseDate >= since).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        return Ok("Recorded payments minus recorded expenses for 30 days. Not a closed accounting period.", new JsonObject
        {
            ["payments"] = payments,
            ["expenses"] = expenses,
            ["difference"] = payments - expenses,
            ["isAccountingRecord"] = false
        });
    }

    private async Task<NalvaToolOutcome> CashFlow(CancellationToken ct)
    {
        var payments = await _db.Payments.AsNoTracking().Select(x => new { x.PaymentDate, x.Amount }).ToListAsync(ct);
        var expenses = await _db.Expenses.AsNoTracking().Select(x => new { x.ExpenseDate, x.Amount }).ToListAsync(ct);
        var months = payments.Select(x => new DateTime(x.PaymentDate.Year, x.PaymentDate.Month, 1)).Concat(expenses.Select(x => new DateTime(x.ExpenseDate.Year, x.ExpenseDate.Month, 1))).Distinct().Count();
        if (months < 2)
            return Ok("Not enough monthly history to estimate cash flow.", new JsonObject { ["estimate"] = false, ["months"] = months });
        var net = payments.Sum(x => x.Amount) - expenses.Sum(x => x.Amount);
        var average = decimal.Round(net / months, 2);
        return Ok("Labeled cash-flow estimate from recorded history. Not a forecast commitment.", new JsonObject { ["estimate"] = true, ["months"] = months, ["averageMonthlyNet"] = average });
    }

    private async Task<NalvaToolOutcome> Revenue(CancellationToken ct)
    {
        var rows = await _db.Payments.AsNoTracking().Select(x => new { x.PaymentDate, x.Amount }).ToListAsync(ct);
        var list = new JsonArray();
        foreach (var group in rows.GroupBy(x => new { x.PaymentDate.Year, x.PaymentDate.Month }).OrderBy(x => x.Key.Year).ThenBy(x => x.Key.Month).Take(6))
            list.Add(new JsonObject { ["month"] = $"{group.Key.Year}-{group.Key.Month:00}", ["amount"] = group.Sum(x => x.Amount) });
        return Ok("Recorded payments by month.", new JsonObject { ["months"] = list, ["isEstimate"] = false });
    }

    private async Task<NalvaToolOutcome> InventoryDetails(string tool, CancellationToken ct)
    {
        var rows = await _db.Products.AsNoTracking().Where(x => x.IsActive).Select(x => new { x.Name, x.SKU, x.CurrentStock, x.ReorderLevel, x.CostPrice }).ToListAsync(ct);
        var selected = tool == "GetLowStockProducts" ? rows.Where(x => x.CurrentStock <= x.ReorderLevel).ToList() : rows;
        return Ok(tool == "GetInventoryAnalytics" ? "Stock value from cost price times quantity." : "Stored stock.", new JsonObject
        {
            ["count"] = selected.Count,
            ["units"] = selected.Sum(x => x.CurrentStock),
            ["costValue"] = selected.Sum(x => x.CurrentStock * x.CostPrice),
            ["names"] = new JsonArray(selected.Take(15).Select(x => (JsonNode)$"{x.Name} ({x.SKU}): {x.CurrentStock}").ToArray())
        });
    }

    private async Task<NalvaToolOutcome> CustomerAnalytics(CancellationToken ct)
    {
        var customers = await _db.Customers.CountAsync(ct);
        var buyers = await _db.Orders.Select(x => x.CustomerId).Distinct().CountAsync(ct);
        return Ok("Customer counts from stored records.", new JsonObject { ["customers"] = customers, ["withOrders"] = buyers });
    }

    private async Task<NalvaToolOutcome> ExpenseAnalytics(CancellationToken ct)
    {
        var rows = await _db.Expenses.AsNoTracking().Select(x => new { x.ExpenseCategoryId, x.Amount }).ToListAsync(ct);
        var categories = await _db.ExpenseCategories.AsNoTracking().Select(x => new { x.Id, x.Name }).ToListAsync(ct);
        var list = new JsonArray();
        foreach (var group in rows.GroupBy(x => x.ExpenseCategoryId))
        {
            var name = categories.FirstOrDefault(x => x.Id == group.Key)?.Name ?? "Uncategorized";
            list.Add(new JsonObject { ["category"] = name, ["total"] = group.Sum(x => x.Amount) });
        }
        return Ok("Recorded expenses by category.", new JsonObject { ["categories"] = list });
    }

    private async Task<NalvaToolOutcome> Trends(CancellationToken ct)
    {
        var start = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var previous = start.AddMonths(-1);
        var current = await _db.Orders.Where(x => x.OrderDate >= start).SumAsync(x => (decimal?)x.GrandTotal, ct) ?? 0m;
        var prior = await _db.Orders.Where(x => x.OrderDate >= previous && x.OrderDate < start).SumAsync(x => (decimal?)x.GrandTotal, ct) ?? 0m;
        return Ok("Order totals for this month and the previous month.", new JsonObject
        {
            ["thisMonth"] = current,
            ["previousMonth"] = prior,
            ["historical"] = true,
            ["prediction"] = false
        });
    }

    private async Task<NalvaToolOutcome> SupplierDetails(JsonElement args, CancellationToken ct)
    {
        var id = NalvaArgs.GuidOf(args, "supplierId") ?? NalvaArgs.GuidOf(args, "id");
        if (id is null)
            return Ok("No supplier id was supplied.", new JsonObject { ["found"] = false });
        var supplier = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (supplier is null || supplier.TenantId != CompanyId)
            return Ok("That supplier was not found.", new JsonObject { ["found"] = false });
        return Ok("Stored supplier.", new JsonObject { ["found"] = true, ["id"] = supplier.Id.ToString(), ["name"] = supplier.Name, ["email"] = supplier.Email });
    }

    private async Task<NalvaToolOutcome> ResearchSuppliers(JsonElement args, CancellationToken ct)
    {
        if (_procurement is null)
            return Fail("Procurement agent service is not available.");
        var productId = NalvaArgs.GuidOf(args, "productId");
        var quantity = NalvaArgs.Dec(args, "quantity") ?? 1m;
        if (productId is null)
            return Ok("A productId is required to compare suppliers.", new JsonObject { ["found"] = false });
        var options = await _procurement.CompareSuppliersAsync(productId.Value, quantity, ct);
        return List("suppliers", options.Select(x => new JsonObject
        {
            ["supplierId"] = x.SupplierId?.ToString(),
            ["name"] = x.SupplierName,
            ["source"] = x.Source,
            ["classification"] = x.Classification,
            ["unitPrice"] = x.UnitPrice,
            ["quantity"] = x.Quantity,
            ["moq"] = x.MinimumOrderQuantity,
            ["totalCost"] = x.TotalCost,
            ["deliveryDays"] = x.EstimatedDeliveryDays,
            ["paymentTerms"] = x.PaymentTerms,
            ["notes"] = x.Notes
        }));
    }

    private async Task<NalvaToolOutcome> Purchases(bool pendingOnly, CancellationToken ct)
    {
        var query = _db.Purchases.AsNoTracking();
        if (pendingOnly)
            query = query.Where(x => x.Status == "Draft" || x.Status == "Pending");
        var rows = await query.OrderByDescending(x => x.PurchaseDate).Take(20).Select(x => new { x.Id, x.Status, x.TotalAmount, x.ExpectedDeliveryDate }).ToListAsync(ct);
        return List("purchases", rows.Select(x => new JsonObject
        {
            ["id"] = x.Id.ToString(),
            ["status"] = x.Status,
            ["total"] = x.TotalAmount,
            ["expectedDeliveryDate"] = x.ExpectedDeliveryDate?.ToString("O")
        }));
    }

    private async Task<NalvaToolOutcome> ForecastDemand(JsonElement args, CancellationToken ct)
    {
        if (_procurement is null)
            return Fail("Procurement agent service is not available.");
        var productId = NalvaArgs.GuidOf(args, "productId");
        var rows = await _procurement.AnalyzeDemandAsync(productId, ct);
        return List("demand", rows.Take(25).Select(x => new JsonObject
        {
            ["productId"] = x.ProductId.ToString(),
            ["name"] = x.ProductName,
            ["sku"] = x.Sku,
            ["averageDailyDemand"] = x.AverageDailyDemand,
            ["averageMonthlyDemand"] = x.AverageMonthlyDemand,
            ["daysOfCover"] = x.DaysOfCover,
            ["estimatedStockoutDate"] = x.EstimatedStockoutDate?.ToString("O"),
            ["suggestedReorderQuantity"] = x.SuggestedReorderQuantity,
            ["confidence"] = x.Confidence,
            ["forecastAvailable"] = x.ForecastAvailable,
            ["assumptions"] = x.Assumptions
        }));
    }

    private async Task<NalvaToolOutcome> DelayedDeliveries(CancellationToken ct)
    {
        if (_procurement is null)
            return Fail("Procurement agent service is not available.");
        var rows = (await _procurement.GetDeliveriesAsync(ct)).Where(x => x.IsDelayed).ToList();
        return List("deliveries", rows.Select(x => new JsonObject
        {
            ["purchaseOrderId"] = x.PurchaseOrderId.ToString(),
            ["reference"] = x.ReferenceNumber,
            ["supplier"] = x.SupplierName,
            ["daysLate"] = x.DaysLate,
            ["remaining"] = x.RemainingQuantity,
            ["expectedDeliveryDate"] = x.ExpectedDeliveryDate?.ToString("O")
        }));
    }

    private async Task<NalvaToolOutcome> SlowMoving(CancellationToken ct)
    {
        if (_procurement is null)
            return Fail("Procurement agent service is not available.");
        var rows = (await _procurement.GetInventoryRisksAsync(ct)).Where(x => x.RiskKind == "slow-moving").ToList();
        return List("slowMoving", rows.Select(x => new JsonObject
        {
            ["productId"] = x.ProductId.ToString(),
            ["name"] = x.ProductName,
            ["sku"] = x.Sku,
            ["currentStock"] = x.CurrentStock,
            ["explanation"] = x.Explanation
        }));
    }

    private async Task<NalvaToolOutcome> ProcurementBudget(CancellationToken ct)
    {
        if (_procurement is null)
            return Fail("Procurement agent service is not available.");
        var overview = await _procurement.GetOverviewAsync(ct);
        return Ok("Estimated procurement need from recorded low/out-of-stock recommendations using product cost prices.", new JsonObject
        {
            ["estimatedProcurementNeed"] = overview.EstimatedProcurementNeed,
            ["lowStockCount"] = overview.LowStockCount,
            ["outOfStockCount"] = overview.OutOfStockCount,
            ["classification"] = overview.DataClassification
        });
    }

    private async Task<NalvaToolOutcome> PreparePurchase(JsonElement args, CancellationToken ct)
    {
        if (_procurement is null)
            return Fail("Procurement agent service is not available.");
        var supplierId = NalvaArgs.GuidOf(args, "supplierId");
        var productId = NalvaArgs.GuidOf(args, "productId");
        var quantity = NalvaArgs.Dec(args, "quantity") ?? 0m;
        var unitPrice = NalvaArgs.Dec(args, "unitPrice");
        if (supplierId is null || productId is null || quantity <= 0)
            return Ok("Supplier, product, and quantity are required, so no purchase was prepared.", new JsonObject { ["created"] = false });

        if (unitPrice is null)
        {
            var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == productId, ct);
            unitPrice = product?.CostPrice ?? 0m;
        }

        var prepared = await _procurement.PreparePurchaseOrderAsync(new PreparePurchaseOrderRequest
        {
            SupplierId = supplierId.Value,
            ProductId = productId.Value,
            Quantity = quantity,
            UnitPrice = unitPrice.Value,
            ExpectedDeliveryDate = NalvaArgs.Date(args, "expectedDeliveryDate"),
            PaymentTerms = NalvaArgs.Str(args, "paymentTerms"),
            Reason = NalvaArgs.Str(args, "reason"),
            IdempotencyKey = NalvaArgs.Str(args, "idempotencyKey") ?? $"nalva:{productId:N}:{supplierId:N}:{quantity}",
            Source = "nalva"
        }, ct);

        return Ok(prepared.Summary, new JsonObject
        {
            ["created"] = prepared.Created,
            ["duplicatePrevented"] = prepared.DuplicatePrevented,
            ["recommendationId"] = prepared.RecommendationId.ToString(),
            ["purchaseOrderId"] = prepared.PurchaseOrderId?.ToString(),
            ["status"] = prepared.Status,
            ["approvalFingerprint"] = prepared.ApprovalFingerprint,
            ["requiresApproval"] = prepared.RequiresApproval,
            ["sentToSupplier"] = false
        });
    }

    private async Task<NalvaToolOutcome> SubmitPurchase(JsonElement args, bool approvalGranted, CancellationToken ct)
    {
        if (_procurement is null)
            return Fail("Procurement agent service is not available.");
        if (!approvalGranted)
            return Fail("Approval is required before submitting a purchase.");

        var recommendationId = NalvaArgs.GuidOf(args, "recommendationId");
        if (recommendationId is null)
        {
            // Backward-compatible path: create pending PO from supplier/product/qty after approval.
            var prepared = await PreparePurchase(args, ct);
            if (prepared.Data["recommendationId"]?.GetValue<string>() is not string rec || !Guid.TryParse(rec, out var id))
                return prepared;
            recommendationId = id;
        }

        var result = await _procurement.SubmitPreparedPurchaseAsync(
            recommendationId.Value,
            NalvaArgs.Str(args, "idempotencyKey"),
            approved: true,
            approvalFingerprint: NalvaArgs.Str(args, "approvalFingerprint"),
            ct);

        return Ok(result.Summary, new JsonObject
        {
            ["created"] = result.Created,
            ["duplicatePrevented"] = result.DuplicatePrevented,
            ["purchaseOrderId"] = result.PurchaseOrderId?.ToString(),
            ["status"] = result.Status,
            ["sentToSupplier"] = false,
            ["approvalFingerprint"] = result.ApprovalFingerprint
        });
    }

    private async Task<NalvaToolOutcome> ReceiveVerified(JsonElement args, CancellationToken ct)
    {
        if (_procurement is null)
            return Fail("Procurement agent service is not available.");
        var purchaseId = NalvaArgs.GuidOf(args, "purchaseOrderId") ?? NalvaArgs.GuidOf(args, "id");
        if (purchaseId is null)
            return Fail("purchaseOrderId is required for verified receiving.");

        var lines = new List<ReceiveVerificationLineRequest>();
        if (args.TryGetProperty("lines", out var linesEl) && linesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var line in linesEl.EnumerateArray())
            {
                var itemId = NalvaArgs.GuidOf(line, "purchaseItemId");
                var qty = NalvaArgs.Dec(line, "receivedQuantity") ?? 0m;
                if (itemId is null || qty <= 0)
                    continue;
                lines.Add(new ReceiveVerificationLineRequest
                {
                    PurchaseItemId = itemId.Value,
                    ReceivedQuantity = qty,
                    DamagedQuantity = NalvaArgs.Dec(line, "damagedQuantity") ?? 0m,
                    DiscrepancyNotes = NalvaArgs.Str(line, "discrepancyNotes")
                });
            }
        }

        if (lines.Count == 0)
            return Fail("Verified receiving requires explicit line quantities. Supplier delivery claims alone cannot increase stock.");

        var result = await _procurement.ReceiveVerifiedAsync(new ReceiveVerificationRequest
        {
            PurchaseOrderId = purchaseId.Value,
            Lines = lines,
            Notes = NalvaArgs.Str(args, "notes")
        }, ct);

        return Ok(result.Summary, new JsonObject
        {
            ["purchaseOrderId"] = result.PurchaseOrderId.ToString(),
            ["status"] = result.Status,
            ["stockIncreasedBy"] = result.StockIncreasedBy,
            ["discrepancies"] = new JsonArray(result.Discrepancies.Select(x => (JsonNode)x).ToArray())
        });
    }

    private async Task<NalvaToolOutcome> ProcurementJson<T>(Func<Task<T>> load, string key)
    {
        if (_procurement is null)
            return Fail("Procurement agent service is not available.");
        var value = await load();
        var node = JsonSerializer.SerializeToNode(value) ?? new JsonObject();
        return Ok($"Recorded {key} from the procurement agent.", new JsonObject { [key] = node });
    }

    private async Task<NalvaToolOutcome> Tasks(CancellationToken ct)
    {
        var soon = DateTime.UtcNow.AddDays(14);
        var rows = await _db.WorkTasks.AsNoTracking()
            .Where(x => x.DueDate != null && x.DueDate <= soon && x.Status != WorkTaskStatus.Done && x.Status != WorkTaskStatus.Cancelled)
            .OrderBy(x => x.DueDate).Take(20).Select(x => new { x.Id, x.Title, x.DueDate, x.Status }).ToListAsync(ct);
        return List("tasks", rows.Select(x => new JsonObject
        {
            ["id"] = x.Id.ToString(),
            ["title"] = x.Title,
            ["due"] = x.DueDate?.ToString("O"),
            ["status"] = x.Status.ToString()
        }));
    }

    private async Task<NalvaToolOutcome> Convert(JsonElement args, CancellationToken ct)
    {
        var id = NalvaArgs.GuidOf(args, "leadId") ?? NalvaArgs.GuidOf(args, "id");
        if (id is null)
            return Fail("A lead id is required.");
        var result = await _crm.ConvertLeadAsync(id.Value, true, ct);
        return Ok("Lead converted through the CRM service.", new JsonObject
        {
            ["leadId"] = result.LeadId.ToString(),
            ["customerId"] = result.CustomerId.ToString(),
            ["created"] = result.CustomerCreated
        });
    }

    private async Task<NalvaToolOutcome> Ingest(string agentKey, JsonElement args, CancellationToken ct)
    {
        var title = NalvaArgs.Str(args, "title");
        var content = NalvaArgs.Str(args, "content");
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
            return Fail("Title and content are required.");
        var shared = NalvaArgs.Bool(args, "shared") == true;
        var source = new NalvaKnowledgeSource
        {
            AgentKey = shared ? null : agentKey,
            Title = title.Trim(),
            Content = NalvaGuard.Redact(content),
            CreatedByUserId = "nalva"
        };
        _db.NalvaKnowledgeSources.Add(source);
        await _db.SaveChangesAsync(ct);
        return Ok("Knowledge stored for this company.", new JsonObject { ["id"] = source.Id.ToString(), ["shared"] = shared });
    }

    private async Task<NalvaToolOutcome> DeleteKnowledge(string agentKey, JsonElement args, CancellationToken ct)
    {
        var id = NalvaArgs.GuidOf(args, "id");
        if (id is null)
            return Fail("A knowledge id is required.");
        var source = await _db.NalvaKnowledgeSources.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (source is null || source.TenantId != CompanyId)
            return Fail("That knowledge source was not found in this company.");
        if (source.AgentKey is not null && !string.Equals(source.AgentKey, agentKey, StringComparison.OrdinalIgnoreCase))
            return Fail("This agent cannot delete another agent's knowledge.");
        _db.NalvaKnowledgeSources.Remove(source);
        await _db.SaveChangesAsync(ct);
        return Ok("Knowledge source deleted.", new JsonObject { ["deleted"] = true });
    }

    private async Task<NalvaToolOutcome> Churn(CancellationToken ct)
    {
        if (_customerIntel is not null)
        {
            var data = await _customerIntel.GetChurnRisksAsync(ct);
            return Ok(
                "Potential churn risks from completed-order silence. Heuristic estimates â€” not confirmed churn.",
                new JsonObject
                {
                    ["count"] = data.Count,
                    ["prediction"] = true,
                    ["customers"] = JsonSerializer.SerializeToNode(data.Take(20)) ?? new JsonArray()
                });
        }

        var cutoff = DateTime.UtcNow.AddDays(-90);
        var recent = await _db.Orders.AsNoTracking().Where(x => x.OrderDate >= cutoff).Select(x => x.CustomerId).Distinct().ToListAsync(ct);
        var older = await _db.Orders.AsNoTracking().Where(x => x.OrderDate < cutoff).Select(x => x.CustomerId).Distinct().ToListAsync(ct);
        var ids = older.Except(recent).Take(20).ToList();
        return Ok("Customers with an older order and none in the last 90 days.", new JsonObject
        {
            ["count"] = older.Except(recent).Count(),
            ["customerIds"] = new JsonArray(ids.Select(x => (JsonNode)x.ToString()).ToArray()),
            ["prediction"] = true
        });
    }

    private async Task<NalvaToolOutcome> Duplicates(CancellationToken ct)
    {
        var leads = await _db.Leads.AsNoTracking().Where(x => x.Email != null && x.Email != "").Select(x => new { x.Id, x.Email }).ToListAsync(ct);
        var dupes = leads.GroupBy(x => x.Email!.Trim().ToLowerInvariant()).Where(x => x.Count() > 1).Take(10).ToList();
        return Ok("Duplicate emails among stored leads.", new JsonObject { ["groups"] = dupes.Count });
    }

    private async Task<NalvaToolOutcome> Alerts(CancellationToken ct)
    {
        var failed = await _db.NalvaExecutions.CountAsync(x => x.Status == NalvaStatuses.Failed, ct);
        var pending = await _db.NalvaApprovals.CountAsync(x => x.Status == NalvaStatuses.Pending, ct);
        return Ok("Workforce alerts. No server was accessed.", new JsonObject { ["failedExecutions"] = failed, ["pendingApprovals"] = pending, ["serverAccess"] = false });
    }

    private async Task<NalvaToolOutcome> Attendance(CancellationToken ct)
    {
        var since = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var rows = await _db.AttendanceRecords.AsNoTracking().Where(x => x.WorkDate >= since).Select(x => x.Status).ToListAsync(ct);
        return Ok("Attendance counts for 30 days.", new JsonObject { ["records"] = rows.Count, ["present"] = rows.Count(x => x == "Present") });
    }

    private async Task<NalvaToolOutcome> EmployeeDetails(JsonElement args, CancellationToken ct)
    {
        var id = NalvaArgs.GuidOf(args, "employeeId") ?? NalvaArgs.GuidOf(args, "id");
        if (id is null)
            return Ok("No employee id was supplied.", new JsonObject { ["found"] = false });
        var employee = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (employee is null || employee.TenantId != CompanyId)
            return Ok("That employee was not found.", new JsonObject { ["found"] = false });
        return Ok("Directory fields only.", new JsonObject
        {
            ["found"] = true,
            ["id"] = employee.Id.ToString(),
            ["name"] = $"{employee.FirstName} {employee.LastName}".Trim(),
            ["designation"] = employee.Designation,
            ["email"] = employee.Email,
            ["status"] = employee.EmploymentStatus,
            ["salaryIncluded"] = false
        });
    }

    private async Task<NalvaToolOutcome> CampaignAnalytics(CancellationToken ct)
    {
        if (_marketing is not null)
        {
            var overview = await _marketing.GetOverviewAsync(ct);
            var snaps = await _db.MarketingAnalyticsSnapshots.AsNoTracking().CountAsync(ct);
            var withMetrics = await _db.MarketingAnalyticsSnapshots.AsNoTracking().CountAsync(x => x.MetricsJson != null, ct);
            return Ok("Marketing campaign counts and verified analytics snapshots only.", new JsonObject
            {
                ["campaigns"] = overview.Campaigns,
                ["activeCampaigns"] = overview.ActiveCampaigns,
                ["contentPublished"] = overview.ContentPublished,
                ["analyticsSnapshots"] = snaps,
                ["snapshotsWithMetrics"] = withMetrics,
                ["externalAnalytics"] = withMetrics > 0
            });
        }

        var campaigns = await _db.NalvaWorkItems.CountAsync(x => x.Kind == "Campaign", ct);
        var leads = await _db.Leads.CountAsync(x => x.Source == "Nalva", ct);
        return Ok("Draft campaigns and Nalva-sourced leads. Not an advertising-platform report.", new JsonObject { ["campaignDrafts"] = campaigns, ["nalvaLeads"] = leads, ["externalAnalytics"] = false });
    }

    private async Task<NalvaToolOutcome> Report(string agentKey, Guid executionId, string tool, CancellationToken ct)
    {
        var summary = tool switch
        {
            "GenerateHRReport" => (await EmployeeSummary(ct)).Summary,
            "GenerateDailySummary" => "Daily summary of tasks due and recent workforce results.",
            _ => (await Kpis(ct)).Summary
        };
        return await Draft(agentKey, executionId, "Report", tool, summary + " Figures come from stored records.", ct);
    }

    private async Task<NalvaToolOutcome> UpdateWorkItem(JsonElement args, string status, CancellationToken ct)
    {
        var id = NalvaArgs.GuidOf(args, "id");
        if (id is null)
            return Ok("No work item id was supplied.", new JsonObject { ["updated"] = false });
        var item = await _db.NalvaWorkItems.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null || item.TenantId != CompanyId)
            return Fail("That work item was not found in this company.");
        item.Status = status;
        await _db.SaveChangesAsync(ct);
        return Ok("Work item updated.", new JsonObject { ["updated"] = true, ["id"] = item.Id.ToString(), ["status"] = status });
    }

    private async Task<NalvaToolOutcome> SupportTicket(string agentKey, Guid executionId, JsonElement args, CancellationToken ct)
    {
        if (_support is not null)
        {
            var result = await _support.ReceiveInboundAsync(new SupportInboundMessageRequest
            {
                Channel = NalvaArgs.Str(args, "channel") ?? SupportChannels.Sandbox,
                Subject = NalvaArgs.Str(args, "title") ?? NalvaArgs.Str(args, "subject") ?? "Support ticket",
                Body = NalvaArgs.Str(args, "body") ?? NalvaArgs.Str(args, "description") ?? "Opened from the workforce.",
                CustomerId = NalvaArgs.GuidOf(args, "customerId"),
                CustomerEmail = NalvaArgs.Str(args, "customerEmail"),
                OrderId = NalvaArgs.GuidOf(args, "orderId"),
                OrderNumber = NalvaArgs.Str(args, "orderNumber"),
                IdempotencyKey = NalvaArgs.Str(args, "idempotencyKey") ?? $"tool-ticket:{executionId:N}",
                ProcessWithAgent = NalvaArgs.Bool(args, "process") != false
            }, ct);

            var ticket = await _support.GetTicketAsync(result.TicketId, ct);
            if (ticket is not null)
            {
                var row = await _db.SupportTickets.FirstOrDefaultAsync(x => x.Id == ticket.Id, ct);
                if (row is not null)
                {
                    row.ExecutionId = executionId == Guid.Empty ? row.ExecutionId : executionId;
                    row.AssignedAgentKey = agentKey;
                    await _db.SaveChangesAsync(ct);
                }
            }

            return Ok(result.Summary, new JsonObject
            {
                ["ticketId"] = result.TicketId.ToString(),
                ["ticketNumber"] = result.TicketNumber,
                ["status"] = result.Status,
                ["responseSent"] = result.ResponseSent,
                ["escalated"] = result.Escalated,
                ["deliveryStatus"] = result.ResponseDeliveryStatus
            });
        }

        var draft = await Draft(agentKey, executionId, "SupportTicket", NalvaArgs.Str(args, "title") ?? "Support ticket", NalvaArgs.Str(args, "body") ?? "Opened from the workforce.", ct);
        if (_events is not null && draft.Success)
            await _events.EnqueueAsync(NalvaEventNames.SupportTicketCreated, draft.Data.ToJsonString(), "SupportTicketCreated:" + executionId.ToString("N"), ct);
        return draft;
    }

    private async Task<NalvaToolOutcome> UpdateSupportTicketTool(JsonElement args, CancellationToken ct)
    {
        if (_support is null)
            return await UpdateWorkItem(args, "Open", ct);

        var id = NalvaArgs.GuidOf(args, "ticketId") ?? NalvaArgs.GuidOf(args, "id");
        if (id is null)
            return Ok("No ticket id was supplied.", new JsonObject { ["updated"] = false });

        var updated = await _support.UpdateTicketAsync(id.Value, new SupportTicketUpdateRequest
        {
            Status = NalvaArgs.Str(args, "status"),
            Priority = NalvaArgs.Str(args, "priority"),
            Category = NalvaArgs.Str(args, "category"),
            AssignedUserId = NalvaArgs.Str(args, "assignedUserId"),
            AiSummary = NalvaArgs.Str(args, "summary"),
            RecommendedAction = NalvaArgs.Str(args, "recommendedAction"),
            RelatedOrderId = NalvaArgs.GuidOf(args, "orderId")
        }, ct);

        return Ok("Support ticket updated.", new JsonObject
        {
            ["updated"] = true,
            ["ticketId"] = updated.Id.ToString(),
            ["status"] = updated.Status,
            ["priority"] = updated.Priority,
            ["category"] = updated.Category
        });
    }

    private async Task<NalvaToolOutcome> CreateSupportResponseDraft(string agentKey, Guid executionId, JsonElement args, CancellationToken ct)
    {
        var body = NalvaArgs.Str(args, "body") ?? "Reply draft. It was not sent.";
        var ticketId = NalvaArgs.GuidOf(args, "ticketId") ?? NalvaArgs.GuidOf(args, "id");
        if (_support is not null && ticketId is Guid id)
        {
            var message = await _support.AddReplyAsync(id, new SupportReplyRequest
            {
                Body = body,
                IsInternalNote = true,
                SendToCustomer = false
            }, ct);
            return Ok("Stored an internal response draft. It was not sent.", new JsonObject
            {
                ["ticketId"] = id.ToString(),
                ["messageId"] = message.Id.ToString(),
                ["sent"] = false,
                ["deliveryStatus"] = message.DeliveryStatus
            });
        }

        return await Draft(agentKey, executionId, "Response", "Customer reply draft", body, ct);
    }

    private async Task<NalvaToolOutcome> SendSupportReplyTool(JsonElement args, CancellationToken ct)
    {
        if (_support is null)
            return Fail("Support agent service is unavailable.");

        var id = NalvaArgs.GuidOf(args, "ticketId") ?? NalvaArgs.GuidOf(args, "id");
        var body = NalvaArgs.Str(args, "body");
        if (id is null || string.IsNullOrWhiteSpace(body))
            return Fail("ticketId and body are required.");

        var message = await _support.AddReplyAsync(id.Value, new SupportReplyRequest
        {
            Body = body,
            SendToCustomer = true,
            Channel = NalvaArgs.Str(args, "channel")
        }, ct);

        return Ok(
            message.DeliveryStatus is SupportDeliveryStatuses.Sent or SupportDeliveryStatuses.Delivered or SupportDeliveryStatuses.Simulated
                ? "Support reply was handed to the channel adapter."
                : "Support reply was not delivered.",
            new JsonObject
            {
                ["messageId"] = message.Id.ToString(),
                ["deliveryStatus"] = message.DeliveryStatus,
                ["sent"] = message.DeliveryStatus is SupportDeliveryStatuses.Sent or SupportDeliveryStatuses.Delivered or SupportDeliveryStatuses.Simulated,
                ["error"] = message.Error
            });
    }

    private async Task<NalvaToolOutcome> EscalateSupportTicketTool(JsonElement args, CancellationToken ct)
    {
        if (_support is null)
            return await UpdateWorkItem(args, "Escalated", ct);

        var id = NalvaArgs.GuidOf(args, "ticketId") ?? NalvaArgs.GuidOf(args, "id");
        if (id is null)
            return Ok("No ticket id was supplied.", new JsonObject { ["escalated"] = false });

        var ticket = await _support.EscalateAsync(id.Value, new SupportEscalateRequest
        {
            Reason = NalvaArgs.Str(args, "reason") ?? "Escalated by support agent.",
            Summary = NalvaArgs.Str(args, "summary"),
            RecommendedAction = NalvaArgs.Str(args, "recommendedAction"),
            Priority = NalvaArgs.Str(args, "priority") ?? SupportPriorities.High
        }, ct);

        return Ok("Support ticket escalated and owner notified when possible.", new JsonObject
        {
            ["escalated"] = true,
            ["ticketId"] = ticket.Id.ToString(),
            ["status"] = ticket.Status,
            ["reason"] = ticket.EscalationReason
        });
    }

    private async Task<NalvaToolOutcome> ResolveSupportTicketTool(JsonElement args, CancellationToken ct)
    {
        if (_support is null)
            return Fail("Support agent service is unavailable.");

        var id = NalvaArgs.GuidOf(args, "ticketId") ?? NalvaArgs.GuidOf(args, "id");
        if (id is null)
            return Fail("ticketId is required.");

        var ticket = await _support.ResolveAsync(id.Value, new SupportResolveRequest
        {
            Resolution = NalvaArgs.Str(args, "resolution") ?? "Resolved by support agent.",
            Close = NalvaArgs.Bool(args, "close") == true,
            ScheduleFollowUp = NalvaArgs.Bool(args, "scheduleFollowUp") != false
        }, ct);

        return Ok("Support ticket resolved.", new JsonObject
        {
            ["ticketId"] = ticket.Id.ToString(),
            ["status"] = ticket.Status,
            ["resolution"] = ticket.Resolution
        });
    }

    private async Task<NalvaToolOutcome> ScheduleSupportFollowUpTool(JsonElement args, CancellationToken ct)
    {
        if (_support is null)
            return Fail("Support agent service is unavailable.");

        var id = NalvaArgs.GuidOf(args, "ticketId") ?? NalvaArgs.GuidOf(args, "id");
        if (id is null)
            return Fail("ticketId is required.");

        var hours = NalvaArgs.Int(args, "hours") ?? 24;
        var followUp = await _support.ScheduleFollowUpAsync(id.Value, new SupportFollowUpRequest
        {
            Kind = NalvaArgs.Str(args, "kind") ?? "CustomerCheckIn",
            MessageTemplate = NalvaArgs.Str(args, "message") ?? "We wanted to check whether your issue was resolved.",
            DueAt = DateTime.UtcNow.AddHours(Math.Clamp(hours, 1, 168)),
            Channel = NalvaArgs.Str(args, "channel"),
            DedupKey = NalvaArgs.Str(args, "dedupKey"),
            RespectBusinessHours = NalvaArgs.Bool(args, "respectBusinessHours") != false
        }, ct);

        return Ok("Support follow-up scheduled.", new JsonObject
        {
            ["followUpId"] = followUp.Id.ToString(),
            ["dueAt"] = followUp.DueAt,
            ["status"] = followUp.Status
        });
    }

    private async Task<NalvaToolOutcome> ReceiveSupportMessageTool(JsonElement args, CancellationToken ct)
    {
        if (_support is null)
            return Fail("Support agent service is unavailable.");

        var result = await _support.ReceiveInboundAsync(new SupportInboundMessageRequest
        {
            Channel = NalvaArgs.Str(args, "channel") ?? SupportChannels.Sandbox,
            Body = NalvaArgs.Str(args, "body") ?? "",
            Subject = NalvaArgs.Str(args, "subject"),
            CustomerId = NalvaArgs.GuidOf(args, "customerId"),
            CustomerEmail = NalvaArgs.Str(args, "customerEmail"),
            OrderId = NalvaArgs.GuidOf(args, "orderId"),
            OrderNumber = NalvaArgs.Str(args, "orderNumber"),
            ExternalThreadId = NalvaArgs.Str(args, "threadId"),
            ExternalMessageId = NalvaArgs.Str(args, "messageId"),
            IdempotencyKey = NalvaArgs.Str(args, "idempotencyKey"),
            ProcessWithAgent = true
        }, ct);

        return Ok(result.Summary, new JsonObject
        {
            ["ticketId"] = result.TicketId.ToString(),
            ["ticketNumber"] = result.TicketNumber,
            ["status"] = result.Status,
            ["responseSent"] = result.ResponseSent,
            ["escalated"] = result.Escalated,
            ["deliveryStatus"] = result.ResponseDeliveryStatus
        });
    }

    private async Task<NalvaToolOutcome> CreateCalendarEventTool(
        string agentKey,
        Guid executionId,
        JsonElement args,
        string argsJson,
        CancellationToken ct)
    {
        if (_integrations is not null)
        {
            var title = NalvaArgs.Str(args, "title") ?? "Meeting";
            var start = NalvaArgs.Str(args, "start") ?? NalvaArgs.Str(args, "startTime");
            var end = NalvaArgs.Str(args, "end") ?? NalvaArgs.Str(args, "endTime");
            var timeZone = NalvaArgs.Str(args, "timeZone") ?? NalvaArgs.Str(args, "timezone") ?? "UTC";
            var description = NalvaArgs.Str(args, "description") ?? NalvaArgs.Str(args, "body") ?? "";
            if (!string.IsNullOrWhiteSpace(start) && !string.IsNullOrWhiteSpace(end))
            {
                var payload = new JsonObject
                {
                    ["summary"] = title,
                    ["description"] = description,
                    ["start"] = new JsonObject { ["dateTime"] = start, ["timeZone"] = timeZone },
                    ["end"] = new JsonObject { ["dateTime"] = end, ["timeZone"] = timeZone }
                };
                var attendees = NalvaArgs.Str(args, "attendees") ?? NalvaArgs.Str(args, "invitees");
                if (!string.IsNullOrWhiteSpace(attendees))
                {
                    var list = new JsonArray();
                    foreach (var email in attendees.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        list.Add(new JsonObject { ["email"] = email });
                    payload["attendees"] = list;
                }

                var google = await _integrations.ExecuteAsync("google-calendar", "create", payload.ToJsonString(), true, ct);
                if (google.Performed)
                    return Ok(google.Message, google.Data);
                if (google.Status is not ("not_connected" or "not_enabled" or "unknown_provider" or "needs_reauth"))
                    return Fail(google.Message);

                var outlook = await _integrations.ExecuteAsync("microsoft-calendar", "create",
                    new JsonObject
                    {
                        ["subject"] = title,
                        ["body"] = new JsonObject { ["contentType"] = "Text", ["content"] = description },
                        ["start"] = new JsonObject { ["dateTime"] = start, ["timeZone"] = timeZone },
                        ["end"] = new JsonObject { ["dateTime"] = end, ["timeZone"] = timeZone }
                    }.ToJsonString(), true, ct);
                if (outlook.Performed)
                    return Ok(outlook.Message, outlook.Data);
                if (outlook.Status is not ("not_connected" or "not_enabled" or "unknown_provider" or "needs_reauth"))
                    return Fail(outlook.Message);
            }
        }

        return await Draft(
            agentKey,
            executionId,
            "Calendar",
            NalvaArgs.Str(args, "title") ?? "Calendar note",
            "Internal note only. No calendar invitation was sent because no calendar integration is connected or start/end were missing.",
            ct);
    }

    private async Task<NalvaToolOutcome> SendWhatsAppMessageTool(string argsJson, CancellationToken ct)
    {
        if (_integrations is null)
        {
            return Ok(
                "Approved. WhatsApp is not connected, so nothing was sent.",
                new JsonObject { ["performed"] = false, ["integration"] = "prepared", ["action"] = "SendWhatsAppMessage" });
        }

        var node = JsonNode.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson) as JsonObject ?? new JsonObject();
        var operation = !string.IsNullOrWhiteSpace(node["templateName"]?.GetValue<string>() ?? node["template"]?.GetValue<string>())
            ? "sendTemplate"
            : "sendText";
        var result = await _integrations.ExecuteAsync("whatsapp", operation, argsJson, true, ct);
        if (result.Status is "not_connected" or "not_enabled" or "unknown_provider")
        {
            return Ok(
                "Approved. WhatsApp is not connected, so nothing was sent.",
                new JsonObject { ["performed"] = false, ["integration"] = "prepared", ["action"] = "SendWhatsAppMessage" });
        }

        return new NalvaToolOutcome
        {
            Success = result.Performed,
            Error = result.Performed ? null : result.Message,
            ErrorClass = result.Performed ? NalvaErrorClass.None : NalvaErrorClass.Validation,
            Idempotent = false,
            Summary = result.Message,
            Data = result.Data
        };
    }

    private async Task<NalvaToolOutcome?> TryLiveIntegrationAsync(string tool, string argsJson, CancellationToken ct)
    {
        if (_integrations is null)
            return null;
        var provider = tool switch
        {
            "SendExternalEmail" => "gmail",
            "IssueRefund" => "stripe",
            "PublishSocialPost" => "meta",
            "SendWhatsAppMessage" => "whatsapp",
            _ => ""
        };
        var operation = tool switch
        {
            "SendExternalEmail" => "send",
            "IssueRefund" => "refund",
            "PublishSocialPost" => "publish",
            "SendWhatsAppMessage" => "sendText",
            _ => ""
        };
        if (provider.Length == 0)
            return null;
        if (tool == "SendExternalEmail")
        {
            var node = JsonNode.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson) as JsonObject ?? new JsonObject();
            if (node["raw"] is null)
            {
                var body = node["body"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";
                node["raw"] = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Subject: Company message\r\n\r\n" + body));
                argsJson = node.ToJsonString();
            }
        }
        if (tool == "PublishSocialPost")
        {
            var node = JsonNode.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson) as JsonObject ?? new JsonObject();
            if (node["message"] is null)
            {
                var body = node["body"] is JsonValue value && value.TryGetValue<string>(out var text)
                    ? text
                    : node["caption"] is JsonValue caption && caption.TryGetValue<string>(out var captionText)
                        ? captionText
                        : "";
                if (string.IsNullOrWhiteSpace(body))
                    return Ok(
                        "Approved. The post has no message body, so nothing was published.",
                        new JsonObject { ["performed"] = false, ["integration"] = "prepared", ["action"] = "PublishSocialContent" });
                node["message"] = body;
                argsJson = node.ToJsonString();
            }
        }

        var result = await _integrations.ExecuteAsync(provider, operation, argsJson, true, ct);
        if (result.Status is "not_connected" or "not_enabled" or "unknown_provider")
            return null;
        return new NalvaToolOutcome
        {
            Success = result.Performed,
            Error = result.Performed ? null : result.Message,
            ErrorClass = result.Performed ? NalvaErrorClass.None : result.Status == "validation" ? NalvaErrorClass.Validation : NalvaErrorClass.Uncertain,
            Idempotent = false,
            Summary = result.Message,
            Data = result.Data
        };
    }

    private async Task<NalvaToolOutcome> RepositoryRead(string tool, string argsJson, CancellationToken ct)
    {
        if (_integrations is null)
            return Ok("No repository integration is connected. Nothing was read from GitHub.", new JsonObject { ["connected"] = false, ["integration"] = "prepared" });
        var result = await _integrations.ExecuteAsync("github", tool == "AnalyzePullRequest" ? "pulls" : "repos", argsJson, false, ct);
        if (!result.Performed)
            return Ok(result.Message, new JsonObject { ["connected"] = false, ["performed"] = false, ["status"] = result.Status });
        return Ok("Repository data was read from GitHub.", result.Data);
    }

    private async Task<NalvaToolOutcome> Draft(string agentKey, Guid executionId, string kind, string title, string body, CancellationToken ct)
    {
        var item = new NalvaWorkItem
        {
            AgentKey = agentKey,
            Kind = kind,
            Title = NalvaGuard.Clip(title, 200),
            Body = NalvaGuard.Clip(body, 4000),
            ExecutionId = executionId == Guid.Empty ? null : executionId,
            Status = "Draft"
        };
        _db.NalvaWorkItems.Add(item);
        await _db.SaveChangesAsync(ct);
        return Ok($"Stored a {kind} draft. Nothing was published, sent, or executed.", new JsonObject
        {
            ["workItemId"] = item.Id.ToString(),
            ["kind"] = kind,
            ["published"] = false,
            ["executed"] = false
        });
    }

    private static NalvaToolOutcome CompareText(JsonElement args)
    {
        var left = Tokens(NalvaArgs.Str(args, "cv") ?? NalvaArgs.Str(args, "skills"));
        var right = Tokens(NalvaArgs.Str(args, "requirements") ?? NalvaArgs.Str(args, "job"));
        var overlap = left.Intersect(right, StringComparer.OrdinalIgnoreCase).Take(20).ToArray();
        return Ok("Qualification overlap only. This is not a hiring or rejection decision.", new JsonObject
        {
            ["matchingTerms"] = new JsonArray(overlap.Select(x => (JsonNode)x).ToArray()),
            ["hiringDecision"] = false,
            ["protectedTraitsIgnored"] = true
        });
    }

    private async Task<NalvaToolOutcome> MarketingStrategy(string agentKey, JsonElement args, CancellationToken ct)
    {
        if (_marketing is null)
            return await Draft(agentKey, Guid.Empty, "Strategy", "Marketing strategy", StrategyBody(), ct);
        var campaign = await _marketing.GenerateStrategyAsync(new GenerateStrategyRequest
        {
            Title = NalvaArgs.Str(args, "title"),
            Objective = NalvaArgs.Str(args, "objective"),
            TargetAudience = NalvaArgs.Str(args, "targetAudience"),
            AgentKey = agentKey,
            CampaignId = NalvaArgs.GuidOf(args, "campaignId")
        }, ct);
        return Ok("Marketing strategy stored on campaign.", new JsonObject
        {
            ["campaignId"] = campaign.Id.ToString(),
            ["title"] = campaign.Title,
            ["strategyApproved"] = campaign.StrategyApproved,
            ["strategyBody"] = campaign.StrategyBody
        });
    }

    private async Task<NalvaToolOutcome> MarketingContentDraft(string agentKey, Guid executionId, JsonElement args, CancellationToken ct)
    {
        if (_marketing is null)
            return await Draft(agentKey, executionId, "Content", NalvaArgs.Str(args, "title") ?? "Content draft", ContentBody(agentKey, args), ct);
        var item = await _marketing.GenerateContentItemAsync(new GenerateContentItemRequest
        {
            Title = NalvaArgs.Str(args, "title"),
            Platform = NalvaArgs.Str(args, "platform") ?? "LinkedIn",
            Brief = NalvaArgs.Str(args, "brief") ?? NalvaArgs.Str(args, "body"),
            CampaignId = NalvaArgs.GuidOf(args, "campaignId"),
            AgentKey = agentKey,
            ExecutionId = executionId == Guid.Empty ? null : executionId
        }, ct);
        return Ok("Marketing content draft stored.", new JsonObject
        {
            ["contentItemId"] = item.Id.ToString(),
            ["platform"] = item.Platform,
            ["approvalStatus"] = item.ApprovalStatus,
            ["published"] = false
        });
    }

    private async Task<NalvaToolOutcome> MarketingCalendar(string agentKey, JsonElement args, CancellationToken ct)
    {
        if (_marketing is null)
            return await Draft(agentKey, Guid.Empty, "Calendar", "Content calendar", "Internal calendar draft. Nothing was scheduled externally.", ct);
        var days = (int)(NalvaArgs.Dec(args, "days") ?? 7);
        var items = await _marketing.GenerateContentCalendarAsync(new GenerateContentCalendarRequest
        {
            Days = days,
            CampaignId = NalvaArgs.GuidOf(args, "campaignId"),
            AgentKey = agentKey
        }, ct);
        return Ok($"Created {items.Count} calendar content items. Nothing was published externally.", new JsonObject
        {
            ["count"] = items.Count,
            ["campaignId"] = items.FirstOrDefault()?.CampaignId?.ToString(),
            ["published"] = false
        });
    }

    private async Task<NalvaToolOutcome> MarketingCampaignDraft(string agentKey, JsonElement args, CancellationToken ct)
    {
        if (_marketing is null)
            return await Draft(agentKey, Guid.Empty, "Campaign", NalvaArgs.Str(args, "title") ?? "Campaign draft", "Campaign draft. Nothing was published.", ct);
        var campaign = await _marketing.CreateCampaignAsync(new CreateCampaignRequest
        {
            Title = NalvaArgs.Str(args, "title") ?? "Campaign draft",
            Objective = NalvaArgs.Str(args, "objective"),
            TargetAudience = NalvaArgs.Str(args, "targetAudience"),
            StrategyBody = NalvaArgs.Str(args, "brief"),
            AgentKey = agentKey
        }, ct);
        return Ok("Campaign draft stored. Nothing was published.", new JsonObject
        {
            ["campaignId"] = campaign.Id.ToString(),
            ["status"] = campaign.Status,
            ["published"] = false
        });
    }

    private async Task<NalvaToolOutcome> UpsertBrandProfile(JsonElement args, CancellationToken ct)
    {
        if (_marketing is null)
            return await Draft("marketing", Guid.Empty, "BrandProfile", "Brand profile note", "Marketing service unavailable; brand fields were not persisted.", ct);
        var brand = await _marketing.UpsertBrandProfileAsync(new UpsertBrandProfileRequest
        {
            BusinessName = NalvaArgs.Str(args, "businessName"),
            Industry = NalvaArgs.Str(args, "industry"),
            TargetAudience = NalvaArgs.Str(args, "targetAudience"),
            BrandDescription = NalvaArgs.Str(args, "brandDescription"),
            BrandVoice = NalvaArgs.Str(args, "brandVoice"),
            BrandColorsJson = NalvaArgs.Str(args, "brandColorsJson"),
            Website = NalvaArgs.Str(args, "website"),
            CompetitorsJson = NalvaArgs.Str(args, "competitorsJson"),
            SourceNotes = NalvaArgs.Str(args, "sourceNotes")
        }, ct);
        return Ok("Brand profile updated.", new JsonObject { ["id"] = brand.Id.ToString(), ["businessName"] = brand.BusinessName });
    }

    private async Task<NalvaToolOutcome> MarketingGraphic(JsonElement args, CancellationToken ct)
    {
        if (_marketing is null)
            return await Draft("marketing", Guid.Empty, "Graphic", "Visual brief", "BriefOnly visual brief. OpenAI image generation and marketing assets were not available in this context.", ct);
        var asset = await _marketing.GenerateGraphicAsync(new GenerateGraphicRequest
        {
            ContentItemId = NalvaArgs.GuidOf(args, "contentItemId"),
            CampaignId = NalvaArgs.GuidOf(args, "campaignId"),
            Platform = NalvaArgs.Str(args, "platform"),
            Prompt = NalvaArgs.Str(args, "prompt"),
            Brief = NalvaArgs.Str(args, "brief")
        }, ct);
        return Ok(asset.Source == "BriefOnly"
            ? "Stored a visual brief only. OpenAI image generation was not available."
            : "Generated a graphic asset.", new JsonObject
        {
            ["assetId"] = asset.Id.ToString(),
            ["source"] = asset.Source,
            ["kind"] = asset.Kind,
            ["brandCompliant"] = asset.BrandCompliant
        });
    }

    private async Task<NalvaToolOutcome> ScheduleMarketingPost(JsonElement args, CancellationToken ct)
    {
        if (_marketing is null)
            return Ok("Marketing service unavailable. Nothing was scheduled.", new JsonObject { ["scheduled"] = false });
        var id = NalvaArgs.GuidOf(args, "contentItemId") ?? NalvaArgs.GuidOf(args, "id");
        if (id is null)
            return Fail("contentItemId is required.");
        var job = await _marketing.ScheduleApprovedContentAsync(id.Value, new ScheduleContentRequest
        {
            RunAtUtc = null,
            ConnectedAccountProvider = NalvaArgs.Str(args, "provider") ?? "sandbox",
            ConnectedAccountExternalId = NalvaArgs.Str(args, "accountExternalId") ?? "sandbox",
            IdempotencyKey = NalvaArgs.Str(args, "idempotencyKey")
        }, ct);
        return Ok("Publish job scheduled.", new JsonObject
        {
            ["jobId"] = job.Id.ToString(),
            ["status"] = job.Status,
            ["provider"] = job.Provider,
            ["idempotencyKey"] = job.IdempotencyKey
        });
    }

    private async Task<NalvaToolOutcome> SyncMarketingAnalytics(JsonElement args, CancellationToken ct)
    {
        if (_marketing is null)
            return Ok("Marketing service unavailable. No analytics were synced.", new JsonObject { ["synced"] = false, ["externalAnalytics"] = false });
        var result = await _marketing.SyncAnalyticsAsync(new SyncAnalyticsRequest
        {
            ContentItemId = NalvaArgs.GuidOf(args, "contentItemId"),
            CampaignId = NalvaArgs.GuidOf(args, "campaignId"),
            AllowSandbox = true
        }, ct);
        return Ok(result.Message, new JsonObject
        {
            ["snapshotsCreated"] = result.SnapshotsCreated,
            ["dataSource"] = result.DataSource
        });
    }

    private async Task<NalvaToolOutcome> MarketingPerformanceReport(JsonElement args, CancellationToken ct)
    {
        if (_marketing is null)
            return await CampaignAnalytics(ct);
        var report = await _marketing.GeneratePerformanceReportAsync(NalvaArgs.GuidOf(args, "campaignId"), ct);
        return Ok(report.Body, new JsonObject
        {
            ["title"] = report.Title,
            ["fabricated"] = report.Fabricated,
            ["verifiedMetricsJson"] = report.VerifiedMetricsJson,
            ["unavailableMetricsJson"] = report.UnavailableMetricsJson,
            ["dataSourcesJson"] = report.DataSourcesJson
        });
    }

    private async Task<NalvaToolOutcome> ShareMarketingInsights(string agentKey, JsonElement args, CancellationToken ct)
    {
        if (_marketing is null)
            return await Draft(agentKey, Guid.Empty, "MarketingInsight", "Marketing insights for sales", "Structured insight placeholder. Marketing service was unavailable.", ct);
        await _marketing.ShareInsightsWithSalesAsync(NalvaArgs.GuidOf(args, "campaignId"), agentKey, ct);
        return Ok("Stored a MarketingInsight work item for sales. This is not a freeform chat message.", new JsonObject
        {
            ["kind"] = "MarketingInsight",
            ["shared"] = true
        });
    }

    private async Task<NalvaToolOutcome> AssignMarketingObjective(string agentKey, Guid executionId, JsonElement args, CancellationToken ct)
    {
        var title = NalvaArgs.Str(args, "title") ?? NalvaArgs.Str(args, "objective") ?? "Marketing objective";
        var objective = NalvaArgs.Str(args, "objective") ?? NalvaArgs.Str(args, "campaignObjective") ?? "BrandAwareness";
        var audience = NalvaArgs.Str(args, "targetAudience");
        var brief = NalvaArgs.Str(args, "brief") ?? NalvaArgs.Str(args, "body");

        if (_marketing is null)
        {
            return await Draft(agentKey, executionId, "MarketingObjective", title,
                $"Marketing objective draft: {objective}. Marketing service unavailable; campaign was not persisted.", ct);
        }

        var campaign = await _marketing.CreateCampaignAsync(new CreateCampaignRequest
        {
            Title = title.Trim(),
            Objective = objective,
            TargetAudience = audience,
            StrategyBody = brief,
            AgentKey = "marketing"
        }, ct);

        var work = new NalvaWorkItem
        {
            AgentKey = agentKey,
            Kind = "MarketingObjective",
            Title = NalvaGuard.Clip(title, 200),
            Body = NalvaGuard.Clip(
                $"Assigned marketing objective '{objective}' as campaign {campaign.Id}. {brief}".Trim(),
                4000),
            Status = "Open",
            RelatedEntityType = "MarketingCampaign",
            RelatedEntityId = campaign.Id,
            ExecutionId = executionId == Guid.Empty ? null : executionId
        };
        _db.NalvaWorkItems.Add(work);

        Guid? queuedTaskId = null;
        var marketingAgent = await _db.NalvaCompanyAgents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.AgentKey == "marketing" && x.Status == NalvaStatuses.Active, ct);
        if (marketingAgent is not null)
        {
            var task = new NalvaTask
            {
                AgentKey = "marketing",
                Objective = $"Execute marketing objective: {title}",
                Status = NalvaStatuses.Assigned,
                RunAfter = DateTime.UtcNow,
                InitiatorUserId = null,
                Depth = 0,
                DelegationChain = agentKey,
                ToolPlanJson = JsonSerializer.Serialize(new[]
                {
                    "GetMarketingBrandProfile", "GenerateMarketingStrategy", "CreateContentCalendar", "CreateContentDraft"
                }),
                ArgumentsJson = new JsonObject
                {
                    ["campaignId"] = campaign.Id.ToString(),
                    ["title"] = title,
                    ["objective"] = objective
                }.ToJsonString()
            };
            _db.NalvaTasks.Add(task);
            queuedTaskId = task.Id;
        }

        await _db.SaveChangesAsync(ct);
        return Ok("Marketing objective assigned to the Marketing Agent.", new JsonObject
        {
            ["assigned"] = true,
            ["campaignId"] = campaign.Id.ToString(),
            ["workItemId"] = work.Id.ToString(),
            ["queuedTaskId"] = queuedTaskId?.ToString(),
            ["objective"] = objective
        });
    }

    private async Task<NalvaToolOutcome?> MarketingPublishAfterApproval(JsonElement args, CancellationToken ct)
    {
        if (_marketing is null)
            return null;
        var contentId = NalvaArgs.GuidOf(args, "contentItemId") ?? NalvaArgs.GuidOf(args, "id");
        if (contentId is null)
            return null;
        try
        {
            var content = await _marketing.GetContentAsync(contentId.Value, ct);
            if (content.ApprovalStatus is not ("Approved" or "Scheduled"))
            {
                return Ok(
                    "Content must be approved via the marketing approval flow before publish scheduling. Nothing was published.",
                    new JsonObject
                    {
                        ["performed"] = false,
                        ["contentItemId"] = content.Id.ToString(),
                        ["approvalStatus"] = content.ApprovalStatus,
                        ["requiresMarketingApproval"] = true
                    });
            }

            var job = await _marketing.ScheduleApprovedContentAsync(contentId.Value, new ScheduleContentRequest
            {
                RunAtUtc = DateTime.UtcNow,
                ConnectedAccountProvider = NalvaArgs.Str(args, "provider") ?? content.ConnectedAccountProvider ?? "sandbox",
                ConnectedAccountExternalId = NalvaArgs.Str(args, "accountExternalId") ?? content.ConnectedAccountExternalId ?? "sandbox",
                IdempotencyKey = NalvaArgs.Str(args, "idempotencyKey")
            }, ct);
            await _marketing.ProcessDuePublishJobsAsync(ct);
            return Ok("Approved publish path created a marketing publish job.", new JsonObject
            {
                ["performed"] = job.Status is "Succeeded" or "Pending" or "Running",
                ["jobId"] = job.Id.ToString(),
                ["status"] = job.Status,
                ["provider"] = job.Provider,
                ["externalPostId"] = job.ExternalPostId
            });
        }
        catch (AppException ex)
        {
            return Ok(ex.Message, new JsonObject { ["performed"] = false, ["error"] = ex.Message });
        }
    }

    private static string StrategyBody() =>
        "Strategy draft based on stored segments and customers. No external market numbers were added.";

    private static string ContentBody(string agentKey, JsonElement args)
    {
        var note = NalvaArgs.Str(args, "brief") ?? "Draft from company context.";
        var body = NalvaGuard.Clip(note, 2000);
        return string.Equals(agentKey, "legal", StringComparison.OrdinalIgnoreCase)
            ? body + " This is not legal advice."
            : body;
    }

    private static string EngineeringBody(string tool, JsonElement args)
    {
        var note = NalvaGuard.Clip(NalvaArgs.Str(args, "requirements") ?? NalvaArgs.Str(args, "diff") ?? "No source text was supplied.", 2000);
        return $"{tool} draft for human review. No repository was changed and no code was executed. Source note: {note}";
    }

    private static string InterviewBody(JsonElement args)
    {
        var job = NalvaGuard.Clip(NalvaArgs.Str(args, "requirements") ?? "the role", 500);
        return $"Questions about experience relevant to: {job}. Do not ask about protected personal characteristics. A person decides who is hired.";
    }

    private static HashSet<string> Tokens(string? value)
    {
        var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "male", "female", "gender", "age", "religion", "race", "nationality", "married", "disability", "pregnant"
        };
        if (string.IsNullOrWhiteSpace(value))
            return [];
        return value.Split([' ', ',', '.', ';', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => x.Length > 2 && !blocked.Contains(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static NalvaToolOutcome List(string name, IEnumerable<JsonObject> rows)
    {
        var array = new JsonArray();
        foreach (var row in rows)
            array.Add(row);
        return Ok($"Returned {array.Count} stored {name}.", new JsonObject { [name] = array });
    }

    private async Task<NalvaToolOutcome> RetrieveSharedContext(JsonElement args, CancellationToken ct)
    {
        var kind = NalvaArgs.Str(args, "kind") ?? "BusinessDecision";
        var query = NalvaArgs.Str(args, "query");
        var rows = await _db.NalvaMemoryRecords.AsNoTracking()
            .Where(x => x.Kind == kind || x.Kind == "SharedBusinessContext" || x.Kind == "BusinessPolicy")
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(query))
            rows = rows.Where(x => x.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || x.Content.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(10).ToList();
        return Ok("Shared business context from tenant-scoped memory only.", new JsonObject
        {
            ["count"] = rows.Count,
            ["items"] = new JsonArray(rows.Select(x => (JsonNode)new JsonObject
            {
                ["id"] = x.Id.ToString(),
                ["agentKey"] = x.AgentKey,
                ["kind"] = x.Kind,
                ["title"] = x.Title,
                ["excerpt"] = NalvaGuard.Clip(x.Content, 240)
            }).ToArray())
        });
    }

    private async Task<NalvaToolOutcome> StoreSharedDecision(string agentKey, Guid executionId, JsonElement args, CancellationToken ct)
    {
        var title = NalvaArgs.Str(args, "title") ?? "Business decision";
        var content = NalvaArgs.Str(args, "content") ?? NalvaArgs.Str(args, "decision") ?? "";
        if (string.IsNullOrWhiteSpace(content))
            return Fail("A decision content body is required.", NalvaErrorClass.Validation);
        var row = new NalvaMemoryRecord
        {
            AgentKey = string.IsNullOrWhiteSpace(agentKey) ? "ceo" : agentKey,
            Kind = "BusinessDecision",
            Title = NalvaGuard.Clip(title, 200),
            Content = NalvaGuard.Clip(content, 4000),
            ExecutionId = executionId == Guid.Empty ? null : executionId,
            ExpiresAt = DateTime.UtcNow.AddDays(180)
        };
        _db.NalvaMemoryRecords.Add(row);
        await _db.SaveChangesAsync(ct);
        return Ok("Stored a tenant-scoped business decision for later agents.", new JsonObject
        {
            ["memoryId"] = row.Id.ToString(),
            ["kind"] = row.Kind,
            ["verified"] = true
        });
    }

    private async Task<NalvaToolOutcome> VerifyOrderReadiness(JsonElement args, CancellationToken ct)
    {
        var orderResult = await OrderDetails(args, ct);
        if (orderResult.Data["found"]?.GetValue<bool?>() != true)
            return Ok("Order readiness could not be verified because the order was not found.", new JsonObject
            {
                ["ready"] = false,
                ["verified"] = false,
                ["order"] = orderResult.Data.DeepClone()
            });

        var orderId = Guid.Parse(orderResult.Data["id"]!.GetValue<string>()!);
        var customerId = Guid.Parse(orderResult.Data["customerId"]!.GetValue<string>()!);
        var items = await _db.OrderItems.AsNoTracking().Where(x => x.OrderId == orderId).ToListAsync(ct);
        var stockBlockers = new JsonArray();
        foreach (var item in items)
        {
            var stock = await _db.Inventories.AsNoTracking().FirstOrDefaultAsync(x => x.ProductId == item.ProductId, ct);
            var available = stock?.CurrentStock ?? 0;
            if (available < item.Quantity)
            {
                stockBlockers.Add(new JsonObject
                {
                    ["productId"] = item.ProductId.ToString(),
                    ["needed"] = item.Quantity,
                    ["available"] = available
                });
            }
        }

        var outstanding = await _db.Invoices.AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.OutstandingAmount > 0)
            .SumAsync(x => (decimal?)x.OutstandingAmount, ct) ?? 0m;
        var payments = await _db.Payments.AsNoTracking().CountAsync(x => x.CustomerId == customerId, ct);

        var ready = stockBlockers.Count == 0;
        return Ok(ready
            ? "Order verified against stock and finance records. Fulfillment still requires owner policy/approval."
            : "Order has stock blockers. Do not mark fulfilled.",
            new JsonObject
            {
                ["ready"] = ready,
                ["verified"] = true,
                ["orderId"] = orderId.ToString(),
                ["customerId"] = customerId.ToString(),
                ["paymentStatus"] = orderResult.Data["paymentStatus"]?.DeepClone(),
                ["outstandingBalance"] = outstanding,
                ["paymentRecordsFound"] = payments,
                ["stockBlockers"] = stockBlockers,
                ["fulfilled"] = false,
                ["paid"] = string.Equals(orderResult.Data["paymentStatus"]?.GetValue<string>(), "Paid", StringComparison.OrdinalIgnoreCase)
            });
    }

    private async Task<NalvaToolOutcome> PrepareInvoiceDraft(string agentKey, Guid executionId, JsonElement args, CancellationToken ct)
    {
        var customerId = NalvaArgs.GuidOf(args, "customerId");
        var orderId = NalvaArgs.GuidOf(args, "orderId");
        var amount = NalvaArgs.Dec(args, "amount");
        Customer? customer = null;
        if (customerId is Guid cid)
            customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cid, ct);
        Order? order = null;
        if (orderId is Guid oid)
            order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == oid, ct);

        if (customer is null && order is null)
            return Ok("Invoice draft was not created. Supply a verified customerId or orderId from company records.", new JsonObject
            {
                ["created"] = false,
                ["posted"] = false,
                ["reason"] = "missing_verified_transaction"
            });

        if (order is not null)
        {
            amount ??= order.GrandTotal;
            customerId ??= order.CustomerId;
        }

        if (amount is null or <= 0)
            return Ok("Invoice draft was not created. No verified positive amount was available.", new JsonObject
            {
                ["created"] = false,
                ["posted"] = false,
                ["reason"] = "missing_amount"
            });

        var body = $"Invoice draft for customer {customerId}. Amount {amount}. Order {orderId}. Not posted to the ledger.";
        var draft = await Draft(agentKey, executionId, "InvoiceDraft", $"Invoice draft ({amount})", body, ct);
        draft.Data["created"] = true;
        draft.Data["posted"] = false;
        draft.Data["amount"] = amount;
        draft.Data["customerId"] = customerId?.ToString();
        draft.Data["orderId"] = orderId?.ToString();
        draft.Data["currency"] = "PKR";
        draft.Data["verified"] = true;
        return draft;
    }

    private async Task<NalvaToolOutcome> ClassifySupportComplaint(JsonElement args, CancellationToken ct)
    {
        if (_support is null)
            return Ok("Support classification unavailable.", new JsonObject { ["classified"] = false });

        var ticketId = NalvaArgs.GuidOf(args, "ticketId") ?? NalvaArgs.GuidOf(args, "id");
        var text = NalvaArgs.Str(args, "complaint") ?? NalvaArgs.Str(args, "message") ?? "";
        string category = "general";
        string urgency = "medium";
        if (text.Contains("refund", StringComparison.OrdinalIgnoreCase) || text.Contains("charge", StringComparison.OrdinalIgnoreCase))
        {
            category = "billing";
            urgency = "high";
        }
        else if (text.Contains("damaged", StringComparison.OrdinalIgnoreCase) || text.Contains("missing", StringComparison.OrdinalIgnoreCase) || text.Contains("delivery", StringComparison.OrdinalIgnoreCase))
        {
            category = "fulfillment";
            urgency = "high";
        }
        else if (text.Contains("urgent", StringComparison.OrdinalIgnoreCase) || text.Contains("asap", StringComparison.OrdinalIgnoreCase))
            urgency = "high";

        if (ticketId is Guid id)
        {
            await _support.UpdateTicketAsync(id, new SupportTicketUpdateRequest
            {
                Category = category,
                Priority = urgency
            }, ct);
        }

        return Ok("Complaint classified from supplied text and stored ticket fields when present.", new JsonObject
        {
            ["classified"] = true,
            ["ticketId"] = ticketId?.ToString(),
            ["category"] = category,
            ["urgency"] = urgency,
            ["verified"] = ticketId is not null,
            ["requiresFinance"] = category == "billing",
            ["requiresInventory"] = category == "fulfillment"
        });
    }

    private static NalvaToolOutcome CoordinateObjective(JsonElement args)
    {
        var objective = NalvaArgs.Str(args, "objective") ?? "";
        var match = NalvaObjectiveRouter.Match(objective);
        return Ok(match.CanStart
            ? $"Mapped objective to workflow '{match.WorkflowKey}'."
            : match.BlockReason ?? "Objective needs clarification.",
            new JsonObject
            {
                ["workflowKey"] = match.WorkflowKey,
                ["workflowName"] = match.Name,
                ["confidence"] = match.Confidence,
                ["canStart"] = match.CanStart,
                ["blockReason"] = match.BlockReason,
                ["requiredAgents"] = new JsonArray(match.RequiredAgents.Select(x => (JsonNode)x).ToArray()),
                ["clarifyingQuestions"] = new JsonArray(match.ClarifyingQuestions.Select(x => (JsonNode)x).ToArray())
            });
    }

    private static NalvaToolOutcome Ok(string summary, JsonObject data) =>
        new() { Success = true, Summary = summary, Data = data };

    private static NalvaToolOutcome Fail(string error, string errorClass = NalvaErrorClass.Permanent) =>
        new() { Success = false, Error = error, ErrorClass = errorClass, Summary = error };
}

internal static class NalvaArgs
{
    public static JsonElement Parse(string? json)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return document.RootElement.Clone();
    }

    public static string? Str(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var value))
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    public static Guid? GuidOf(JsonElement args, string name)
    {
        var text = Str(args, name);
        return Guid.TryParse(text, out var id) ? id : null;
    }

    public static decimal? Dec(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
            return number;
        return value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out var parsed) ? parsed : null;
    }

    public static bool? Bool(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.True) return true;
        if (value.ValueKind == JsonValueKind.False) return false;
        return value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed) ? parsed : null;
    }

    public static int? Int(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed) ? parsed : null;
    }

    public static DateTime? Date(JsonElement args, string name)
    {
        var text = Str(args, name);
        if (string.IsNullOrWhiteSpace(text))
            return null;
        return DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? DateTime.SpecifyKind(parsed.ToUniversalTime(), DateTimeKind.Utc)
            : null;
    }

    public static IReadOnlyList<string> Strings(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            return [];
        return value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).Where(x => x.Length > 0).ToArray();
    }
}

internal static class NalvaJson
{
    public static IReadOnlyList<string> Strings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value);
}

