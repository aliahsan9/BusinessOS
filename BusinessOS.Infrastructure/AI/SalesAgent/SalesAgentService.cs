using System.Text.Json;
using System.Text.Json.Nodes;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Application.Features.SalesAgent;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.AI.Nalva;
using BusinessOS.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI.SalesAgent;

public sealed class SalesAgentService : ISalesAgentService
{
    private readonly BusinessOSDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly ILeadResearchRouter _research;
    private readonly INalvaWorkforceService _workforce;
    private readonly ISalesAgentToolOperations _tools;
    private readonly IDataProtector _protector;
    private readonly IOutboundEmailSink? _emailSink;

    public SalesAgentService(
        BusinessOSDbContext db,
        ICurrentUserService user,
        ILeadResearchRouter research,
        INalvaWorkforceService workforce,
        ISalesAgentToolOperations tools,
        IDataProtectionProvider dataProtection,
        IOutboundEmailSink? emailSink = null)
    {
        _db = db;
        _user = user;
        _research = research;
        _workforce = workforce;
        _tools = tools;
        _emailSink = emailSink;
        _protector = dataProtection.CreateProtector("BusinessOS.SalesAgent.ResearchApiKey.v1");
    }

    public async Task<SalesAgentCommandCenterDto> GetCommandCenterAsync(CancellationToken cancellationToken = default)
    {
        var settings = await EnsureSettingsAsync(cancellationToken);
        var provider = await _research.GetActiveStatusAsync(cancellationToken);
        var agent = await SafeAgentAsync(cancellationToken);
        var discovered = await _db.SalesProspectResearch.CountAsync(cancellationToken);
        var qualified = await _db.SalesProspectResearch.CountAsync(x => x.IsQualified, cancellationToken);
        var pending = await _db.SalesOutreachMessages.CountAsync(x => x.Status == SalesAgentStatuses.PendingApproval || x.Status == SalesAgentStatuses.Draft, cancellationToken);
        var sent = await _db.SalesOutboundEmailLogs.CountAsync(x => x.Performed, cancellationToken);
        var responses = await _db.SalesOutreachMessages.CountAsync(x => x.Status == SalesAgentStatuses.Replied, cancellationToken);
        var meetings = await _db.FollowUps.CountAsync(x => x.Notes != null && x.Notes.Contains("Sales Agent") && x.Status == "Open", cancellationToken);
        var failed = await _db.NalvaTasks.CountAsync(x => x.AgentKey == "sales" && x.Status == NalvaStatuses.Failed, cancellationToken);
        var cost = await _db.NalvaExecutions.Where(x => x.AgentKey == "sales").SumAsync(x => (decimal?)x.EstimatedCostUsd, cancellationToken) ?? 0m;
        var currentExecution = await _db.NalvaExecutions.AsNoTracking()
            .Where(x => x.AgentKey == "sales")
            .OrderByDescending(x => x.StartedAt)
            .Select(x => x.Summary)
            .FirstOrDefaultAsync(cancellationToken);
        var gmailConnected = await _db.CompanyIntegrationConnections.AsNoTracking()
            .AnyAsync(x => x.ProviderKey == "gmail" && x.Status == "Connected", cancellationToken);
        var emailMessage = gmailConnected
            ? "Gmail connection is present."
            : _emailSink is { IsTestSink: true }
                ? "No Gmail connection. Test email sink is available for automated verification only."
                : "No mailbox is connected. Connect Gmail under Integrations for live sends.";

        var objectives = await _db.SalesAgentObjectives.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt).Take(20)
            .Select(x => new SalesAgentObjectiveDto(x.Id, x.ObjectiveText, x.Status, x.TargetProspectCount, x.DiscoveredCount, x.QualifiedCount, x.OutreachDraftCount, x.OutreachSentCount, x.ResponseCount, x.DelegatedByAgentKey, x.CreatedAt, x.CompletedAt, x.ResultSummary))
            .ToListAsync(cancellationToken);

        var prospects = await _db.SalesProspectResearch.AsNoTracking()
            .OrderByDescending(x => x.ResearchedAt).Take(25)
            .Select(x => new SalesProspectDto(x.Id, x.LeadId, x.CompanyName, x.Website, x.Industry, x.Country, x.CompanySize, x.PublicContactEmail, x.ProviderKey, x.SourceAttribution, x.ResearchedAt, x.LeadScore, x.ScoreExplanation, x.ScoreIsEstimate, x.IsQualified, x.IsDuplicate, x.QualityStatus))
            .ToListAsync(cancellationToken);

        var approvals = await _db.SalesOutreachMessages.AsNoTracking()
            .Where(x => x.Status == SalesAgentStatuses.Draft || x.Status == SalesAgentStatuses.PendingApproval)
            .OrderByDescending(x => x.CreatedAt).Take(25)
            .Select(x => new SalesOutreachMessageDto(x.Id, x.CampaignId, x.LeadId, x.ToEmail, x.Subject, x.Body, x.FitExplanation, x.Status, x.SequenceStep, x.CreatedAt, x.SentAt, x.UsedTestSink))
            .ToListAsync(cancellationToken);

        var history = await _db.NalvaExecutions.AsNoTracking()
            .Where(x => x.AgentKey == "sales")
            .OrderByDescending(x => x.StartedAt).Take(30)
            .Select(x => new SalesAgentExecutionDto(x.Id, x.Status, x.Objective, x.Summary, x.StartedAt, x.CompletedAt, x.EstimatedCostUsd, x.Error))
            .ToListAsync(cancellationToken);

        return new SalesAgentCommandCenterDto(
            settings.IsPaused ? SalesAgentStatuses.Paused : (agent?.Status ?? NalvaStatuses.Inactive),
            settings.IsPaused,
            settings.CurrentObjective,
            settings.ActiveObjectiveId,
            currentExecution,
            discovered,
            qualified,
            pending,
            sent,
            responses,
            meetings,
            failed,
            cost,
            true,
            provider,
            gmailConnected,
            emailMessage,
            objectives,
            prospects,
            approvals,
            history,
            MapSettings(settings));
    }

    public async Task<SalesAgentSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        MapSettings(await EnsureSettingsAsync(cancellationToken));

    public async Task<SalesAgentSettingsDto> SaveSettingsAsync(SalesAgentSettingsWriteRequest request, CancellationToken cancellationToken = default)
    {
        var row = await EnsureSettingsAsync(cancellationToken);
        if (request.DailyResearchLimit is int daily)
            row.DailyResearchLimit = Math.Clamp(daily, 1, 200);
        if (request.MaxOutreachPerDay is int max)
            row.MaxOutreachPerDay = Math.Clamp(max, 1, 500);
        if (request.AllowFixtureResearch is bool allow)
            row.AllowFixtureResearch = allow;
        if (request.IdealCustomerProfileJson is not null)
            row.IdealCustomerProfileJson = request.IdealCustomerProfileJson;
        if (request.QualificationCriteriaJson is not null)
            row.QualificationCriteriaJson = request.QualificationCriteriaJson;
        if (request.QualificationThreshold is int threshold)
            row.QualificationThreshold = Math.Clamp(threshold, 1, 100);
        if (request.ResearchProviderKey is not null)
            row.ResearchProviderKey = string.IsNullOrWhiteSpace(request.ResearchProviderKey) ? null : request.ResearchProviderKey.Trim();
        if (request.ResearchBaseUrl is not null)
            row.ResearchBaseUrl = string.IsNullOrWhiteSpace(request.ResearchBaseUrl) ? null : request.ResearchBaseUrl.Trim();
        if (request.RequireOutreachApproval is bool require)
            row.RequireOutreachApproval = require;
        if (request.ClearResearchApiKey)
            row.ResearchApiKeyProtected = null;
        else if (!string.IsNullOrWhiteSpace(request.ResearchApiKey))
            row.ResearchApiKeyProtected = _protector.Protect(request.ResearchApiKey.Trim());
        await _db.SaveChangesAsync(cancellationToken);
        return MapSettings(row);
    }

    public async Task<SalesAgentObjectiveDto> AssignObjectiveAsync(AssignSalesObjectiveRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Objective))
            throw new Application.Common.Exceptions.BadRequestException("Objective text is required.");

        await _workforce.ActivateAsync("sales", cancellationToken);
        var args = JsonSerializer.Serialize(new
        {
            objective = request.Objective,
            targetProspectCount = request.TargetProspectCount,
            idealCustomerProfileJson = request.IdealCustomerProfileJson,
            delegatedByAgentKey = request.DelegatedByAgentKey
        });
        var result = await _tools.AssignSalesObjectiveAsync(
            request.DelegatedByAgentKey ?? "ceo",
            request.ParentExecutionId ?? Guid.Empty,
            NalvaArgs.Parse(args),
            cancellationToken);
        if (result.Data["assigned"]?.GetValue<bool>() != true)
            throw new Application.Common.Exceptions.BadRequestException(result.Summary);

        var objectiveId = Guid.Parse(result.Data["objectiveId"]!.GetValue<string>()!);
        if (request.QueueExecution)
        {
            var runArgs = JsonSerializer.Serialize(new
            {
                objectiveId,
                limit = request.TargetProspectCount ?? 10,
                idealCustomerProfileJson = request.IdealCustomerProfileJson
            });
            await _workforce.EnqueueAsync("sales", new NalvaRunRequest
            {
                Objective = request.Objective,
                ArgumentsJson = runArgs,
                Tools =
                [
                    "ResearchExternalProspects",
                    "QualifyLead",
                    "CreateOutreachCampaign",
                    "GeneratePersonalizedOutreach",
                    "CreateFollowUp",
                    "GenerateSalesCampaignReport"
                ],
                IdempotencyKey = "sales-objective:" + objectiveId.ToString("N"),
                ParentTaskId = request.ParentTaskId,
                Priority = 2
            }, cancellationToken);
        }

        var row = await _db.SalesAgentObjectives.AsNoTracking().FirstAsync(x => x.Id == objectiveId, cancellationToken);
        return new SalesAgentObjectiveDto(row.Id, row.ObjectiveText, row.Status, row.TargetProspectCount, row.DiscoveredCount, row.QualifiedCount, row.OutreachDraftCount, row.OutreachSentCount, row.ResponseCount, row.DelegatedByAgentKey, row.CreatedAt, row.CompletedAt, row.ResultSummary);
    }

    public async Task<SalesAgentSettingsDto> PauseAsync(CancellationToken cancellationToken = default)
    {
        var row = await EnsureSettingsAsync(cancellationToken);
        row.IsPaused = true;
        await _db.SaveChangesAsync(cancellationToken);
        var tasks = await _db.NalvaTasks.Where(x => x.AgentKey == "sales" && (x.Status == NalvaStatuses.Pending || x.Status == "Paused" || x.Status == NalvaStatuses.Running)).ToListAsync(cancellationToken);
        foreach (var task in tasks.Where(x => x.Status == NalvaStatuses.Pending || x.Status == NalvaStatuses.Running))
            await _workforce.PauseTaskAsync(task.Id, cancellationToken);
        return MapSettings(row);
    }

    public async Task<SalesAgentSettingsDto> ResumeAsync(CancellationToken cancellationToken = default)
    {
        var row = await EnsureSettingsAsync(cancellationToken);
        row.IsPaused = false;
        await _db.SaveChangesAsync(cancellationToken);
        var paused = await _db.NalvaTasks.Where(x => x.AgentKey == "sales" && x.Status == "Paused").Select(x => x.Id).ToListAsync(cancellationToken);
        foreach (var id in paused)
            await _workforce.ResumeTaskAsync(id, cancellationToken);
        return MapSettings(row);
    }

    public async Task<IReadOnlyList<SalesProspectDto>> ImportAuthorizedProspectsAsync(ImportAuthorizedProspectsRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await EnsureSettingsAsync(cancellationToken);
        var created = new List<SalesProspectDto>();
        foreach (var item in request.Prospects)
        {
            if (string.IsNullOrWhiteSpace(item.CompanyName))
                continue;
            var research = new SalesProspectResearch
            {
                ObjectiveId = request.ObjectiveId ?? settings.ActiveObjectiveId,
                CompanyName = item.CompanyName.Trim(),
                Website = item.Website,
                Industry = item.Industry,
                Country = item.Country,
                CompanySize = item.CompanySize,
                BusinessDescription = item.BusinessDescription,
                PublicContactEmail = string.IsNullOrWhiteSpace(item.PublicContactEmail) ? null : item.PublicContactEmail.Trim().ToLowerInvariant(),
                PublicContactPhone = item.PublicContactPhone,
                DecisionMakerName = item.DecisionMakerName,
                DecisionMakerTitle = item.DecisionMakerTitle,
                SourceUrl = item.SourceUrl ?? item.Website,
                ProviderKey = "owner-import",
                SourceAttribution = "Owner-authorized public business import.",
                ResearchedAt = DateTime.UtcNow,
                DedupKey = Guid.NewGuid().ToString("N"),
                QualityStatus = "Valid",
                RawPayloadJson = JsonSerializer.Serialize(item)
            };
            _db.SalesProspectResearch.Add(research);
            await _db.SaveChangesAsync(cancellationToken);
            created.Add(new SalesProspectDto(research.Id, research.LeadId, research.CompanyName, research.Website, research.Industry, research.Country, research.CompanySize, research.PublicContactEmail, research.ProviderKey, research.SourceAttribution, research.ResearchedAt, research.LeadScore, research.ScoreExplanation, research.ScoreIsEstimate, research.IsQualified, research.IsDuplicate, research.QualityStatus));
        }
        return created;
    }

    public async Task<SalesCampaignReportDto> GetCampaignReportAsync(Guid? objectiveId, Guid? campaignId, CancellationToken cancellationToken = default)
    {
        var researched = await _db.SalesProspectResearch.CountAsync(x => objectiveId == null || x.ObjectiveId == objectiveId, cancellationToken);
        var qualified = await _db.SalesProspectResearch.CountAsync(x => x.IsQualified && (objectiveId == null || x.ObjectiveId == objectiveId), cancellationToken);
        var drafts = await _db.SalesOutreachMessages.CountAsync(x => x.Status == SalesAgentStatuses.Draft || x.Status == SalesAgentStatuses.PendingApproval, cancellationToken);
        var sent = await _db.SalesOutboundEmailLogs.CountAsync(x => x.Performed, cancellationToken);
        var responses = await _db.SalesOutreachMessages.CountAsync(x => x.Status == SalesAgentStatuses.Replied, cancellationToken);
        var followUps = await _db.FollowUps.CountAsync(x => x.Notes != null && x.Notes.Contains("Sales Agent"), cancellationToken);
        var failed = await _db.SalesOutboundEmailLogs.CountAsync(x => !x.Performed, cancellationToken);
        var cost = await _db.NalvaExecutions.Where(x => x.AgentKey == "sales").SumAsync(x => (decimal?)x.EstimatedCostUsd, cancellationToken) ?? 0m;
        var summary = $"Persisted campaign report: {researched} researched, {qualified} qualified, {sent} sent, {responses} responses.";
        return new SalesCampaignReportDto(campaignId, objectiveId, "Sales campaign report", researched, qualified, drafts, sent, responses, followUps, failed, cost, true, summary, new JsonObject
        {
            ["prospectsResearched"] = researched,
            ["qualifiedLeads"] = qualified,
            ["emailsSent"] = sent,
            ["responses"] = responses
        });
    }

    public async Task<SalesOutreachMessageDto> SimulateProspectReplyAsync(SimulateProspectReplyRequest request, CancellationToken cancellationToken = default)
    {
        var args = JsonSerializer.Serialize(new
        {
            outreachMessageId = request.OutreachMessageId,
            replySummary = request.ReplySummary,
            scheduleFollowUp = request.ScheduleFollowUp,
            followUpDays = request.FollowUpDays
        });
        var outcome = await _tools.ProcessProspectReplyAsync(NalvaArgs.Parse(args), cancellationToken);
        if (outcome.Data["processed"]?.GetValue<bool>() != true)
            throw new Application.Common.Exceptions.BadRequestException(outcome.Summary);
        var message = await _db.SalesOutreachMessages.AsNoTracking().FirstAsync(x => x.Id == request.OutreachMessageId, cancellationToken);
        return new SalesOutreachMessageDto(message.Id, message.CampaignId, message.LeadId, message.ToEmail, message.Subject, message.Body, message.FitExplanation, message.Status, message.SequenceStep, message.CreatedAt, message.SentAt, message.UsedTestSink);
    }

    public Task<IReadOnlyList<LeadResearchProviderStatus>> GetResearchProvidersAsync(CancellationToken cancellationToken = default) =>
        _research.ListProvidersAsync(cancellationToken);

    private async Task<NalvaAgentDetailDto?> SafeAgentAsync(CancellationToken ct)
    {
        try
        {
            return await _workforce.GetAgentAsync("sales", ct);
        }
        catch
        {
            return null;
        }
    }

    private async Task<SalesAgentSettings> EnsureSettingsAsync(CancellationToken ct)
    {
        var row = await _db.SalesAgentSettings.FirstOrDefaultAsync(ct);
        if (row is not null)
            return row;
        row = new SalesAgentSettings
        {
            AllowFixtureResearch = false,
            QualificationCriteriaJson =
                """[{"key":"industry","label":"Industry","expectedValue":null,"weight":20},{"key":"country","label":"Geographic location","expectedValue":null,"weight":15},{"key":"companySize","label":"Company size","expectedValue":null,"weight":10},{"key":"services","label":"Required services","expectedValue":".NET,Angular","weight":25},{"key":"contact","label":"Public business contact","expectedValue":null,"weight":20},{"key":"website","label":"Company website","expectedValue":null,"weight":10}]""",
            IdealCustomerProfileJson = """{"industry":"Software","requiredServices":".NET,Angular","keywords":"software development"}"""
        };
        _db.SalesAgentSettings.Add(row);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    private static SalesAgentSettingsDto MapSettings(SalesAgentSettings row) =>
        new(row.IsPaused, row.DailyResearchLimit, row.MaxOutreachPerDay, row.AllowFixtureResearch, row.IdealCustomerProfileJson, row.QualificationCriteriaJson, row.QualificationThreshold, row.ResearchProviderKey, !string.IsNullOrWhiteSpace(row.ResearchApiKeyProtected), row.ResearchBaseUrl, row.RequireOutreachApproval, row.CurrentObjective, row.ActiveObjectiveId);
}
