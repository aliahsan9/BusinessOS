using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Application.Features.Operations.DTOs;
using BusinessOS.Application.Features.Operations.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class CrmOperationsService : ICrmOperationsService
{
    private static readonly (string Name, bool Won, bool Lost)[] DefaultStages =
    [
        ("New", false, false),
        ("Contacted", false, false),
        ("Qualified", false, false),
        ("Proposal Sent", false, false),
        ("Negotiation", false, false),
        ("Won", true, false),
        ("Lost", false, true)
    ];

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly INalvaEventOutbox? _events;

    public CrmOperationsService(IApplicationDbContext db, ICurrentUserService user, INalvaEventOutbox? events = null)
    {
        _db = db;
        _user = user;
        _events = events;
    }

    public async Task<IReadOnlyList<StageDto>> GetStagesAsync(CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.LeadView, PermissionCodes.LeadManage, PermissionCodes.DealView);
        await EnsureStagesAsync(cancellationToken);
        return await _db.PipelineStages.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder)
            .Select(x => new StageDto(x.Id, x.Name, x.SortOrder, x.IsWon, x.IsLost))
            .ToListAsync(cancellationToken);
    }

    public async Task<StageDto> SaveStageAsync(Guid? id, StageRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.LeadManage);
        var stage = id is null ? new PipelineStage() : await _db.PipelineStages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Pipeline stage not found.");
        if (id is null)
            _db.PipelineStages.Add(stage);
        stage.Name = Ops.Required(request.Name, "Name");
        stage.SortOrder = request.SortOrder;
        stage.IsWon = request.IsWon;
        stage.IsLost = request.IsLost;
        stage.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return new StageDto(stage.Id, stage.Name, stage.SortOrder, stage.IsWon, stage.IsLost);
    }

    public async Task<PagedResult<LeadDto>> GetLeadsAsync(string? search, Guid? stageId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.LeadView, PermissionCodes.LeadManage);
        await EnsureStagesAsync(cancellationToken);
        var query = from lead in _db.Leads.AsNoTracking()
                    join stage in _db.PipelineStages.AsNoTracking() on lead.StageId equals stage.Id into stages
                    from stage in stages.DefaultIfEmpty()
                    select new { lead, stage };
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.lead.Name.Contains(term) || (x.lead.Email != null && x.lead.Email.Contains(term)) || (x.lead.CompanyName != null && x.lead.CompanyName.Contains(term)));
        }
        if (stageId is not null)
            query = query.Where(x => x.lead.StageId == stageId);
        var paged = await query.OrderByDescending(x => x.lead.CreatedAt).PageAsync(page, pageSize, cancellationToken);
        return new PagedResult<LeadDto>
        {
            Items = paged.Items.Select(x => MapLead(x.lead, x.stage == null ? null : x.stage.Name)).ToList(),
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount
        };
    }

    public async Task<LeadDto> SaveLeadAsync(Guid? id, LeadRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.LeadManage);
        await EnsureStagesAsync(cancellationToken);
        var lead = id is null ? new Lead() : await _db.Leads.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Lead not found.");
        if (id is null)
        {
            lead.StageId = request.StageId ?? await _db.PipelineStages.OrderBy(x => x.SortOrder).Select(x => x.Id).FirstAsync(cancellationToken);
            lead.Status = LeadStatuses.New;
            _db.Leads.Add(lead);
        }
        lead.Name = Ops.Required(request.Name, "Name");
        lead.CompanyName = request.CompanyName?.Trim();
        lead.Email = request.Email?.Trim().ToLowerInvariant();
        lead.Phone = request.Phone?.Trim();
        lead.Source = string.IsNullOrWhiteSpace(request.Source) ? "Manual" : request.Source.Trim();
        lead.Priority = string.IsNullOrWhiteSpace(request.Priority) ? "Medium" : request.Priority.Trim();
        if (request.StageId is not null && id is not null && request.StageId != lead.StageId)
            await MoveLeadAsync(lead.Id, request.StageId.Value, cancellationToken);
        else if (request.StageId is not null)
            lead.StageId = request.StageId;
        lead.AssignedUserId = request.AssignedUserId;
        lead.EstimatedValue = request.EstimatedValue;
        lead.ExpectedCloseDate = request.ExpectedCloseDate;
        lead.Notes = request.Notes?.Trim();
        lead.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        if (id is null && _events is not null)
            await _events.EnqueueAsync(NalvaEventNames.LeadCreated, $"{{\"leadId\":\"{lead.Id}\",\"priority\":\"{lead.Priority}\"}}", "LeadCreated:" + lead.Id.ToString("N"), cancellationToken);
        return await MapLeadAsync(lead.Id, cancellationToken);
    }

    public async Task<LeadDto> MoveLeadAsync(Guid id, Guid stageId, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.LeadManage);
        var lead = await _db.Leads.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Lead not found.");
        var stage = await _db.PipelineStages.FirstOrDefaultAsync(x => x.Id == stageId, cancellationToken)
            ?? throw new NotFoundException("Pipeline stage not found.");
        var from = lead.StageId;
        lead.StageId = stage.Id;
        lead.Status = stage.IsWon ? LeadStatuses.Converted : stage.IsLost ? LeadStatuses.Lost : stage.Name == "Qualified" ? LeadStatuses.Qualified : lead.Status;
        lead.IsQualified = lead.IsQualified || stage.Name == "Qualified" || stage.SortOrder >= 3 && !stage.IsLost;
        lead.UpdatedAt = DateTime.UtcNow;
        _db.StageTransitions.Add(new StageTransition
        {
            EntityType = "Lead",
            EntityId = lead.Id,
            FromStageId = from,
            ToStageId = stage.Id,
            UserId = _user.UserId
        });
        await _db.SaveChangesAsync(cancellationToken);
        if (_events is not null && (stage.Name == "Qualified" || lead.Status == LeadStatuses.Qualified))
            await _events.EnqueueAsync(NalvaEventNames.LeadQualified, $"{{\"leadId\":\"{lead.Id}\"}}", "LeadQualified:" + lead.Id.ToString("N") + ":" + stage.Id.ToString("N"), cancellationToken);
        return await MapLeadAsync(lead.Id, cancellationToken);
    }

    public async Task<ConvertLeadResult> ConvertLeadAsync(Guid id, bool createDeal, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.LeadManage, PermissionCodes.DealManage, PermissionCodes.CustomerCreate);
        var lead = await _db.Leads.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Lead not found.");
        if (lead.CustomerId is not null && lead.Status == LeadStatuses.Converted)
            return new ConvertLeadResult(lead.Id, lead.CustomerId.Value, lead.DealId, false);

        var email = string.IsNullOrWhiteSpace(lead.Email) ? null : lead.Email.Trim().ToLowerInvariant();
        Customer? customer = null;
        var created = false;
        if (lead.CustomerId is not null)
            customer = await _db.Customers.FirstOrDefaultAsync(x => x.Id == lead.CustomerId, cancellationToken);
        if (customer is null && email is not null)
            customer = await _db.Customers.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (customer is null)
        {
            if (email is null)
                throw new BadRequestException("A lead needs an email before it can create a customer.");
            var parts = lead.Name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            customer = new Customer
            {
                FirstName = parts.Length > 0 ? parts[0] : lead.Name,
                LastName = parts.Length > 1 ? parts[1] : "-",
                Email = email,
                PhoneNumber = string.IsNullOrWhiteSpace(lead.Phone) ? "Not provided" : lead.Phone,
                Address = "Not provided",
                City = "Not provided",
                Country = "Not provided",
                PostalCode = "-",
                Company = lead.CompanyName,
                Notes = lead.Notes,
                CustomerType = string.IsNullOrWhiteSpace(lead.CompanyName) ? "Individual" : "Business",
                Status = "Active",
                SourceLeadId = lead.Id,
                AssignedUserId = lead.AssignedUserId
            };
            _db.Customers.Add(customer);
            created = true;
        }
        else if (customer.SourceLeadId is null)
        {
            customer.SourceLeadId = lead.Id;
        }

        Deal? deal = null;
        if (createDeal)
        {
            if (lead.DealId is not null)
                deal = await _db.Deals.FirstOrDefaultAsync(x => x.Id == lead.DealId, cancellationToken);
            if (deal is null)
            {
                await EnsureStagesAsync(cancellationToken);
                var won = await _db.PipelineStages.FirstAsync(x => x.IsWon, cancellationToken);
                deal = new Deal
                {
                    Title = lead.CompanyName ?? lead.Name,
                    LeadId = lead.Id,
                    CustomerId = customer.Id,
                    StageId = won.Id,
                    AssignedUserId = lead.AssignedUserId,
                    Value = lead.EstimatedValue,
                    Probability = 100,
                    ExpectedCloseDate = lead.ExpectedCloseDate,
                    Status = DealStatuses.Won,
                    Notes = lead.Notes
                };
                _db.Deals.Add(deal);
            }
        }

        var wonStage = await _db.PipelineStages.FirstOrDefaultAsync(x => x.IsWon, cancellationToken);
        lead.CustomerId = customer.Id;
        lead.DealId = deal?.Id ?? lead.DealId;
        lead.Status = LeadStatuses.Converted;
        lead.IsQualified = true;
        if (wonStage is not null)
            lead.StageId = wonStage.Id;
        lead.UpdatedAt = DateTime.UtcNow;
        _db.CustomerInteractions.Add(new CustomerInteraction
        {
            CustomerId = customer.Id,
            LeadId = lead.Id,
            DealId = deal?.Id,
            Channel = "Sales",
            Summary = "Lead converted to customer.",
            UserId = _user.UserId
        });
        await _db.SaveChangesAsync(cancellationToken);
        if (created && _events is not null)
            await _events.EnqueueAsync(NalvaEventNames.CustomerRegistered, $"{{\"customerId\":\"{customer.Id}\",\"marketingConsent\":false}}", "CustomerRegistered:" + customer.Id.ToString("N"), cancellationToken);
        return new ConvertLeadResult(lead.Id, customer.Id, deal?.Id, created);
    }

    public async Task<PagedResult<DealDto>> GetDealsAsync(string? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.DealView, PermissionCodes.DealManage);
        var query = from deal in _db.Deals.AsNoTracking()
                    join stage in _db.PipelineStages.AsNoTracking() on deal.StageId equals stage.Id into stages
                    from stage in stages.DefaultIfEmpty()
                    select new { deal, stage };
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.deal.Status == status);
        var paged = await query.OrderByDescending(x => x.deal.CreatedAt).PageAsync(page, pageSize, cancellationToken);
        return new PagedResult<DealDto>
        {
            Items = paged.Items.Select(x => MapDeal(x.deal, x.stage == null ? null : x.stage.Name)).ToList(),
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount
        };
    }

    public async Task<DealDto> SaveDealAsync(Guid? id, DealRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.DealManage);
        var deal = id is null ? new Deal() : await _db.Deals.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Deal not found.");
        if (id is null)
            _db.Deals.Add(deal);
        deal.Title = Ops.Required(request.Title, "Title");
        deal.CustomerId = request.CustomerId;
        deal.LeadId = request.LeadId;
        deal.StageId = request.StageId;
        deal.AssignedUserId = request.AssignedUserId;
        deal.Value = request.Value;
        deal.Probability = Math.Clamp(request.Probability, 0, 100);
        deal.ExpectedCloseDate = request.ExpectedCloseDate;
        deal.Notes = request.Notes?.Trim();
        deal.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapDeal(deal, null);
    }

    public async Task<DealDto> WinDealAsync(Guid id, bool createProject, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.DealManage);
        var deal = await _db.Deals.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Deal not found.");
        if (deal.CustomerId is null && deal.LeadId is not null)
        {
            var converted = await ConvertLeadAsync(deal.LeadId.Value, false, cancellationToken);
            deal.CustomerId = converted.CustomerId;
        }
        if (deal.CustomerId is null)
            throw new BadRequestException("A won deal must be linked to a customer.");
        await EnsureStagesAsync(cancellationToken);
        var won = await _db.PipelineStages.FirstAsync(x => x.IsWon, cancellationToken);
        var from = deal.StageId;
        deal.StageId = won.Id;
        deal.Status = DealStatuses.Won;
        deal.Probability = 100;
        deal.UpdatedAt = DateTime.UtcNow;
        _db.StageTransitions.Add(new StageTransition
        {
            EntityType = "Deal",
            EntityId = deal.Id,
            FromStageId = from,
            ToStageId = won.Id,
            UserId = _user.UserId
        });
        if (createProject && deal.ProjectId is null)
        {
            var project = new Project
            {
                Name = deal.Title,
                CustomerId = deal.CustomerId,
                DealId = deal.Id,
                Status = ProjectStatus.Planning,
                Budget = deal.Value,
                ManagerUserId = deal.AssignedUserId,
                AssignedUserId = deal.AssignedUserId,
                Workflow = "General",
                Visibility = "Internal"
            };
            _db.Projects.Add(project);
            deal.ProjectId = project.Id;
        }
        await _db.SaveChangesAsync(cancellationToken);
        return MapDeal(deal, won.Name);
    }

    public async Task<PagedResult<FollowUpDto>> GetFollowUpsAsync(bool overdueOnly, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.LeadView, PermissionCodes.DealView);
        var now = DateTime.UtcNow;
        var query = _db.FollowUps.AsNoTracking().AsQueryable();
        if (overdueOnly)
            query = query.Where(x => x.Status == FollowUpStatuses.Open && x.DueAt < now);
        var paged = await query.OrderBy(x => x.DueAt).PageAsync(page, pageSize, cancellationToken);
        return new PagedResult<FollowUpDto>
        {
            Items = paged.Items.Select(x => MapFollowUp(x, now)).ToList(),
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount
        };
    }

    public async Task<FollowUpDto> SaveFollowUpAsync(FollowUpRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.LeadManage, PermissionCodes.DealManage);
        var followUp = new FollowUp
        {
            Title = Ops.Required(request.Title, "Title"),
            DueAt = request.DueAt,
            Priority = string.IsNullOrWhiteSpace(request.Priority) ? "Normal" : request.Priority.Trim(),
            AssignedUserId = request.AssignedUserId,
            LeadId = request.LeadId,
            CustomerId = request.CustomerId,
            DealId = request.DealId,
            Notes = request.Notes?.Trim()
        };
        _db.FollowUps.Add(followUp);
        if (!string.IsNullOrWhiteSpace(request.AssignedUserId))
        {
            _db.Notifications.Add(new Notification
            {
                UserId = request.AssignedUserId,
                Title = "Follow-up assigned",
                Message = followUp.Title,
                Type = "FollowUp",
                Link = "/crm/follow-ups"
            });
        }
        await _db.SaveChangesAsync(cancellationToken);
        return MapFollowUp(followUp, DateTime.UtcNow);
    }

    public async Task<FollowUpDto> CompleteFollowUpAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.LeadManage, PermissionCodes.DealManage);
        var followUp = await _db.FollowUps.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Follow-up not found.");
        followUp.Status = FollowUpStatuses.Completed;
        followUp.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapFollowUp(followUp, DateTime.UtcNow);
    }

    public async Task<IReadOnlyList<SegmentDto>> GetSegmentsAsync(CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.CustomerView, PermissionCodes.LeadView);
        var segments = await _db.CustomerSegments.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var result = new List<SegmentDto>();
        foreach (var segment in segments)
        {
            var count = await SegmentQuery(segment).CountAsync(cancellationToken);
            result.Add(new SegmentDto(segment.Id, segment.Name, segment.CustomerType, segment.Industry, segment.Country, segment.Tag, segment.MinSpend, count));
        }
        return result;
    }

    public async Task<SegmentDto> SaveSegmentAsync(SegmentRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.CustomerUpdate, PermissionCodes.LeadManage);
        var segment = new CustomerSegment
        {
            Name = Ops.Required(request.Name, "Name"),
            CustomerType = request.CustomerType?.Trim(),
            Industry = request.Industry?.Trim(),
            Country = request.Country?.Trim(),
            Tag = request.Tag?.Trim(),
            MinSpend = request.MinSpend,
            Notes = request.Notes?.Trim()
        };
        _db.CustomerSegments.Add(segment);
        await _db.SaveChangesAsync(cancellationToken);
        return new SegmentDto(segment.Id, segment.Name, segment.CustomerType, segment.Industry, segment.Country, segment.Tag, segment.MinSpend, 0);
    }

    public async Task<PagedResult<CustomerMatchDto>> GetSegmentCustomersAsync(Guid id, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.CustomerView);
        var segment = await _db.CustomerSegments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Segment not found.");
        var paged = await SegmentQuery(segment).OrderBy(x => x.Name).PageAsync(page, pageSize, cancellationToken);
        return paged;
    }

    public async Task<InteractionDto> AddInteractionAsync(InteractionRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.CustomerUpdate, PermissionCodes.LeadManage, PermissionCodes.DealManage);
        var interaction = new CustomerInteraction
        {
            CustomerId = request.CustomerId,
            LeadId = request.LeadId,
            DealId = request.DealId,
            Channel = Ops.Required(request.Channel, "Channel"),
            Summary = Ops.Required(request.Summary, "Summary"),
            OccurredAt = request.OccurredAt ?? DateTime.UtcNow,
            UserId = _user.UserId
        };
        _db.CustomerInteractions.Add(interaction);
        await _db.SaveChangesAsync(cancellationToken);
        return new InteractionDto(interaction.Id, interaction.CustomerId, interaction.LeadId, interaction.Channel, interaction.Summary, interaction.OccurredAt);
    }

    public async Task<IReadOnlyList<InteractionDto>> GetInteractionsAsync(Guid? customerId, Guid? leadId, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.CustomerView, PermissionCodes.LeadView);
        var query = _db.CustomerInteractions.AsNoTracking().AsQueryable();
        if (customerId is not null)
            query = query.Where(x => x.CustomerId == customerId);
        if (leadId is not null)
            query = query.Where(x => x.LeadId == leadId);
        return await query.OrderByDescending(x => x.OccurredAt).Take(100)
            .Select(x => new InteractionDto(x.Id, x.CustomerId, x.LeadId, x.Channel, x.Summary, x.OccurredAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<CrmAnalyticsDto> GetAnalyticsAsync(CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.LeadView, PermissionCodes.DealView, PermissionCodes.AnalyticsView);
        var leads = await _db.Leads.AsNoTracking().Select(x => x.Status).ToListAsync(cancellationToken);
        var deals = await _db.Deals.AsNoTracking().Select(x => new { x.Status, x.Value }).ToListAsync(cancellationToken);
        var total = leads.Count;
        var converted = leads.Count(x => x == LeadStatuses.Converted);
        return new CrmAnalyticsDto(
            total,
            leads.Count(x => x == LeadStatuses.Qualified),
            converted,
            leads.Count(x => x == LeadStatuses.Lost),
            deals.Count(x => x.Status == DealStatuses.Open),
            deals.Count(x => x.Status == DealStatuses.Won),
            deals.Where(x => x.Status == DealStatuses.Open).Sum(x => x.Value),
            total == 0 ? 0 : Math.Round(converted * 100m / total, 2));
    }

    private async Task EnsureStagesAsync(CancellationToken cancellationToken)
    {
        if (await _db.PipelineStages.AnyAsync(cancellationToken))
            return;
        for (var i = 0; i < DefaultStages.Length; i++)
        {
            var stage = DefaultStages[i];
            _db.PipelineStages.Add(new PipelineStage
            {
                Name = stage.Name,
                SortOrder = i + 1,
                IsWon = stage.Won,
                IsLost = stage.Lost
            });
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<CustomerMatchDto> SegmentQuery(CustomerSegment segment)
    {
        var query = _db.Customers.AsNoTracking().Select(customer => new
        {
            customer.Id,
            Name = customer.FirstName + " " + customer.LastName,
            customer.Email,
            customer.Company,
            customer.CustomerType,
            customer.Industry,
            customer.Country,
            customer.Tags,
            Spend = _db.Orders.Where(order => order.CustomerId == customer.Id && order.Status != "Cancelled").Sum(order => (decimal?)order.GrandTotal) ?? 0
        });
        if (!string.IsNullOrWhiteSpace(segment.CustomerType))
            query = query.Where(x => x.CustomerType == segment.CustomerType);
        if (!string.IsNullOrWhiteSpace(segment.Industry))
            query = query.Where(x => x.Industry == segment.Industry);
        if (!string.IsNullOrWhiteSpace(segment.Country))
            query = query.Where(x => x.Country == segment.Country);
        if (!string.IsNullOrWhiteSpace(segment.Tag))
            query = query.Where(x => x.Tags != null && x.Tags.Contains(segment.Tag));
        if (segment.MinSpend is not null)
            query = query.Where(x => x.Spend >= segment.MinSpend);
        return query.Select(x => new CustomerMatchDto(x.Id, x.Name, x.Email, x.Company, x.Spend));
    }

    private async Task<LeadDto> MapLeadAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await (from lead in _db.Leads.AsNoTracking()
                         join stage in _db.PipelineStages.AsNoTracking() on lead.StageId equals stage.Id into stages
                         from stage in stages.DefaultIfEmpty()
                         where lead.Id == id
                         select new { lead, name = stage == null ? null : stage.Name }).FirstAsync(cancellationToken);
        return MapLead(row.lead, row.name);
    }

    private static LeadDto MapLead(Lead lead, string? stageName) =>
        new(lead.Id, lead.Name, lead.CompanyName, lead.Email, lead.Phone, lead.Source, lead.Status, lead.Priority,
            lead.StageId, stageName, lead.AssignedUserId, lead.EstimatedValue, lead.ExpectedCloseDate, lead.IsQualified,
            lead.CustomerId, lead.DealId, lead.Notes);

    private static DealDto MapDeal(Deal deal, string? stageName) =>
        new(deal.Id, deal.Title, deal.CustomerId, deal.LeadId, deal.StageId, stageName, deal.AssignedUserId,
            deal.Value, deal.Probability, deal.ExpectedCloseDate, deal.Status, deal.ProjectId, deal.Notes);

    private static FollowUpDto MapFollowUp(FollowUp followUp, DateTime now) =>
        new(followUp.Id, followUp.Title, followUp.DueAt, followUp.Priority, followUp.Status, followUp.AssignedUserId,
            followUp.LeadId, followUp.CustomerId, followUp.DealId, followUp.Status == FollowUpStatuses.Open && followUp.DueAt < now);
}
