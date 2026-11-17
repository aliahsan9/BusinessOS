using System.Text.Json;
using System.Text.Json.Nodes;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Integrations;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Application.Features.Operations.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
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

    public NalvaToolGateway(BusinessOSDbContext db, ITenantProvider tenant, ICrmOperationsService crm, INalvaEventOutbox? events = null, IIntegrationPlatform? integrations = null)
    {
        _db = db;
        _tenant = tenant;
        _crm = crm;
        _events = events;
        _integrations = integrations;
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
        if (tool.RequiresApproval && !approvalGranted)
        {
            return new NalvaToolOutcome
            {
                ApprovalRequired = true,
                ApprovalAction = tool.ApprovalAction,
                Risk = tool.Risk,
                Summary = $"Waiting for a person to approve {tool.ApprovalAction}.",
                Data = new JsonObject
                {
                    ["tool"] = tool.Name,
                    ["action"] = tool.ApprovalAction,
                    ["risk"] = tool.Risk
                }
            };
        }

        if (tool.RequiresApproval && tool.Integration == "prepared")
        {
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
                "GetFinancialSummary" => await FinancialSummary(cancellationToken),
                "GetProjectSummary" => await ProjectSummary(cancellationToken),
                "GetInventorySummary" => await InventorySummary(cancellationToken),
                "GetEmployeeSummary" => await EmployeeSummary(cancellationToken),
                "GetAgentStatus" => await AgentStatus(cancellationToken),
                "GetAgentExecutionResults" => await ExecutionResults(cancellationToken),
                "CreateAgentTask" or "DelegateAgentTask" => await Delegate(tool.Name, args, agentKey, delegationChain, depth, cancellationToken),
                "GetCompanyProfile" => await CompanyProfile(cancellationToken),
                "GetTargetCustomers" => await Customers(cancellationToken),
                "GetCustomerSegments" => await Segments(cancellationToken),
                "ResearchMarket" => await ResearchMarket(cancellationToken),
                "ResearchCompetitors" => await KnowledgeSearch(agentKey, "competitor", cancellationToken),
                "GenerateMarketingStrategy" => await Draft(agentKey, executionId, "Strategy", "Marketing strategy", StrategyBody(), cancellationToken),
                "CreateContentDraft" => await Draft(agentKey, executionId, "Content", NalvaArgs.Str(args, "title") ?? "Content draft", ContentBody(agentKey, args), cancellationToken),
                "CreateContentCalendar" => await Draft(agentKey, executionId, "Calendar", "Content calendar", "Internal calendar draft. Nothing was scheduled externally.", cancellationToken),
                "CreateCampaignDraft" => await Draft(agentKey, executionId, "Campaign", NalvaArgs.Str(args, "title") ?? "Campaign draft", "Campaign draft. Nothing was published.", cancellationToken),
                "GetCampaignAnalytics" => await CampaignAnalytics(cancellationToken),
                "SearchAuthorizedLeadSources" => await LeadSources(cancellationToken),
                "ResearchProspect" or "GetLeadDetails" => await LeadDetails(args, cancellationToken),
                "CreateLead" => await CreateLead(args, cancellationToken),
                "UpdateLead" => await UpdateLead(args, cancellationToken),
                "CreateDeal" => await CreateDeal(args, cancellationToken),
                "CreateFollowUp" => await CreateFollowUp(agentKey, args, cancellationToken),
                "CreateProposalDraft" => await Draft(agentKey, executionId, "Proposal", "Proposal draft", "Proposal draft. It was not sent.", cancellationToken),
                "CreateQuotationDraft" => await QuotationDraft(args, cancellationToken),
                "GetSalesPipeline" => await Pipeline(cancellationToken),
                "GetConversionAnalytics" => await Conversion(cancellationToken),
                "SendExternalEmail" or "PublishSocialPost" or "SpendAdBudget" or "IssueRefund" or "ExecuteProductionDeployment" or "ChangeEmployeeRecord" =>
                    Ok("No external effect was performed.", new JsonObject { ["performed"] = false }),
                "SearchKnowledgeBase" or "GetJobRequirements" => await KnowledgeSearch(agentKey, NalvaArgs.Str(args, "query"), cancellationToken),
                "GetCustomerDetails" => await CustomerDetails(args, cancellationToken),
                "GetOrderDetails" => await OrderDetails(args, cancellationToken),
                "GetCustomerHistory" => await CustomerHistory(args, cancellationToken),
                "CreateSupportTicket" => await SupportTicket(agentKey, executionId, args, cancellationToken),
                "UpdateSupportTicket" => await UpdateWorkItem(args, "Open", cancellationToken),
                "CreateResponseDraft" => await Draft(agentKey, executionId, "Response", "Customer reply draft", NalvaArgs.Str(args, "body") ?? "Reply draft. It was not sent.", cancellationToken),
                "EscalateTicket" => await UpdateWorkItem(args, "Escalated", cancellationToken),
                "GetProjectDetails" or "GetProjectRequirements" => await ProjectDetails(args, tool.Name == "GetProjectRequirements", cancellationToken),
                "CreateDevelopmentTask" or "CreateTask" => await CreateTask(args, cancellationToken),
                "AnalyzeRepository" or "AnalyzePullRequest" => await RepositoryRead(tool.Name, argsJson, cancellationToken),
                "ReviewCodeChanges" or "GenerateCode" or "GenerateUnitTests" or "GenerateTechnicalDocumentation" or "GenerateCodeReviewReport" =>
                    await Draft(agentKey, executionId, "Engineering", tool.Name, EngineeringBody(tool.Name, args), cancellationToken),
                "GetExpenseRecords" => await Expenses(cancellationToken),
                "GetInvoiceRecords" => await Invoices(cancellationToken),
                "GetPaymentRecords" => await Payments(cancellationToken),
                "GetOutstandingReceivables" => await Receivables(cancellationToken),
                "GetOutstandingPayables" => await Payables(cancellationToken),
                "CalculateProfitability" => await Profit(cancellationToken),
                "ForecastCashFlow" => await CashFlow(cancellationToken),
                "GenerateFinancialReport" or "GenerateBusinessReport" or "GenerateHRReport" or "GenerateDailySummary" => await Report(agentKey, executionId, tool.Name, cancellationToken),
                "AnalyzeCandidateCV" or "CompareCandidateQualifications" => CompareText(args),
                "CreateInterviewQuestionSet" => await Draft(agentKey, executionId, "Interview", "Interview questions", InterviewBody(args), cancellationToken),
                "CreateOnboardingTask" => await Draft(agentKey, executionId, "Onboarding", NalvaArgs.Str(args, "title") ?? "Onboarding item", "Checklist item for a person to complete. This is not a hiring decision.", cancellationToken),
                "GetEmployeeDetails" => await EmployeeDetails(args, cancellationToken),
                "GetAttendanceSummary" => await Attendance(cancellationToken),
                "GetSalesAnalytics" => await SalesSummary(cancellationToken),
                "GetRevenueAnalytics" => await Revenue(cancellationToken),
                "GetInventoryAnalytics" or "GetInventoryLevels" or "GetLowStockProducts" => await InventoryDetails(tool.Name, cancellationToken),
                "GetCustomerAnalytics" => await CustomerAnalytics(cancellationToken),
                "GetProjectAnalytics" => await ProjectSummary(cancellationToken),
                "GetExpenseAnalytics" => await ExpenseAnalytics(cancellationToken),
                "IdentifyBusinessTrends" => await Trends(cancellationToken),
                "GetSupplierDetails" => await SupplierDetails(args, cancellationToken),
                "CompareSupplierPrices" => await SupplierPrices(args, cancellationToken),
                "GetPurchaseHistory" or "GetPendingPurchaseOrders" => await Purchases(tool.Name == "GetPendingPurchaseOrders", cancellationToken),
                "ForecastProductDemand" => await Demand(cancellationToken),
                "CreatePurchaseOrderDraft" => await PurchaseDraft(args, "Draft", cancellationToken),
                "CreatePurchaseOrder" => await PurchaseDraft(args, "Pending", cancellationToken),
                "GetUpcomingTasks" or "GetCalendarEvents" => await Tasks(cancellationToken),
                "CreateCalendarEvent" => await Draft(agentKey, executionId, "Calendar", NalvaArgs.Str(args, "title") ?? "Calendar note", "Internal note. No invitation was sent.", cancellationToken),
                "SummarizeMeetingNotes" => await Draft(agentKey, executionId, "Meeting", "Meeting summary", NalvaGuard.Clip(NalvaArgs.Str(args, "notes"), 2000), cancellationToken),
                "CreateEmailDraft" => await Draft(agentKey, executionId, "Email", NalvaArgs.Str(args, "subject") ?? "Email draft", NalvaArgs.Str(args, "body") ?? "Email draft. It was not sent.", cancellationToken),
                "CreateReminder" => await Draft(agentKey, executionId, "Reminder", NalvaArgs.Str(args, "title") ?? "Reminder", NalvaArgs.Str(args, "body") ?? "Internal reminder.", cancellationToken),
                "ConvertLead" => await Convert(args, cancellationToken),
                "IngestKnowledge" => await Ingest(agentKey, args, cancellationToken),
                "DeleteKnowledge" => await DeleteKnowledge(agentKey, args, cancellationToken),
                "GetChurnRisk" => await Churn(cancellationToken),
                "FindDuplicateLeads" => await Duplicates(cancellationToken),
                "GetOperationalAlerts" => await Alerts(cancellationToken),
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

    private async Task<NalvaToolOutcome> FinancialSummary(CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-30);
        return Ok("Recorded finance totals. This is not a statutory statement.", new JsonObject
        {
            ["invoiceOutstanding"] = await _db.Invoices.SumAsync(x => (decimal?)x.OutstandingAmount, ct) ?? 0m,
            ["paymentsLast30Days"] = await _db.Payments.Where(x => x.PaymentDate >= since).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m,
            ["expensesLast30Days"] = await _db.Expenses.Where(x => x.ExpenseDate >= since).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m,
            ["isAccountingRecord"] = false
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
        var products = await _db.Products.AsNoTracking().Where(x => x.IsActive).Select(x => new { x.CurrentStock, x.ReorderLevel }).ToListAsync(ct);
        return Ok("Inventory summary from product stock.", new JsonObject
        {
            ["products"] = products.Count,
            ["units"] = products.Sum(x => x.CurrentStock),
            ["lowStock"] = products.Count(x => x.CurrentStock <= x.ReorderLevel)
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

    private async Task<NalvaToolOutcome> ResearchMarket(CancellationToken ct)
    {
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
        var total = await _db.Leads.CountAsync(ct);
        var converted = await _db.Leads.CountAsync(x => x.Status == LeadStatuses.Converted, ct);
        return Ok("Conversion from stored leads.", new JsonObject
        {
            ["leads"] = total,
            ["converted"] = converted,
            ["rate"] = total == 0 ? 0 : decimal.Round((decimal)converted / total, 4)
        });
    }

    private async Task<NalvaToolOutcome> KnowledgeSearch(string agentKey, string? query, CancellationToken ct)
    {
        var rows = await _db.NalvaKnowledgeSources.AsNoTracking()
            .Where(x => x.AgentKey == null || x.AgentKey == agentKey)
            .ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(query))
        {
            rows = rows.Where(x =>
                x.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || x.Content.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
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
                ["excerpt"] = NalvaGuard.Clip(row.Content, 240)
            });
        }
        var data = new JsonObject { ["sources"] = list, ["authorizedOnly"] = true };
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
        if (id is null)
            return Ok("No order id was supplied.", new JsonObject { ["found"] = false });
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (order is null || order.TenantId != CompanyId)
            return Ok("That order was not found in this company.", new JsonObject { ["found"] = false });
        return Ok("Stored order.", new JsonObject
        {
            ["found"] = true,
            ["id"] = order.Id.ToString(),
            ["number"] = order.OrderNumber,
            ["status"] = order.Status,
            ["total"] = order.GrandTotal,
            ["paymentStatus"] = order.PaymentStatus
        });
    }

    private async Task<NalvaToolOutcome> CustomerHistory(JsonElement args, CancellationToken ct)
    {
        var id = NalvaArgs.GuidOf(args, "customerId");
        if (id is null)
            return Ok("No customer id was supplied.", new JsonObject { ["found"] = false });
        var orders = await _db.Orders.AsNoTracking().Where(x => x.CustomerId == id).CountAsync(ct);
        var followUps = await _db.FollowUps.AsNoTracking().Where(x => x.CustomerId == id).CountAsync(ct);
        return Ok("Stored customer history.", new JsonObject { ["customerId"] = id.ToString(), ["orders"] = orders, ["followUps"] = followUps });
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

    private async Task<NalvaToolOutcome> SupplierPrices(JsonElement args, CancellationToken ct)
    {
        var productId = NalvaArgs.GuidOf(args, "productId");
        var items = _db.PurchaseItems.AsNoTracking().Where(x => !x.IsDeleted && x.TenantId == CompanyId);
        if (productId is not null)
            items = items.Where(x => x.ProductId == productId);
        var rows = await items.OrderByDescending(x => x.UnitPrice).Take(10).Select(x => new { x.ProductId, x.UnitPrice, x.Quantity }).ToListAsync(ct);
        return List("prices", rows.Select(x => new JsonObject
        {
            ["productId"] = x.ProductId.ToString(),
            ["unitPrice"] = x.UnitPrice,
            ["quantity"] = x.Quantity
        }));
    }

    private async Task<NalvaToolOutcome> Purchases(bool pendingOnly, CancellationToken ct)
    {
        var query = _db.Purchases.AsNoTracking();
        if (pendingOnly)
            query = query.Where(x => x.Status == "Draft" || x.Status == "Pending");
        var rows = await query.OrderByDescending(x => x.PurchaseDate).Take(20).Select(x => new { x.Id, x.Status, x.TotalAmount }).ToListAsync(ct);
        return List("purchases", rows.Select(x => new JsonObject
        {
            ["id"] = x.Id.ToString(),
            ["status"] = x.Status,
            ["total"] = x.TotalAmount
        }));
    }

    private async Task<NalvaToolOutcome> Demand(CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-90);
        var orderIds = await _db.Orders.AsNoTracking().Where(x => x.OrderDate >= since).Select(x => x.Id).ToListAsync(ct);
        if (orderIds.Count < 3)
            return Ok("Not enough order history to estimate demand.", new JsonObject { ["estimate"] = false, ["orders"] = orderIds.Count });
        var qty = await _db.OrderItems.AsNoTracking().Where(x => orderIds.Contains(x.OrderId) && !x.IsDeleted).SumAsync(x => (decimal?)x.Quantity, ct) ?? 0m;
        return Ok("Labeled demand note from the last 90 days of order lines. No stock was moved.", new JsonObject { ["estimate"] = true, ["units"] = qty, ["stockMoved"] = false });
    }

    private async Task<NalvaToolOutcome> PurchaseDraft(JsonElement args, string status, CancellationToken ct)
    {
        var supplierId = NalvaArgs.GuidOf(args, "supplierId");
        var productId = NalvaArgs.GuidOf(args, "productId");
        var quantity = NalvaArgs.Dec(args, "quantity") ?? 0m;
        if (supplierId is null || productId is null || quantity <= 0)
            return Ok("Supplier, product, and quantity are required, so no purchase was created.", new JsonObject { ["created"] = false });
        var supplier = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == supplierId, ct);
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == productId, ct);
        if (supplier is null || product is null || supplier.TenantId != CompanyId || product.TenantId != CompanyId)
            return Fail("The supplier or product was not found in this company.");
        var purchase = new Purchase
        {
            SupplierId = supplier.Id,
            PurchaseDate = DateTime.UtcNow,
            TotalAmount = quantity * product.CostPrice,
            Status = status,
            ReferenceNumber = status == "Draft" ? "NALVA-DRAFT" : "NALVA-PENDING",
            Notes = status == "Draft"
                ? "Draft prepared by Nalva. Not a commitment and not sent to the supplier."
                : "Pending purchase created after approval. Not transmitted to the supplier."
        };
        _db.Purchases.Add(purchase);
        await _db.SaveChangesAsync(ct);
        _db.PurchaseItems.Add(new PurchaseItem
        {
            PurchaseId = purchase.Id,
            ProductId = product.Id,
            Quantity = quantity,
            UnitPrice = product.CostPrice,
            Total = quantity * product.CostPrice,
            TenantId = CompanyId
        });
        await _db.SaveChangesAsync(ct);
        return Ok(status == "Draft" ? "Purchase draft created." : "Pending purchase created after approval.", new JsonObject
        {
            ["created"] = true,
            ["purchaseId"] = purchase.Id.ToString(),
            ["status"] = status,
            ["sentToSupplier"] = false
        });
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
        var cutoff = DateTime.UtcNow.AddDays(-90);
        var recent = await _db.Orders.AsNoTracking().Where(x => x.OrderDate >= cutoff).Select(x => x.CustomerId).Distinct().ToListAsync(ct);
        var older = await _db.Orders.AsNoTracking().Where(x => x.OrderDate < cutoff).Select(x => x.CustomerId).Distinct().ToListAsync(ct);
        var ids = older.Except(recent).Take(20).ToList();
        return Ok("Customers with an older order and none in the last 90 days.", new JsonObject
        {
            ["count"] = older.Except(recent).Count(),
            ["customerIds"] = new JsonArray(ids.Select(x => (JsonNode)x.ToString()).ToArray()),
            ["prediction"] = false
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
        var campaigns = await _db.NalvaWorkItems.CountAsync(x => x.Kind == "Campaign", ct);
        var leads = await _db.Leads.CountAsync(x => x.Source == "Nalva", ct);
        return Ok("Draft campaigns and Nalva-sourced leads. Not an advertising-platform report.", new JsonObject { ["campaignDrafts"] = campaigns, ["nalvaLeads"] = leads, ["externalAnalytics"] = false });
    }

    private async Task<NalvaToolOutcome> Report(string agentKey, Guid executionId, string tool, CancellationToken ct)
    {
        var summary = tool switch
        {
            "GenerateFinancialReport" => (await FinancialSummary(ct)).Summary,
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
        var draft = await Draft(agentKey, executionId, "SupportTicket", NalvaArgs.Str(args, "title") ?? "Support ticket", NalvaArgs.Str(args, "body") ?? "Opened from the workforce.", ct);
        if (_events is not null && draft.Success)
            await _events.EnqueueAsync(NalvaEventNames.SupportTicketCreated, draft.Data.ToJsonString(), "SupportTicketCreated:" + executionId.ToString("N"), ct);
        return draft;
    }

    private async Task<NalvaToolOutcome?> TryLiveIntegrationAsync(string tool, string argsJson, CancellationToken ct)
    {
        if (_integrations is null)
            return null;
        var provider = tool switch
        {
            "SendExternalEmail" => "gmail",
            "IssueRefund" => "stripe",
            _ => ""
        };
        var operation = tool switch
        {
            "SendExternalEmail" => "send",
            "IssueRefund" => "refund",
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
