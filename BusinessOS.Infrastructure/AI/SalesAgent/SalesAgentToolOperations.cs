using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Integrations;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Application.Features.SalesAgent;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.AI.Nalva;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI.SalesAgent;

public interface ISalesAgentToolOperations
{
    Task<NalvaToolOutcome> ResearchExternalProspectsAsync(string agentKey, Guid executionId, JsonElement args, CancellationToken ct);
    Task<NalvaToolOutcome> QualifyProspectAsync(JsonElement args, CancellationToken ct);
    Task<NalvaToolOutcome> CreateOutreachCampaignAsync(string agentKey, Guid executionId, JsonElement args, CancellationToken ct);
    Task<NalvaToolOutcome> GeneratePersonalizedOutreachAsync(string agentKey, Guid executionId, JsonElement args, CancellationToken ct);
    Task<NalvaToolOutcome> GetSalesAgentStatusAsync(CancellationToken ct);
    Task<NalvaToolOutcome> AssignSalesObjectiveAsync(string agentKey, Guid executionId, JsonElement args, CancellationToken ct);
    Task<NalvaToolOutcome> ProcessProspectReplyAsync(JsonElement args, CancellationToken ct);
    Task<NalvaToolOutcome> GenerateSalesCampaignReportAsync(string agentKey, Guid executionId, JsonElement args, CancellationToken ct);
    Task<NalvaToolOutcome?> TrySendOutreachEmailAsync(JsonElement args, string argsJson, bool approvalGranted, CancellationToken ct);
    Task SanitizeExternalContentAsync(string? content, CancellationToken ct);
}

public sealed class SalesAgentToolOperations : ISalesAgentToolOperations
{
    private readonly BusinessOSDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly ILeadResearchRouter _research;
    private readonly ILeadQualificationService _qualification;
    private readonly IOutboundEmailSink? _emailSink;
    private readonly IIntegrationPlatform? _integrations;
    private readonly ICurrentUserService _user;
    private readonly INalvaEventOutbox? _events;

    public SalesAgentToolOperations(
        BusinessOSDbContext db,
        ITenantProvider tenant,
        ILeadResearchRouter research,
        ILeadQualificationService qualification,
        ICurrentUserService user,
        IOutboundEmailSink? emailSink = null,
        IIntegrationPlatform? integrations = null,
        INalvaEventOutbox? events = null)
    {
        _db = db;
        _tenant = tenant;
        _research = research;
        _qualification = qualification;
        _user = user;
        _emailSink = emailSink;
        _integrations = integrations;
        _events = events;
    }

    private Guid CompanyId => _tenant.TenantId;

    public Task SanitizeExternalContentAsync(string? content, CancellationToken ct)
    {
        // External website/document text is never accepted as tool policy overrides.
        // Callers must pass research fields through structured DTOs, not free-form system prompts.
        _ = content;
        _ = ct;
        return Task.CompletedTask;
    }

    public async Task<NalvaToolOutcome> GetSalesAgentStatusAsync(CancellationToken ct)
    {
        var settings = await EnsureSettingsAsync(ct);
        var provider = await _research.GetActiveStatusAsync(ct);
        var discovered = await _db.SalesProspectResearch.CountAsync(ct);
        var qualified = await _db.SalesProspectResearch.CountAsync(x => x.IsQualified, ct);
        var pending = await _db.SalesOutreachMessages.CountAsync(x => x.Status == SalesAgentStatuses.PendingApproval, ct);
        var sent = await _db.SalesOutreachMessages.CountAsync(x => x.Status == SalesAgentStatuses.Sent || x.Status == SalesAgentStatuses.Replied, ct);
        var gmail = await EmailStatusAsync(ct);
        return Ok("Sales Agent status from persisted records.", new JsonObject
        {
            ["paused"] = settings.IsPaused,
            ["currentObjective"] = settings.CurrentObjective,
            ["activeObjectiveId"] = settings.ActiveObjectiveId?.ToString(),
            ["leadsDiscovered"] = discovered,
            ["qualifiedLeads"] = qualified,
            ["pendingOutreachApprovals"] = pending,
            ["emailsSent"] = sent,
            ["researchProvider"] = provider.ProviderKey,
            ["researchAvailable"] = provider.IsAvailable,
            ["researchMessage"] = provider.Message,
            ["emailIntegrationConnected"] = gmail.Connected,
            ["emailIntegrationMessage"] = gmail.Message
        });
    }

    public async Task<NalvaToolOutcome> AssignSalesObjectiveAsync(string agentKey, Guid executionId, JsonElement args, CancellationToken ct)
    {
        var text = NalvaArgs.Str(args, "objective") ?? NalvaArgs.Str(args, "objectiveText");
        if (string.IsNullOrWhiteSpace(text))
            return Ok("No objective text was supplied.", new JsonObject { ["assigned"] = false });

        var settings = await EnsureSettingsAsync(ct);
        if (settings.IsPaused)
            return Ok("Sales Agent is paused. Resume it before assigning objectives.", new JsonObject { ["assigned"] = false, ["paused"] = true });

        var target = NalvaArgs.Int(args, "targetCount") ?? NalvaArgs.Int(args, "targetProspectCount") ?? ExtractTarget(text) ?? 10;
        var icp = NalvaArgs.Str(args, "idealCustomerProfileJson") ?? BuildIcpFromObjective(text, settings.IdealCustomerProfileJson);
        var objective = new SalesAgentObjective
        {
            ObjectiveText = NalvaGuard.Clip(text, 2000),
            Status = SalesAgentStatuses.Active,
            TargetProspectCount = Math.Clamp(target, 1, 500),
            IdealCustomerProfileJson = icp,
            AssignedByUserId = _user.UserId,
            DelegatedByAgentKey = NalvaArgs.Str(args, "delegatedByAgentKey") ?? (string.Equals(agentKey, "sales", StringComparison.OrdinalIgnoreCase) ? null : agentKey),
            ParentExecutionId = executionId
        };
        _db.SalesAgentObjectives.Add(objective);
        settings.CurrentObjective = objective.ObjectiveText;
        settings.ActiveObjectiveId = objective.Id;
        settings.IdealCustomerProfileJson = icp;
        await _db.SaveChangesAsync(ct);
        return Ok("Sales objective assigned.", new JsonObject
        {
            ["assigned"] = true,
            ["objectiveId"] = objective.Id.ToString(),
            ["targetProspectCount"] = objective.TargetProspectCount,
            ["delegatedByAgentKey"] = objective.DelegatedByAgentKey
        });
    }

    public async Task<NalvaToolOutcome> ResearchExternalProspectsAsync(string agentKey, Guid executionId, JsonElement args, CancellationToken ct)
    {
        var settings = await EnsureSettingsAsync(ct);
        if (settings.IsPaused)
            return Ok("Sales Agent is paused.", new JsonObject { ["researched"] = 0, ["paused"] = true });

        var providerStatus = await _research.GetActiveStatusAsync(ct);
        if (!providerStatus.IsAvailable)
        {
            return Ok(providerStatus.Message, new JsonObject
            {
                ["researched"] = 0,
                ["providerAvailable"] = false,
                ["providerKey"] = providerStatus.ProviderKey,
                ["message"] = providerStatus.Message,
                ["connectHint"] = "Enable fixture research, configure an HTTP research API, or import authorized public business records."
            });
        }

        var today = DateTime.UtcNow.Date;
        var researchedToday = await _db.SalesProspectResearch.CountAsync(x => x.ResearchedAt >= today, ct);
        var remaining = Math.Max(0, settings.DailyResearchLimit - researchedToday);
        if (remaining == 0)
            return Ok("Daily research limit reached.", new JsonObject { ["researched"] = 0, ["dailyLimitReached"] = true, ["dailyResearchLimit"] = settings.DailyResearchLimit });

        var limit = Math.Min(remaining, Math.Clamp(NalvaArgs.Int(args, "limit") ?? 10, 1, settings.DailyResearchLimit));
        var icp = ParseIcp(NalvaArgs.Str(args, "idealCustomerProfileJson") ?? settings.IdealCustomerProfileJson);
        var query = new LeadResearchQuery(
            NalvaArgs.Str(args, "industry") ?? icp.Industry,
            NalvaArgs.Str(args, "country") ?? icp.Country,
            NalvaArgs.Str(args, "requiredServices") ?? icp.RequiredServices,
            NalvaArgs.Str(args, "companySize") ?? icp.CompanySize,
            NalvaArgs.Str(args, "keywords") ?? icp.Keywords,
            limit);

        var found = await _research.SearchAsync(query, ct);
        var objectiveId = NalvaArgs.GuidOf(args, "objectiveId") ?? settings.ActiveObjectiveId;
        var created = 0;
        var duplicates = 0;
        var leadIds = new JsonArray();
        var researchIds = new JsonArray();

        foreach (var item in found)
        {
            await SanitizeExternalContentAsync(item.BusinessDescription, ct);
            var quality = ValidateQuality(item);
            var dedup = BuildDedupKey(item.CompanyName, item.Website, item.PublicContactEmail);
            var existing = await _db.SalesProspectResearch.AsNoTracking().FirstOrDefaultAsync(x => x.DedupKey == dedup, ct);
            if (existing is not null)
            {
                duplicates++;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(item.PublicContactEmail))
            {
                var email = item.PublicContactEmail.Trim().ToLowerInvariant();
                var leadDup = await _db.Leads.AsNoTracking().AnyAsync(x => x.Email != null && x.Email.ToLower() == email, ct);
                if (leadDup)
                {
                    duplicates++;
                    _db.SalesProspectResearch.Add(new SalesProspectResearch
                    {
                        ObjectiveId = objectiveId,
                        CompanyName = item.CompanyName,
                        Website = item.Website,
                        Industry = item.Industry,
                        Country = item.Country,
                        CompanySize = item.CompanySize,
                        BusinessDescription = NalvaGuard.Clip(item.BusinessDescription, 2000),
                        PublicContactEmail = email,
                        PublicContactPhone = item.PublicContactPhone,
                        DecisionMakerName = item.DecisionMakerName,
                        DecisionMakerTitle = item.DecisionMakerTitle,
                        SourceUrl = item.SourceUrl,
                        ProviderKey = item.ProviderKey,
                        SourceAttribution = item.SourceAttribution,
                        ResearchedAt = item.ResearchedAt,
                        IsDuplicate = true,
                        DedupKey = dedup,
                        QualityStatus = quality.Status,
                        QualityNotes = quality.Notes,
                        RawPayloadJson = item.RawPayloadJson
                    });
                    continue;
                }
            }

            Guid? leadId = null;
            if (quality.Status == "Valid" && !string.IsNullOrWhiteSpace(item.CompanyName))
            {
                var lead = new Lead
                {
                    Name = item.DecisionMakerName ?? item.CompanyName,
                    CompanyName = item.CompanyName,
                    Email = string.IsNullOrWhiteSpace(item.PublicContactEmail) ? null : item.PublicContactEmail.Trim().ToLowerInvariant(),
                    Phone = item.PublicContactPhone,
                    Source = "SalesAgent:" + item.ProviderKey,
                    Status = LeadStatuses.New,
                    Notes = NalvaGuard.Clip(
                        $"Research source: {item.SourceAttribution}. URL: {item.SourceUrl}. {item.BusinessDescription}",
                        1000)
                };
                _db.Leads.Add(lead);
                await _db.SaveChangesAsync(ct);
                leadId = lead.Id;
                leadIds.Add(lead.Id.ToString());
                if (_events is not null)
                    await _events.EnqueueAsync(NalvaEventNames.LeadCreated, JsonSerializer.Serialize(new { leadId = lead.Id }), "LeadCreated:" + lead.Id.ToString("N"), ct);
            }

            var research = new SalesProspectResearch
            {
                ObjectiveId = objectiveId,
                LeadId = leadId,
                CompanyName = item.CompanyName,
                Website = item.Website,
                Industry = item.Industry,
                Country = item.Country,
                CompanySize = item.CompanySize,
                BusinessDescription = NalvaGuard.Clip(item.BusinessDescription, 2000),
                PublicContactEmail = string.IsNullOrWhiteSpace(item.PublicContactEmail) ? null : item.PublicContactEmail.Trim().ToLowerInvariant(),
                PublicContactPhone = item.PublicContactPhone,
                DecisionMakerName = item.DecisionMakerName,
                DecisionMakerTitle = item.DecisionMakerTitle,
                SourceUrl = item.SourceUrl,
                ProviderKey = item.ProviderKey,
                SourceAttribution = item.SourceAttribution,
                ResearchedAt = item.ResearchedAt,
                DedupKey = dedup,
                QualityStatus = quality.Status,
                QualityNotes = quality.Notes,
                RawPayloadJson = item.RawPayloadJson
            };
            _db.SalesProspectResearch.Add(research);
            await _db.SaveChangesAsync(ct);
            researchIds.Add(research.Id.ToString());
            created++;

            if (objectiveId is Guid oid)
            {
                var objective = await _db.SalesAgentObjectives.FirstOrDefaultAsync(x => x.Id == oid, ct);
                if (objective is not null)
                {
                    objective.DiscoveredCount += 1;
                    await _db.SaveChangesAsync(ct);
                }
            }
        }

        await _db.NalvaMemoryRecords.AddAsync(new NalvaMemoryRecord
        {
            AgentKey = agentKey,
            Kind = "SalesResearch",
            Title = "Prospect research batch",
            Content = $"Researched {created} prospects via {providerStatus.ProviderKey}. Duplicates skipped: {duplicates}.",
            ExecutionId = executionId,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        }, ct);
        await _db.SaveChangesAsync(ct);

        return Ok(
            $"Researched {created} prospects via {providerStatus.DisplayName}. Source attribution stored. Duplicates skipped: {duplicates}.",
            new JsonObject
            {
                ["researched"] = created,
                ["duplicatesSkipped"] = duplicates,
                ["providerKey"] = providerStatus.ProviderKey,
                ["isFixture"] = providerStatus.IsFixture,
                ["isLiveIntegration"] = providerStatus.IsLiveIntegration,
                ["leadIds"] = leadIds,
                ["researchIds"] = researchIds,
                ["estimate"] = false
            });
    }

    public async Task<NalvaToolOutcome> QualifyProspectAsync(JsonElement args, CancellationToken ct)
    {
        var settings = await EnsureSettingsAsync(ct);
        var criteria = ParseCriteria(settings.QualificationCriteriaJson);
        var researchId = NalvaArgs.GuidOf(args, "researchId") ?? NalvaArgs.GuidOf(args, "prospectResearchId");
        var leadId = NalvaArgs.GuidOf(args, "leadId");
        var icp = ParseIcp(settings.IdealCustomerProfileJson);

        List<SalesProspectResearch> batch = [];
        if (researchId is Guid rid)
        {
            var one = await _db.SalesProspectResearch.FirstOrDefaultAsync(x => x.Id == rid, ct);
            if (one is not null) batch.Add(one);
        }
        else if (leadId is Guid lid)
        {
            var one = await _db.SalesProspectResearch.FirstOrDefaultAsync(x => x.LeadId == lid, ct);
            if (one is not null) batch.Add(one);
            else
            {
                var leadOnly = await _db.Leads.FirstOrDefaultAsync(x => x.Id == lid, ct);
                if (leadOnly is null)
                    return Ok("No lead or research id was supplied.", new JsonObject { ["qualified"] = false });
                var scoreOnly = _qualification.Score(criteria, settings.QualificationThreshold, null, null, null, leadOnly.Notes, icp.RequiredServices, null, leadOnly.Email);
                leadOnly.Notes = NalvaGuard.Clip((leadOnly.Notes ?? "") + "\n" + scoreOnly.Explanation, 1000);
                if (scoreOnly.MeetsThreshold && !string.IsNullOrWhiteSpace(leadOnly.Email))
                {
                    leadOnly.IsQualified = true;
                    leadOnly.Status = LeadStatuses.Qualified;
                }
                await _db.SaveChangesAsync(ct);
                return Ok(scoreOnly.Explanation, new JsonObject
                {
                    ["qualified"] = scoreOnly.MeetsThreshold,
                    ["score"] = scoreOnly.Score,
                    ["scoreIsEstimate"] = true,
                    ["leadId"] = leadOnly.Id.ToString()
                });
            }
        }
        else
        {
            var objectiveId = NalvaArgs.GuidOf(args, "objectiveId") ?? settings.ActiveObjectiveId;
            batch = await _db.SalesProspectResearch
                .Where(x => !x.IsDuplicate && x.LeadScore == null && (objectiveId == null || x.ObjectiveId == objectiveId))
                .OrderByDescending(x => x.ResearchedAt)
                .Take(25)
                .ToListAsync(ct);
            // If the objective filter matched nothing (for example older research rows without ObjectiveId),
            // fall back to any unscored non-duplicate research for this company.
            if (batch.Count == 0)
            {
                batch = await _db.SalesProspectResearch
                    .Where(x => !x.IsDuplicate && x.LeadScore == null)
                    .OrderByDescending(x => x.ResearchedAt)
                    .Take(25)
                    .ToListAsync(ct);
            }
        }

        if (batch.Count == 0)
            return Ok("No prospects were available to qualify.", new JsonObject { ["qualified"] = false, ["qualifiedCount"] = 0, ["scoreIsEstimate"] = true });

        var qualifiedCount = 0;
        var scored = new JsonArray();
        foreach (var research in batch)
        {
            var score = _qualification.Score(
                criteria,
                settings.QualificationThreshold,
                research.Industry ?? NalvaArgs.Str(args, "industry"),
                research.Country ?? NalvaArgs.Str(args, "country"),
                research.CompanySize ?? NalvaArgs.Str(args, "companySize"),
                research.BusinessDescription,
                icp.RequiredServices,
                research.Website,
                research.PublicContactEmail);

            research.LeadScore = score.Score;
            research.ScoreExplanation = score.Explanation;
            research.ScoreIsEstimate = true;
            research.IsQualified = score.MeetsThreshold;
            if (score.MeetsThreshold)
                qualifiedCount++;

            if (research.LeadId is Guid updateLeadId)
            {
                var lead = await _db.Leads.FirstOrDefaultAsync(x => x.Id == updateLeadId, ct);
                if (lead is not null)
                {
                    lead.Notes = NalvaGuard.Clip((lead.Notes ?? "") + "\n" + score.Explanation, 1000);
                    if (score.MeetsThreshold && !string.IsNullOrWhiteSpace(lead.Email))
                    {
                        lead.IsQualified = true;
                        lead.Status = LeadStatuses.Qualified;
                        lead.Priority = score.Score >= 80 ? "High" : "Medium";
                    }
                }
            }

            scored.Add(new JsonObject
            {
                ["researchId"] = research.Id.ToString(),
                ["leadId"] = research.LeadId?.ToString(),
                ["score"] = score.Score,
                ["qualified"] = score.MeetsThreshold,
                ["scoreIsEstimate"] = true
            });
        }

        if (qualifiedCount > 0 && settings.ActiveObjectiveId is Guid oid)
        {
            var objective = await _db.SalesAgentObjectives.FirstOrDefaultAsync(x => x.Id == oid, ct);
            if (objective is not null)
                objective.QualifiedCount += qualifiedCount;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(
            $"Scored {batch.Count} prospects. {qualifiedCount} met the threshold. Scores are estimates, not verified business truth.",
            new JsonObject
            {
                ["qualified"] = qualifiedCount > 0,
                ["qualifiedCount"] = qualifiedCount,
                ["scoredCount"] = batch.Count,
                ["scoreIsEstimate"] = true,
                ["results"] = scored
            });
    }

    public async Task<NalvaToolOutcome> CreateOutreachCampaignAsync(string agentKey, Guid executionId, JsonElement args, CancellationToken ct)
    {
        var settings = await EnsureSettingsAsync(ct);
        var name = NalvaArgs.Str(args, "name") ?? NalvaArgs.Str(args, "title") ?? "Sales outreach campaign";
        var campaign = new SalesOutreachCampaign
        {
            ObjectiveId = NalvaArgs.GuidOf(args, "objectiveId") ?? settings.ActiveObjectiveId,
            Name = NalvaGuard.Clip(name, 200),
            Description = NalvaGuard.Clip(NalvaArgs.Str(args, "description"), 1000),
            Status = SalesAgentStatuses.Draft,
            SequenceJson = NalvaArgs.Str(args, "sequenceJson") ?? JsonSerializer.Serialize(new[]
            {
                new { step = 1, delayDays = 0, purpose = "Intro" },
                new { step = 2, delayDays = 3, purpose = "Follow-up" },
                new { step = 3, delayDays = 7, purpose = "Final check-in" }
            }),
            StartedAt = DateTime.UtcNow
        };
        _db.SalesOutreachCampaigns.Add(campaign);
        await _db.SaveChangesAsync(ct);
        return Ok("Outreach campaign created. No emails were sent.", new JsonObject
        {
            ["created"] = true,
            ["campaignId"] = campaign.Id.ToString(),
            ["status"] = campaign.Status
        });
    }

    public async Task<NalvaToolOutcome> GeneratePersonalizedOutreachAsync(string agentKey, Guid executionId, JsonElement args, CancellationToken ct)
    {
        var settings = await EnsureSettingsAsync(ct);
        var campaignId = NalvaArgs.GuidOf(args, "campaignId");
        SalesOutreachCampaign? campaign;
        if (campaignId is Guid cid)
            campaign = await _db.SalesOutreachCampaigns.FirstOrDefaultAsync(x => x.Id == cid, ct);
        else
        {
            campaign = new SalesOutreachCampaign
            {
                ObjectiveId = settings.ActiveObjectiveId,
                Name = "Auto outreach " + DateTime.UtcNow.ToString("yyyy-MM-dd"),
                Status = SalesAgentStatuses.Draft,
                SequenceJson = "[]",
                StartedAt = DateTime.UtcNow
            };
            _db.SalesOutreachCampaigns.Add(campaign);
            await _db.SaveChangesAsync(ct);
        }

        if (campaign is null)
            return Ok("Campaign was not found.", new JsonObject { ["created"] = false });

        var researchId = NalvaArgs.GuidOf(args, "researchId");
        var leadId = NalvaArgs.GuidOf(args, "leadId");
        SalesProspectResearch? research = null;
        Lead? lead = null;
        if (researchId is Guid rid)
            research = await _db.SalesProspectResearch.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid, ct);
        if (leadId is Guid lid)
            lead = await _db.Leads.AsNoTracking().FirstOrDefaultAsync(x => x.Id == lid, ct);
        if (research is null && lead is not null)
            research = await _db.SalesProspectResearch.AsNoTracking().FirstOrDefaultAsync(x => x.LeadId == lead.Id, ct);

        var toEmail = NalvaArgs.Str(args, "to") ?? NalvaArgs.Str(args, "email") ?? research?.PublicContactEmail ?? lead?.Email;
        if (string.IsNullOrWhiteSpace(toEmail))
            return Ok("No recipient email is available for outreach.", new JsonObject { ["created"] = false });

        var suppressed = await _db.SalesEmailSuppressions.AsNoTracking()
            .AnyAsync(x => x.Email == toEmail.Trim().ToLowerInvariant(), ct);
        if (suppressed)
            return Ok("Recipient is on the suppression list. No outreach was drafted.", new JsonObject { ["created"] = false, ["suppressed"] = true });

        var company = research?.CompanyName ?? lead?.CompanyName ?? "your team";
        var industry = research?.Industry ?? "your industry";
        var fit = NalvaArgs.Str(args, "fitExplanation")
                  ?? $"Potential fit because {company} operates in {industry} and may need {(ParseIcp(settings.IdealCustomerProfileJson).RequiredServices ?? ".NET and Angular development services")}.";
        var subject = NalvaArgs.Str(args, "subject")
                      ?? $"Helping {company} with .NET and Angular delivery";
        var body = NalvaArgs.Str(args, "body")
                   ?? $"Hello{(string.IsNullOrWhiteSpace(research?.DecisionMakerName) ? "" : " " + research!.DecisionMakerName)},\n\n"
                      + $"I work with teams like {company} that need reliable .NET and Angular delivery.\n"
                      + $"{fit}\n\n"
                      + "If useful, I can share a short outline of how similar software houses engage us.\n\n"
                      + "Best regards";

        var idempotency = $"outreach:{campaign.Id:N}:{toEmail.Trim().ToLowerInvariant()}:1";
        if (await _db.SalesOutreachMessages.AsNoTracking().AnyAsync(x => x.IdempotencyKey == idempotency, ct))
            return Ok("Outreach for this recipient already exists in the campaign.", new JsonObject { ["created"] = false, ["duplicate"] = true });

        var workItem = new NalvaWorkItem
        {
            AgentKey = agentKey,
            Kind = "Email",
            Title = subject,
            Body = body,
            Status = "Draft",
            RelatedEntityType = "Lead",
            RelatedEntityId = lead?.Id ?? research?.LeadId,
            ExecutionId = executionId
        };
        _db.NalvaWorkItems.Add(workItem);
        await _db.SaveChangesAsync(ct);

        var message = new SalesOutreachMessage
        {
            CampaignId = campaign.Id,
            LeadId = lead?.Id ?? research?.LeadId,
            ProspectResearchId = research?.Id,
            WorkItemId = workItem.Id,
            ToEmail = toEmail.Trim().ToLowerInvariant(),
            Subject = NalvaGuard.Clip(subject, 300),
            Body = NalvaGuard.Clip(body, 8000),
            FitExplanation = NalvaGuard.Clip(fit, 2000),
            SequenceStep = NalvaArgs.Int(args, "sequenceStep") ?? 1,
            Status = SalesAgentStatuses.Draft,
            IdempotencyKey = idempotency
        };
        _db.SalesOutreachMessages.Add(message);
        campaign.DraftCount += 1;
        if (settings.ActiveObjectiveId is Guid oid)
        {
            var objective = await _db.SalesAgentObjectives.FirstOrDefaultAsync(x => x.Id == oid, ct);
            if (objective is not null)
                objective.OutreachDraftCount += 1;
        }
        await _db.SaveChangesAsync(ct);

        return Ok("Personalized outreach draft stored. External send still requires approval.", new JsonObject
        {
            ["created"] = true,
            ["messageId"] = message.Id.ToString(),
            ["campaignId"] = campaign.Id.ToString(),
            ["workItemId"] = workItem.Id.ToString(),
            ["to"] = message.ToEmail,
            ["subject"] = message.Subject,
            ["fitExplanation"] = message.FitExplanation,
            ["requiresApproval"] = settings.RequireOutreachApproval
        });
    }

    public async Task<NalvaToolOutcome?> TrySendOutreachEmailAsync(JsonElement args, string argsJson, bool approvalGranted, CancellationToken ct)
    {
        if (!approvalGranted)
            return null;

        var messageId = NalvaArgs.GuidOf(args, "outreachMessageId") ?? NalvaArgs.GuidOf(args, "messageId");
        SalesOutreachMessage? message = null;
        if (messageId is Guid mid)
            message = await _db.SalesOutreachMessages.FirstOrDefaultAsync(x => x.Id == mid, ct);

        var to = NalvaArgs.Str(args, "to") ?? NalvaArgs.Str(args, "email") ?? message?.ToEmail;
        var subject = NalvaArgs.Str(args, "subject") ?? message?.Subject ?? "Company message";
        var body = NalvaArgs.Str(args, "body") ?? message?.Body ?? "";
        if (string.IsNullOrWhiteSpace(to))
            return Ok("No recipient was supplied, so nothing was sent.", new JsonObject { ["performed"] = false });

        var normalized = to.Trim().ToLowerInvariant();
        if (await _db.SalesEmailSuppressions.AsNoTracking().AnyAsync(x => x.Email == normalized, ct))
        {
            if (message is not null)
                message.Status = SalesAgentStatuses.Suppressed;
            await _db.SaveChangesAsync(ct);
            return Ok("Recipient is suppressed. Nothing was sent.", new JsonObject { ["performed"] = false, ["suppressed"] = true });
        }

        var settings = await EnsureSettingsAsync(ct);
        var sentToday = await _db.SalesOutboundEmailLogs.CountAsync(x => x.Performed && x.CreatedAt >= DateTime.UtcNow.Date, ct);
        if (sentToday >= settings.MaxOutreachPerDay)
            return Ok("Daily outreach send limit reached.", new JsonObject { ["performed"] = false, ["dailyLimitReached"] = true });

        var idempotency = message?.IdempotencyKey ?? NalvaArgs.Str(args, "idempotencyKey") ?? $"send:{normalized}:{subject}";
        if (await _db.SalesOutboundEmailLogs.AsNoTracking().AnyAsync(x => x.OutreachMessageId == messageId && x.Performed, ct))
            return Ok("This outreach was already sent (idempotent).", new JsonObject { ["performed"] = true, ["idempotent"] = true });

        // Prefer live Gmail when connected; otherwise use explicit test sink when registered.
        if (_integrations is not null)
        {
            var node = JsonNode.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson) as JsonObject ?? new JsonObject();
            node["to"] = normalized;
            node["subject"] = subject;
            if (node["raw"] is null)
                node["raw"] = Convert.ToBase64String(Encoding.UTF8.GetBytes($"To: {normalized}\r\nSubject: {subject}\r\n\r\n{body}"));
            var live = await _integrations.ExecuteAsync("gmail", "send", node.ToJsonString(), true, ct);
            if (live.Status is not ("not_connected" or "not_enabled" or "unknown_provider"))
            {
                await LogSendAsync(message, normalized, subject, "gmail", live.Performed, false, live.Status, live.Message, null, ct);
                if (live.Performed && message is not null)
                    await MarkSentAsync(message, false, null, ct);
                return new NalvaToolOutcome
                {
                    Success = live.Performed,
                    Error = live.Performed ? null : live.Message,
                    Idempotent = false,
                    Summary = live.Message,
                    Data = live.Data
                };
            }
        }

        if (_emailSink is not null)
        {
            var sink = await _emailSink.SendAsync(normalized, subject, body, idempotency, ct);
            await LogSendAsync(message, normalized, subject, _emailSink.IsTestSink ? "test-sink" : "sink", sink.Performed, _emailSink.IsTestSink, sink.Status, sink.Message, sink.ProviderMessageId, ct);
            if (sink.Performed && message is not null)
                await MarkSentAsync(message, _emailSink.IsTestSink, sink.ProviderMessageId, ct);
            return Ok(sink.Message, new JsonObject
            {
                ["performed"] = sink.Performed,
                ["isTestSink"] = _emailSink.IsTestSink,
                ["providerMessageId"] = sink.ProviderMessageId,
                ["status"] = sink.Status,
                ["integration"] = _emailSink.IsTestSink ? "test-sink" : "sink"
            });
        }

        return Ok(
            "Approved. No Gmail connection and no test email sink are available, so nothing was sent.",
            new JsonObject { ["performed"] = false, ["integration"] = "prepared", ["action"] = "SendExternalEmail" });
    }

    public async Task<NalvaToolOutcome> ProcessProspectReplyAsync(JsonElement args, CancellationToken ct)
    {
        var messageId = NalvaArgs.GuidOf(args, "outreachMessageId") ?? NalvaArgs.GuidOf(args, "messageId");
        if (messageId is null)
            return Ok("No outreach message id was supplied.", new JsonObject { ["processed"] = false });
        var message = await _db.SalesOutreachMessages.FirstOrDefaultAsync(x => x.Id == messageId, ct);
        if (message is null)
            return Ok("Outreach message was not found.", new JsonObject { ["processed"] = false });

        var summary = NalvaGuard.Clip(NalvaArgs.Str(args, "replySummary") ?? "Prospect replied.", 1000);
        message.Status = SalesAgentStatuses.Replied;
        message.ResponseAt = DateTime.UtcNow;
        message.ResponseSummary = summary;

        var campaign = await _db.SalesOutreachCampaigns.FirstOrDefaultAsync(x => x.Id == message.CampaignId, ct);
        if (campaign is not null)
            campaign.ResponseCount += 1;

        if (message.LeadId is Guid leadId)
        {
            var lead = await _db.Leads.FirstOrDefaultAsync(x => x.Id == leadId, ct);
            if (lead is not null)
            {
                lead.Status = LeadStatuses.Qualified;
                lead.IsQualified = true;
                lead.Notes = NalvaGuard.Clip((lead.Notes ?? "") + "\nReply: " + summary, 1000);
                _db.CustomerInteractions.Add(new CustomerInteraction
                {
                    LeadId = lead.Id,
                    Channel = "Email",
                    Summary = "Prospect reply: " + summary,
                    OccurredAt = DateTime.UtcNow,
                    UserId = _user.UserId
                });
            }
        }

        Guid? followUpId = null;
        if (NalvaArgs.Bool(args, "scheduleFollowUp") != false)
        {
            var days = Math.Clamp(NalvaArgs.Int(args, "followUpDays") ?? 3, 1, 30);
            var followUp = new FollowUp
            {
                Title = "Sales Agent follow-up",
                LeadId = message.LeadId,
                DueAt = DateTime.UtcNow.AddDays(days),
                Status = FollowUpStatuses.Open,
                Notes = "Sales Agent follow-up after prospect reply."
            };
            _db.FollowUps.Add(followUp);
            await _db.SaveChangesAsync(ct);
            followUpId = followUp.Id;
        }

        if (message.CampaignId != Guid.Empty)
        {
            // already loaded
        }

        var settings = await EnsureSettingsAsync(ct);
        if (settings.ActiveObjectiveId is Guid oid)
        {
            var objective = await _db.SalesAgentObjectives.FirstOrDefaultAsync(x => x.Id == oid, ct);
            if (objective is not null)
                objective.ResponseCount += 1;
        }

        await _db.SaveChangesAsync(ct);
        return Ok("Prospect reply processed and CRM updated.", new JsonObject
        {
            ["processed"] = true,
            ["messageId"] = message.Id.ToString(),
            ["leadId"] = message.LeadId?.ToString(),
            ["followUpId"] = followUpId?.ToString(),
            ["status"] = message.Status
        });
    }

    public async Task<NalvaToolOutcome> GenerateSalesCampaignReportAsync(string agentKey, Guid executionId, JsonElement args, CancellationToken ct)
    {
        var objectiveId = NalvaArgs.GuidOf(args, "objectiveId");
        var campaignId = NalvaArgs.GuidOf(args, "campaignId");
        var researched = await _db.SalesProspectResearch.CountAsync(x => objectiveId == null || x.ObjectiveId == objectiveId, ct);
        var qualified = await _db.SalesProspectResearch.CountAsync(x => x.IsQualified && (objectiveId == null || x.ObjectiveId == objectiveId), ct);
        var drafts = await _db.SalesOutreachMessages.CountAsync(x => x.Status == SalesAgentStatuses.Draft || x.Status == SalesAgentStatuses.PendingApproval, ct);
        var sent = await _db.SalesOutboundEmailLogs.CountAsync(x => x.Performed, ct);
        var responses = await _db.SalesOutreachMessages.CountAsync(x => x.Status == SalesAgentStatuses.Replied, ct);
        var followUps = await _db.FollowUps.CountAsync(x => x.Notes != null && x.Notes.Contains("Sales Agent"), ct);
        var failed = await _db.SalesOutboundEmailLogs.CountAsync(x => !x.Performed, ct);
        var cost = await _db.NalvaExecutions.Where(x => x.AgentKey == "sales").SumAsync(x => (decimal?)x.EstimatedCostUsd, ct) ?? 0m;
        var summary =
            $"Campaign report from persisted records: {researched} researched, {qualified} qualified, {sent} emails sent, {responses} responses, {followUps} follow-ups, {failed} failed sends.";

        var report = new JsonObject
        {
            ["prospectsResearched"] = researched,
            ["qualifiedLeads"] = qualified,
            ["outreachDrafts"] = drafts,
            ["emailsSent"] = sent,
            ["responses"] = responses,
            ["followUpsScheduled"] = followUps,
            ["failedSends"] = failed,
            ["estimatedCostUsd"] = cost,
            ["costIsEstimate"] = true
        };

        if (campaignId is Guid cid)
        {
            var campaign = await _db.SalesOutreachCampaigns.FirstOrDefaultAsync(x => x.Id == cid, ct);
            if (campaign is not null)
            {
                campaign.ReportJson = report.ToJsonString();
                campaign.Status = SalesAgentStatuses.Completed;
                campaign.CompletedAt = DateTime.UtcNow;
            }
        }

        if (objectiveId is Guid oid)
        {
            var objective = await _db.SalesAgentObjectives.FirstOrDefaultAsync(x => x.Id == oid, ct);
            if (objective is not null)
            {
                objective.ResultSummary = summary;
                objective.ResultJson = report.ToJsonString();
                objective.Status = SalesAgentStatuses.Completed;
                objective.CompletedAt = DateTime.UtcNow;
            }
        }

        var item = new NalvaWorkItem
        {
            AgentKey = agentKey,
            Kind = "Report",
            Title = "Sales campaign report",
            Body = summary,
            Status = "Draft",
            ExecutionId = executionId
        };
        _db.NalvaWorkItems.Add(item);
        await _db.SaveChangesAsync(ct);
        report["workItemId"] = item.Id.ToString();
        return Ok(summary, report);
    }

    private async Task MarkSentAsync(SalesOutreachMessage message, bool testSink, string? providerMessageId, CancellationToken ct)
    {
        message.Status = SalesAgentStatuses.Sent;
        message.SentAt = DateTime.UtcNow;
        message.UsedTestSink = testSink;
        message.ProviderMessageId = providerMessageId;
        var campaign = await _db.SalesOutreachCampaigns.FirstOrDefaultAsync(x => x.Id == message.CampaignId, ct);
        if (campaign is not null)
            campaign.SentCount += 1;
        var settings = await EnsureSettingsAsync(ct);
        if (settings.ActiveObjectiveId is Guid oid)
        {
            var objective = await _db.SalesAgentObjectives.FirstOrDefaultAsync(x => x.Id == oid, ct);
            if (objective is not null)
                objective.OutreachSentCount += 1;
        }
        if (message.WorkItemId is Guid wid)
        {
            var work = await _db.NalvaWorkItems.FirstOrDefaultAsync(x => x.Id == wid, ct);
            if (work is not null)
                work.Status = "Sent";
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task LogSendAsync(
        SalesOutreachMessage? message,
        string to,
        string subject,
        string provider,
        bool performed,
        bool testSink,
        string status,
        string? error,
        string? providerMessageId,
        CancellationToken ct)
    {
        _db.SalesOutboundEmailLogs.Add(new SalesOutboundEmailLog
        {
            OutreachMessageId = message?.Id,
            ToEmail = to,
            Subject = subject,
            ProviderKey = provider,
            Performed = performed,
            IsTestSink = testSink,
            Status = status,
            ProviderMessageId = providerMessageId,
            Error = performed ? null : NalvaGuard.Clip(error, 500)
        });
        await _db.SaveChangesAsync(ct);
    }

    private async Task<(bool Connected, string Message)> EmailStatusAsync(CancellationToken ct)
    {
        var connection = await _db.CompanyIntegrationConnections.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProviderKey == "gmail" && x.Status == "Connected", ct);
        if (connection is not null)
            return (true, "Gmail connection is present for this company.");
        if (_emailSink is not null)
            return (false, _emailSink.IsTestSink
                ? "No Gmail connection. A test email sink is registered for automated verification only."
                : "No Gmail connection. An alternate email sink is registered.");
        return (false, "No mailbox is connected. Connect Gmail under Integrations before live sends.");
    }

    private async Task<SalesAgentSettings> EnsureSettingsAsync(CancellationToken ct)
    {
        var row = await _db.SalesAgentSettings.FirstOrDefaultAsync(ct);
        if (row is not null)
            return row;
        row = new SalesAgentSettings
        {
            QualificationCriteriaJson = DefaultCriteriaJson(),
            IdealCustomerProfileJson = """{"industry":"Software","requiredServices":".NET,Angular","keywords":"software development agency"}"""
        };
        _db.SalesAgentSettings.Add(row);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    private static string DefaultCriteriaJson() =>
        """[{"key":"industry","label":"Industry","expectedValue":null,"weight":20},{"key":"country","label":"Geographic location","expectedValue":null,"weight":15},{"key":"companySize","label":"Company size","expectedValue":null,"weight":10},{"key":"services","label":"Required services","expectedValue":".NET,Angular","weight":25},{"key":"contact","label":"Public business contact","expectedValue":null,"weight":20},{"key":"website","label":"Company website","expectedValue":null,"weight":10}]""";

    private static IReadOnlyList<QualificationCriterion> ParseCriteria(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            var rows = JsonSerializer.Deserialize<List<CriteriaDto>>(json) ?? [];
            return rows.Select(x => new QualificationCriterion(x.key ?? "custom", x.label ?? x.key ?? "Criterion", x.expectedValue, x.weight <= 0 ? 10 : x.weight)).ToList();
        }
        catch
        {
            return [];
        }
    }

    private static Icp ParseIcp(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Icp(null, null, null, null, null);
        try
        {
            var dto = JsonSerializer.Deserialize<IcpDto>(json) ?? new IcpDto();
            return new Icp(dto.industry, dto.country, dto.companySize, dto.requiredServices, dto.keywords);
        }
        catch
        {
            return new Icp(null, null, null, null, null);
        }
    }

    private static string BuildIcpFromObjective(string objective, string fallbackJson)
    {
        var country = objective.Contains("UK", StringComparison.OrdinalIgnoreCase) || objective.Contains("United Kingdom", StringComparison.OrdinalIgnoreCase)
            ? "United Kingdom"
            : null;
        var services = objective.Contains(".NET", StringComparison.OrdinalIgnoreCase) || objective.Contains("Angular", StringComparison.OrdinalIgnoreCase)
            ? ".NET,Angular"
            : null;
        var industry = objective.Contains("software", StringComparison.OrdinalIgnoreCase) ? "Software" : null;
        if (country is null && services is null && industry is null)
            return string.IsNullOrWhiteSpace(fallbackJson) ? "{}" : fallbackJson;
        return JsonSerializer.Serialize(new { industry, country, requiredServices = services, keywords = "software development" });
    }

    private static int? ExtractTarget(string text)
    {
        var digits = new string(text.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) ? n : null;
    }

    private static string BuildDedupKey(string company, string? website, string? email)
    {
        var raw = (email ?? "") + "|" + (website ?? "") + "|" + company.Trim().ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    private static (string Status, string? Notes) ValidateQuality(LeadResearchResult item)
    {
        if (string.IsNullOrWhiteSpace(item.CompanyName))
            return ("Invalid", "Company name is required.");
        if (string.IsNullOrWhiteSpace(item.SourceUrl) && string.IsNullOrWhiteSpace(item.Website) && string.IsNullOrWhiteSpace(item.PublicContactEmail))
            return ("Invalid", "At least one of source URL, website, or public contact email is required.");
        if (!string.IsNullOrWhiteSpace(item.PublicContactEmail) && !item.PublicContactEmail.Contains('@'))
            return ("Invalid", "Public contact email failed format validation.");
        if (string.IsNullOrWhiteSpace(item.SourceAttribution) || string.IsNullOrWhiteSpace(item.ProviderKey))
            return ("Invalid", "Source attribution is required.");
        return ("Valid", item.ContactVerified ? "Contact marked verified by provider." : "Contact not independently verified.");
    }

    private static NalvaToolOutcome Ok(string summary, JsonObject data) => new()
    {
        Success = true,
        Summary = summary,
        Data = data
    };

    private sealed record Icp(string? Industry, string? Country, string? CompanySize, string? RequiredServices, string? Keywords);
    private sealed class IcpDto
    {
        public string? industry { get; set; }
        public string? country { get; set; }
        public string? companySize { get; set; }
        public string? requiredServices { get; set; }
        public string? keywords { get; set; }
    }
    private sealed class CriteriaDto
    {
        public string? key { get; set; }
        public string? label { get; set; }
        public string? expectedValue { get; set; }
        public int weight { get; set; }
    }
}
