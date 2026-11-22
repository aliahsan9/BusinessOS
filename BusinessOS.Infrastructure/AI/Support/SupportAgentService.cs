using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Notifications.DTOs;
using BusinessOS.Application.Features.Notifications.Services;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Application.Features.Support;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI.Support;

public sealed class SupportAgentService : ISupportAgentService
{
    private static readonly Regex HumanRequest = new(@"\b(human|agent|person|representative|manager|speak to someone)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RefundWords = new(@"\b(refund|chargeback|money back)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CancelWords = new(@"\b(cancel(lation)?|void order)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AngryWords = new(@"\b(lawsuit|lawyer|attorney|fraud|scam|unacceptable|furious|angry)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly BusinessOSDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly ICurrentUserService _user;
    private readonly SupportChannelRouter _channels;
    private readonly INotificationService? _notifications;
    private readonly INalvaEventOutbox? _events;

    public SupportAgentService(
        BusinessOSDbContext db,
        ITenantProvider tenant,
        ICurrentUserService user,
        SupportChannelRouter channels,
        INotificationService? notifications = null,
        INalvaEventOutbox? events = null)
    {
        _db = db;
        _tenant = tenant;
        _user = user;
        _channels = channels;
        _notifications = notifications;
        _events = events;
    }

    private Guid CompanyId => _tenant.HasTenant() ? _tenant.TenantId : throw new BadRequestException("Company context is required.");

    public async Task<SupportOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var tickets = await _db.SupportTickets.AsNoTracking().ToListAsync(cancellationToken);
        var openStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            SupportTicketStatuses.New, SupportTicketStatuses.Open, SupportTicketStatuses.InProgress, SupportTicketStatuses.WaitingForCustomer
        };
        var resolved = tickets.Where(x => x.Status is SupportTicketStatuses.Resolved or SupportTicketStatuses.Closed).ToList();
        var resolutionHours = resolved
            .Where(x => x.ResolvedAt.HasValue)
            .Select(x => (x.ResolvedAt!.Value - x.CreatedAt).TotalHours)
            .ToList();

        var messages = await _db.SupportMessages.AsNoTracking()
            .Where(x => x.Direction == SupportMessageDirections.Outbound && x.AuthorType == "Agent")
            .Select(x => new { x.TicketId, x.CreatedAt })
            .ToListAsync(cancellationToken);
        var firstCustomer = await _db.SupportMessages.AsNoTracking()
            .Where(x => x.Direction == SupportMessageDirections.Inbound)
            .GroupBy(x => x.TicketId)
            .Select(g => new { TicketId = g.Key, FirstAt = g.Min(x => x.CreatedAt) })
            .ToListAsync(cancellationToken);
        var responseMinutes = new List<double>();
        foreach (var inbound in firstCustomer)
        {
            var firstReply = messages.Where(x => x.TicketId == inbound.TicketId && x.CreatedAt >= inbound.FirstAt).OrderBy(x => x.CreatedAt).FirstOrDefault();
            if (firstReply is not null)
                responseMinutes.Add((firstReply.CreatedAt - inbound.FirstAt).TotalMinutes);
        }

        var approvals = await _db.NalvaApprovals.AsNoTracking().CountAsync(x => x.Status == NalvaStatuses.Pending && x.AgentKey == SupportAgentKeys.Support, cancellationToken);
        var executions = await _db.NalvaExecutions.AsNoTracking().Where(x => x.AgentKey == SupportAgentKeys.Support).ToListAsync(cancellationToken);
        var channels = await ListChannelsAsync(cancellationToken);

        var open = tickets.Count(x => openStatuses.Contains(x.Status));
        var escalated = tickets.Count(x => x.Status == SupportTicketStatuses.Escalated);
        var resolvedCount = resolved.Count;
        var handled = tickets.Count(x => x.LastAgentMessageAt.HasValue || x.AssignedAgentKey == SupportAgentKeys.Support);

        return new SupportOverviewDto(
            tickets.Count,
            open,
            tickets.Count(x => x.Status == SupportTicketStatuses.WaitingForCustomer),
            escalated,
            resolvedCount,
            resolutionHours.Count == 0 ? null : Math.Round(resolutionHours.Average(), 2),
            tickets.Count(x => openStatuses.Contains(x.Status) || x.Status == SupportTicketStatuses.Escalated),
            approvals,
            handled,
            resolvedCount,
            escalated,
            responseMinutes.Count == 0 ? null : Math.Round(responseMinutes.Average(), 2),
            tickets.Count == 0 ? null : Math.Round((double)resolvedCount / tickets.Count, 4),
            executions.Count,
            executions.Sum(x => x.EstimatedCostUsd),
            true,
            channels);
    }

    public async Task<IReadOnlyList<SupportTicketListItemDto>> ListTicketsAsync(string? status = null, string? priority = null, CancellationToken cancellationToken = default)
    {
        var query = _db.SupportTickets.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(priority))
            query = query.Where(x => x.Priority == priority);

        var rows = await query.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).Take(200).ToListAsync(cancellationToken);
        var customerIds = rows.Where(x => x.CustomerId.HasValue).Select(x => x.CustomerId!.Value).Distinct().ToList();
        var customers = await _db.Customers.AsNoTracking().Where(x => customerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var counts = await _db.SupportMessages.AsNoTracking()
            .Where(x => rows.Select(t => t.Id).Contains(x.TicketId))
            .GroupBy(x => x.TicketId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        return rows.Select(x =>
        {
            customers.TryGetValue(x.CustomerId ?? Guid.Empty, out var customer);
            counts.TryGetValue(x.Id, out var messageCount);
            return new SupportTicketListItemDto(
                x.Id,
                x.TicketNumber,
                x.CustomerId,
                customer is null ? null : $"{customer.FirstName} {customer.LastName}".Trim(),
                x.Subject,
                x.Category,
                x.Priority,
                x.Status,
                x.Channel,
                x.AssignedAgentKey,
                x.AssignedUserId,
                x.AiSummary,
                x.RecommendedAction,
                x.CreatedAt,
                x.UpdatedAt,
                x.EscalatedAt,
                x.ResolvedAt,
                x.HumanTakeover,
                messageCount);
        }).ToList();
    }

    public async Task<SupportTicketDetailDto?> GetTicketAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        var ticket = await _db.SupportTickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == ticketId, cancellationToken);
        if (ticket is null)
            return null;
        return await MapDetailAsync(ticket, cancellationToken);
    }

    public async Task<IReadOnlyList<SupportChannelDto>> ListChannelsAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<SupportChannelDto>();
        foreach (var adapter in _channels.All)
            list.Add(await adapter.GetStatusAsync(cancellationToken));
        return list;
    }

    public async Task<SupportProcessResult> ReceiveInboundAsync(SupportInboundMessageRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            throw new BadRequestException("Message body is required.");

        var channel = NormalizeChannel(request.Channel);
        var idempotency = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? request.ExternalMessageId
            : request.IdempotencyKey;

        if (!string.IsNullOrWhiteSpace(idempotency))
        {
            var existingMessage = await _db.SupportMessages.AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotency || x.ExternalMessageId == idempotency, cancellationToken);
            if (existingMessage is not null)
            {
                var existingTicket = await _db.SupportTickets.AsNoTracking().FirstAsync(x => x.Id == existingMessage.TicketId, cancellationToken);
                return new SupportProcessResult(
                    existingTicket.Id,
                    existingTicket.TicketNumber,
                    existingTicket.Status,
                    "Duplicate inbound message ignored.",
                    false,
                    SupportDeliveryStatuses.Skipped,
                    existingTicket.Status == SupportTicketStatuses.Escalated,
                    null,
                    new JsonObject { ["duplicate"] = true, ["ticketId"] = existingTicket.Id.ToString() });
            }
        }

        var context = await GetAuthorizedCustomerContextAsync(request.CustomerId, request.CustomerEmail, request.CustomerPhone, request.OrderId, request.OrderNumber, cancellationToken);
        await AttachProductMatchAsync(context, request.Body, cancellationToken);
        var customerId = ReadGuid(context, "customerId");
        var orderId = ReadGuid(context, "orderId");
        var customerFound = ReadBool(context, "customerFound");

        SupportTicket? ticket = null;
        if (!string.IsNullOrWhiteSpace(request.ExternalThreadId))
        {
            ticket = await _db.SupportTickets.FirstOrDefaultAsync(x => x.ExternalThreadId == request.ExternalThreadId, cancellationToken);
            if (ticket is not null && ticket.Status is SupportTicketStatuses.Resolved or SupportTicketStatuses.Closed)
            {
                ticket.Status = SupportTicketStatuses.Open;
                ticket.ReopenCount += 1;
                ticket.ResolvedAt = null;
                ticket.ClosedAt = null;
                ticket.UpdatedAt = DateTime.UtcNow;
            }
        }

        if (ticket is null && customerId is Guid cid)
        {
            ticket = await _db.SupportTickets
                .Where(x => x.CustomerId == cid && x.Status != SupportTicketStatuses.Closed && x.Status != SupportTicketStatuses.Resolved)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var created = false;
        if (ticket is null)
        {
            ticket = new SupportTicket
            {
                TicketNumber = await NextTicketNumberAsync(cancellationToken),
                CustomerId = customerId,
                Subject = string.IsNullOrWhiteSpace(request.Subject) ? Clip(request.Body, 80) : request.Subject.Trim(),
                Description = SanitizeCustomerText(request.Body),
                Category = Classify(request.Body),
                Priority = Prioritize(request.Body),
                Status = SupportTicketStatuses.Open,
                Channel = channel,
                AssignedAgentKey = SupportAgentKeys.Support,
                ExternalThreadId = request.ExternalThreadId,
                IdempotencyKey = string.IsNullOrWhiteSpace(idempotency) ? null : $"{idempotency}:ticket",
                RelatedOrderId = orderId,
                LastCustomerMessageAt = DateTime.UtcNow
            };
            _db.SupportTickets.Add(ticket);
            created = true;
        }
        else
        {
            ticket.LastCustomerMessageAt = DateTime.UtcNow;
            ticket.UpdatedAt = DateTime.UtcNow;
            if (ticket.Status == SupportTicketStatuses.WaitingForCustomer)
                ticket.Status = SupportTicketStatuses.Open;
            if (orderId is Guid oid)
                ticket.RelatedOrderId = oid;
        }

        var inbound = new SupportMessage
        {
            TicketId = ticket.Id,
            Channel = channel,
            Direction = SupportMessageDirections.Inbound,
            Body = SanitizeCustomerText(request.Body),
            AuthorType = "Customer",
            AuthorId = customerId?.ToString(),
            DeliveryStatus = SupportDeliveryStatuses.Delivered,
            ExternalMessageId = request.ExternalMessageId,
            IdempotencyKey = idempotency,
            DeliveredAt = DateTime.UtcNow
        };
        // Ensure ticket Id is available before message FK for in-memory
        if (ticket.Id == Guid.Empty)
            ticket.Id = Guid.NewGuid();
        inbound.TicketId = ticket.Id;
        _db.SupportMessages.Add(inbound);

        await SyncChannelInboundAsync(channel, cancellationToken);
        await MirrorWorkItemAsync(ticket, "SupportTicket", ticket.Subject, ticket.Description, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        if (_events is not null && created)
            await _events.EnqueueAsync(NalvaEventNames.SupportTicketCreated, $"{{\"ticketId\":\"{ticket.Id}\"}}", $"SupportTicketCreated:{ticket.Id:N}", cancellationToken);

        if (!request.ProcessWithAgent || ticket.HumanTakeover)
        {
            return new SupportProcessResult(
                ticket.Id,
                ticket.TicketNumber,
                ticket.Status,
                ticket.HumanTakeover ? "Human takeover is active. AI did not reply." : "Message stored without autonomous processing.",
                false,
                SupportDeliveryStatuses.Skipped,
                false,
                null,
                new JsonObject { ["stored"] = true });
        }

        return await AutonomouslyHandleAsync(ticket, request.Body, customerFound, context, cancellationToken);
    }

    public async Task<SupportTicketDetailDto> UpdateTicketAsync(Guid ticketId, SupportTicketUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var ticket = await RequireTicketAsync(ticketId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.Status) && SupportTicketStatuses.All.Contains(request.Status))
            ticket.Status = request.Status!;
        if (!string.IsNullOrWhiteSpace(request.Priority))
            ticket.Priority = request.Priority!;
        if (!string.IsNullOrWhiteSpace(request.Category))
            ticket.Category = request.Category!;
        if (request.AssignedUserId is not null)
            ticket.AssignedUserId = request.AssignedUserId;
        if (request.AssignedAgentKey is not null)
            ticket.AssignedAgentKey = request.AssignedAgentKey;
        if (request.AiSummary is not null)
            ticket.AiSummary = Clip(request.AiSummary, 2000);
        if (request.RecommendedAction is not null)
            ticket.RecommendedAction = Clip(request.RecommendedAction, 1000);
        if (request.RelatedOrderId is Guid orderId)
        {
            var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
            if (order is null || order.TenantId != CompanyId)
                throw new NotFoundException("Order was not found in this company.");
            if (ticket.CustomerId is Guid customerId && order.CustomerId != customerId)
                throw new BadRequestException("Order does not belong to this customer.");
            ticket.RelatedOrderId = orderId;
        }

        ticket.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return (await GetTicketAsync(ticket.Id, cancellationToken))!;
    }

    public async Task<SupportMessageDto> AddReplyAsync(Guid ticketId, SupportReplyRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            throw new BadRequestException("Reply body is required.");

        var ticket = await RequireTicketAsync(ticketId, cancellationToken);
        var channel = NormalizeChannel(request.Channel ?? ticket.Channel);
        var message = new SupportMessage
        {
            TicketId = ticket.Id,
            Channel = channel,
            Direction = request.IsInternalNote ? SupportMessageDirections.Internal : SupportMessageDirections.Outbound,
            Body = Clip(request.Body.Trim(), 8000),
            AuthorType = request.IsInternalNote ? "Employee" : "Agent",
            AuthorId = _user.UserId,
            IsInternalNote = request.IsInternalNote,
            DeliveryStatus = SupportDeliveryStatuses.Pending
        };

        if (request.IsInternalNote || !request.SendToCustomer)
        {
            message.DeliveryStatus = SupportDeliveryStatuses.Skipped;
            message.Direction = SupportMessageDirections.Internal;
            message.IsInternalNote = true;
        }
        else
        {
            var detail = await MapDetailAsync(ticket, cancellationToken);
            var send = await _channels.Resolve(channel).SendAsync(detail, message.Body, cancellationToken);
            message.DeliveryStatus = send.DeliveryStatus;
            message.ExternalMessageId = send.ExternalMessageId;
            message.Error = send.Error;
            message.DeliveredAt = send.Performed ? DateTime.UtcNow : null;
            if (send.Performed)
            {
                ticket.LastAgentMessageAt = DateTime.UtcNow;
                if (ticket.Status is SupportTicketStatuses.New or SupportTicketStatuses.Open)
                    ticket.Status = SupportTicketStatuses.WaitingForCustomer;
            }
        }

        ticket.UpdatedAt = DateTime.UtcNow;
        _db.SupportMessages.Add(message);
        await MirrorWorkItemAsync(ticket, "Response", "Customer reply", message.Body, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return MapMessage(message);
    }

    public async Task<SupportTicketDetailDto> EscalateAsync(Guid ticketId, SupportEscalateRequest request, CancellationToken cancellationToken = default)
    {
        var ticket = await RequireTicketAsync(ticketId, cancellationToken);
        ticket.Status = SupportTicketStatuses.Escalated;
        ticket.EscalationReason = Clip(string.IsNullOrWhiteSpace(request.Reason) ? "Escalated by support agent." : request.Reason, 500);
        ticket.EscalationSummary = Clip(request.Summary ?? ticket.AiSummary ?? ticket.Description, 2000);
        ticket.RecommendedAction = Clip(request.RecommendedAction ?? "Review the conversation and decide the next action.", 1000);
        ticket.Priority = string.IsNullOrWhiteSpace(request.Priority) ? SupportPriorities.High : request.Priority;
        ticket.EscalatedAt = DateTime.UtcNow;
        ticket.UpdatedAt = DateTime.UtcNow;
        ticket.AssignedAgentKey ??= SupportAgentKeys.Support;

        _db.SupportMessages.Add(new SupportMessage
        {
            TicketId = ticket.Id,
            Channel = ticket.Channel,
            Direction = SupportMessageDirections.Internal,
            Body = $"Escalated: {ticket.EscalationReason}. Recommended: {ticket.RecommendedAction}",
            AuthorType = "Agent",
            AuthorId = SupportAgentKeys.Support,
            IsInternalNote = true,
            DeliveryStatus = SupportDeliveryStatuses.Skipped
        });

        await MirrorWorkItemAsync(ticket, "SupportTicket", $"Escalated {ticket.TicketNumber}", ticket.EscalationSummary ?? ticket.Description, cancellationToken, "Escalated");
        await _db.SaveChangesAsync(cancellationToken);

        var notifyUser = string.IsNullOrWhiteSpace(request.NotifyUserId) ? _user.UserId : request.NotifyUserId;
        if (_notifications is not null && !string.IsNullOrWhiteSpace(notifyUser))
        {
            await _notifications.CreateNotificationAsync(new CreateNotificationRequest(
                notifyUser!,
                $"Support escalation {ticket.TicketNumber}",
                ticket.EscalationSummary ?? ticket.Subject,
                "SupportEscalation",
                $"/nalva/support?ticket={ticket.Id}"), cancellationToken);
        }

        return (await GetTicketAsync(ticket.Id, cancellationToken))!;
    }

    public async Task<SupportTicketDetailDto> ResolveAsync(Guid ticketId, SupportResolveRequest request, CancellationToken cancellationToken = default)
    {
        var ticket = await RequireTicketAsync(ticketId, cancellationToken);
        ticket.Resolution = Clip(string.IsNullOrWhiteSpace(request.Resolution) ? "Resolved." : request.Resolution, 4000);
        ticket.Status = request.Close ? SupportTicketStatuses.Closed : SupportTicketStatuses.Resolved;
        ticket.ResolvedAt = DateTime.UtcNow;
        if (request.Close)
            ticket.ClosedAt = DateTime.UtcNow;
        ticket.UpdatedAt = DateTime.UtcNow;

        _db.SupportMessages.Add(new SupportMessage
        {
            TicketId = ticket.Id,
            Channel = ticket.Channel,
            Direction = SupportMessageDirections.Internal,
            Body = $"Resolution: {ticket.Resolution}",
            AuthorType = "Agent",
            AuthorId = SupportAgentKeys.Support,
            IsInternalNote = true,
            DeliveryStatus = SupportDeliveryStatuses.Skipped
        });

        await _db.SaveChangesAsync(cancellationToken);

        if (request.ScheduleFollowUp)
        {
            await ScheduleFollowUpAsync(ticket.Id, new SupportFollowUpRequest
            {
                Kind = "ResolutionCheckIn",
                DueAt = DateTime.UtcNow.AddHours(Math.Clamp(request.FollowUpHours, 1, 168)),
                MessageTemplate = "Checking in: was your issue resolved? Reply if you still need help.",
                Channel = ticket.Channel,
                DedupKey = $"resolve-checkin:{ticket.Id:N}"
            }, cancellationToken);
        }

        return (await GetTicketAsync(ticket.Id, cancellationToken))!;
    }

    public async Task<SupportTicketDetailDto> ReopenAsync(Guid ticketId, string? note = null, CancellationToken cancellationToken = default)
    {
        var ticket = await RequireTicketAsync(ticketId, cancellationToken);
        ticket.Status = SupportTicketStatuses.Open;
        ticket.ReopenCount += 1;
        ticket.ResolvedAt = null;
        ticket.ClosedAt = null;
        ticket.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(note))
        {
            _db.SupportMessages.Add(new SupportMessage
            {
                TicketId = ticket.Id,
                Channel = ticket.Channel,
                Direction = SupportMessageDirections.Internal,
                Body = Clip(note, 2000),
                AuthorType = "Employee",
                AuthorId = _user.UserId,
                IsInternalNote = true,
                DeliveryStatus = SupportDeliveryStatuses.Skipped
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (await GetTicketAsync(ticket.Id, cancellationToken))!;
    }

    public async Task<SupportTicketDetailDto> TakeoverAsync(Guid ticketId, SupportTakeoverRequest request, CancellationToken cancellationToken = default)
    {
        var ticket = await RequireTicketAsync(ticketId, cancellationToken);
        ticket.HumanTakeover = request.Enabled;
        ticket.AssignedUserId = request.Enabled ? _user.UserId : ticket.AssignedUserId;
        ticket.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            _db.SupportMessages.Add(new SupportMessage
            {
                TicketId = ticket.Id,
                Channel = ticket.Channel,
                Direction = SupportMessageDirections.Internal,
                Body = Clip(request.Note, 2000),
                AuthorType = "Employee",
                AuthorId = _user.UserId,
                IsInternalNote = true,
                DeliveryStatus = SupportDeliveryStatuses.Skipped
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (await GetTicketAsync(ticket.Id, cancellationToken))!;
    }

    public async Task<SupportFollowUpDto> ScheduleFollowUpAsync(Guid ticketId, SupportFollowUpRequest request, CancellationToken cancellationToken = default)
    {
        var ticket = await RequireTicketAsync(ticketId, cancellationToken);
        var dedup = string.IsNullOrWhiteSpace(request.DedupKey) ? null : request.DedupKey.Trim();
        if (!string.IsNullOrWhiteSpace(dedup))
        {
            var existing = await _db.SupportFollowUpJobs.AsNoTracking()
                .FirstOrDefaultAsync(x => x.DedupKey == dedup && x.Status == SupportFollowUpStatuses.Scheduled, cancellationToken);
            if (existing is not null)
                return MapFollowUp(existing);
        }

        var job = new SupportFollowUpJob
        {
            TicketId = ticket.Id,
            Kind = string.IsNullOrWhiteSpace(request.Kind) ? "CustomerCheckIn" : request.Kind.Trim(),
            Status = SupportFollowUpStatuses.Scheduled,
            DueAt = request.DueAt ?? DateTime.UtcNow.AddHours(24),
            Channel = NormalizeChannel(request.Channel ?? ticket.Channel),
            MessageTemplate = Clip(string.IsNullOrWhiteSpace(request.MessageTemplate)
                ? "We wanted to check whether your issue was resolved."
                : request.MessageTemplate, 2000),
            DedupKey = dedup,
            RespectBusinessHours = request.RespectBusinessHours
        };
        _db.SupportFollowUpJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);
        return MapFollowUp(job);
    }

    public async Task<int> ProcessDueFollowUpsAsync(CancellationToken cancellationToken = default)
    {
        var due = await _db.SupportFollowUpJobs
            .Where(x => x.Status == SupportFollowUpStatuses.Scheduled && x.DueAt <= DateTime.UtcNow)
            .OrderBy(x => x.DueAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var job in due)
        {
            var ticket = await _db.SupportTickets.FirstOrDefaultAsync(x => x.Id == job.TicketId, cancellationToken);
            if (ticket is null)
            {
                job.Status = SupportFollowUpStatuses.Cancelled;
                job.LastError = "Ticket missing.";
                continue;
            }

            if (job.RespectBusinessHours && !IsBusinessHours(DateTime.UtcNow))
                continue;

            if (ticket.Status is SupportTicketStatuses.Closed)
            {
                job.Status = SupportFollowUpStatuses.Skipped;
                job.LastError = "Ticket already closed.";
                continue;
            }

            var detail = await MapDetailAsync(ticket, cancellationToken);
            var send = await _channels.Resolve(job.Channel).SendAsync(detail, job.MessageTemplate, cancellationToken);
            var message = new SupportMessage
            {
                TicketId = ticket.Id,
                Channel = job.Channel,
                Direction = SupportMessageDirections.Outbound,
                Body = job.MessageTemplate,
                AuthorType = "Agent",
                AuthorId = SupportAgentKeys.Support,
                DeliveryStatus = send.DeliveryStatus,
                ExternalMessageId = send.ExternalMessageId,
                Error = send.Error,
                DeliveredAt = send.Performed ? DateTime.UtcNow : null
            };
            _db.SupportMessages.Add(message);

            if (send.Performed)
            {
                job.Status = SupportFollowUpStatuses.Sent;
                job.SentAt = DateTime.UtcNow;
                ticket.LastAgentMessageAt = DateTime.UtcNow;
                ticket.Status = SupportTicketStatuses.WaitingForCustomer;
                ticket.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                job.Status = SupportFollowUpStatuses.Failed;
                job.LastError = send.Error ?? send.Message;
            }

            processed++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return processed;
    }

    public async Task<JsonObject> SearchKnowledgeAsync(string? query, CancellationToken cancellationToken = default)
    {
        var rows = await _db.NalvaKnowledgeSources.AsNoTracking()
            .Where(x => x.AgentKey == null || x.AgentKey == SupportAgentKeys.Support)
            .ToListAsync(cancellationToken);

        var tokens = string.IsNullOrWhiteSpace(query)
            ? []
            : Regex.Split(query.ToLowerInvariant(), @"[^a-z0-9]+").Where(x => x.Length > 2).Distinct().ToArray();

        IEnumerable<NalvaKnowledgeSource> ranked = rows.Where(x => x.TenantId == CompanyId);
        if (tokens.Length > 0)
        {
            ranked = ranked
                .Select(row =>
                {
                    var haystack = $"{row.Title} {row.Content}".ToLowerInvariant();
                    var score = tokens.Count(token => haystack.Contains(token, StringComparison.Ordinal));
                    return (row, score);
                })
                .Where(x => x.score > 0)
                .OrderByDescending(x => x.score)
                .ThenByDescending(x => x.row.UpdatedAt ?? x.row.CreatedAt)
                .Select(x => x.row);
        }
        else
        {
            ranked = ranked.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt);
        }

        var list = new JsonArray();
        foreach (var row in ranked.Take(5))
        {
            list.Add(new JsonObject
            {
                ["id"] = row.Id.ToString(),
                ["title"] = row.Title,
                ["agentKey"] = row.AgentKey,
                ["excerpt"] = Clip(row.Content, 240),
                ["source"] = "authorized-knowledge",
                ["stale"] = row.UpdatedAt is DateTime updated && updated < DateTime.UtcNow.AddDays(-180)
            });
        }

        return new JsonObject
        {
            ["sources"] = list,
            ["authorizedOnly"] = true,
            ["confidence"] = list.Count == 0 ? "none" : "authorized",
            ["invented"] = false
        };
    }

    public async Task<JsonObject> GetAuthorizedCustomerContextAsync(
        Guid? customerId,
        string? email,
        string? phone,
        Guid? orderId,
        string? orderNumber,
        CancellationToken cancellationToken = default)
    {
        Customer? customer = null;
        if (customerId is Guid cid)
            customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cid, cancellationToken);
        if (customer is null && !string.IsNullOrWhiteSpace(email))
            customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Email == email.Trim(), cancellationToken);
        if (customer is null && !string.IsNullOrWhiteSpace(phone))
            customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.PhoneNumber == phone.Trim(), cancellationToken);

        if (customer is not null && customer.TenantId != CompanyId)
            customer = null;

        Order? order = null;
        if (orderId is Guid oid)
            order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == oid, cancellationToken);
        if (order is null && !string.IsNullOrWhiteSpace(orderNumber))
            order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.OrderNumber == orderNumber.Trim(), cancellationToken);

        if (order is not null && order.TenantId != CompanyId)
            order = null;

        if (order is not null && customer is not null && order.CustomerId != customer.Id)
            order = null;

        if (order is not null && customer is null)
            customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == order.CustomerId, cancellationToken);

        var openTickets = customer is null
            ? 0
            : await _db.SupportTickets.AsNoTracking().CountAsync(x =>
                x.CustomerId == customer.Id &&
                x.Status != SupportTicketStatuses.Closed &&
                x.Status != SupportTicketStatuses.Resolved, cancellationToken);

        var orderCount = customer is null
            ? 0
            : await _db.Orders.AsNoTracking().CountAsync(x => x.CustomerId == customer.Id, cancellationToken);

        return new JsonObject
        {
            ["customerFound"] = customer is not null,
            ["customerId"] = customer?.Id,
            ["customerName"] = customer is null ? null : $"{customer.FirstName} {customer.LastName}".Trim(),
            ["customerEmail"] = customer?.Email,
            ["orderFound"] = order is not null,
            ["orderId"] = order?.Id,
            ["orderNumber"] = order?.OrderNumber,
            ["orderStatus"] = order?.Status,
            ["paymentStatus"] = order?.PaymentStatus,
            ["shippingAddress"] = order?.ShippingAddress,
            ["openTickets"] = openTickets,
            ["orderCount"] = orderCount,
            ["authorized"] = true
        };
    }

    private async Task<SupportProcessResult> AutonomouslyHandleAsync(
        SupportTicket ticket,
        string rawBody,
        bool customerFound,
        JsonObject context,
        CancellationToken cancellationToken)
    {
        var knowledge = await SearchKnowledgeAsync(ExtractQuery(rawBody), cancellationToken);
        var sources = knowledge["sources"] as JsonArray ?? [];
        var escalate = ShouldEscalate(rawBody, sources.Count == 0 && LooksLikePolicyQuestion(rawBody));

        ticket.AiSummary = Clip($"Customer asked: {rawBody}", 2000);
        ticket.Category = Classify(rawBody);
        ticket.Priority = Prioritize(rawBody);
        ticket.AssignedAgentKey = SupportAgentKeys.Support;
        ticket.UpdatedAt = DateTime.UtcNow;

        if (escalate)
        {
            var reason = HumanRequest.IsMatch(rawBody) ? "Customer requested a human."
                : RefundWords.IsMatch(rawBody) ? "Refund or financial decision required."
                : CancelWords.IsMatch(rawBody) ? "Cancellation requires approval."
                : AngryWords.IsMatch(rawBody) ? "Customer appears highly dissatisfied or legally sensitive."
                : "Insufficient verified knowledge or confidence.";

            await EscalateAsync(ticket.Id, new SupportEscalateRequest
            {
                Reason = reason,
                Summary = ticket.AiSummary,
                RecommendedAction = RefundWords.IsMatch(rawBody)
                    ? "Review refund eligibility and approve or reject IssueRefund."
                    : "Owner or support employee should continue the conversation.",
                Priority = ticket.Priority,
                NotifyUserId = _user.UserId
            }, cancellationToken);

            var hold = "Thanks for reaching out. I've escalated this to a teammate who will follow up shortly.";
            // External messaging channels require human approval before delivery (Phase 8C).
            var holdSend = !RequiresExternalApproval(ticket.Channel);
            var holdReply = await AddReplyAsync(ticket.Id, new SupportReplyRequest { Body = hold, SendToCustomer = holdSend, Channel = ticket.Channel }, cancellationToken);
            return new SupportProcessResult(
                ticket.Id,
                ticket.TicketNumber,
                SupportTicketStatuses.Escalated,
                reason,
                holdReply.DeliveryStatus is SupportDeliveryStatuses.Sent or SupportDeliveryStatuses.Delivered or SupportDeliveryStatuses.Simulated,
                holdReply.DeliveryStatus,
                true,
                holdReply.Id,
                new JsonObject { ["escalated"] = true, ["reason"] = reason, ["awaitingApproval"] = !holdSend });
        }

        var reply = BuildAuthorizedReply(rawBody, context, knowledge, customerFound);
        var externalApproval = RequiresExternalApproval(ticket.Channel);
        ticket.RecommendedAction = externalApproval
            ? "Approve SendSupportReply / SendWhatsAppMessage before the customer receives this draft."
            : sources.Count == 0 && !customerFound
                ? "Ask clarifying question or add knowledge."
                : "Monitor for customer confirmation.";
        ticket.Status = SupportTicketStatuses.InProgress;
        await _db.SaveChangesAsync(cancellationToken);

        var sent = await AddReplyAsync(ticket.Id, new SupportReplyRequest
        {
            Body = reply,
            SendToCustomer = !externalApproval,
            Channel = ticket.Channel
        }, cancellationToken);

        if (LooksLikeRoutineResolved(rawBody, context, sources.Count > 0))
        {
            await ResolveAsync(ticket.Id, new SupportResolveRequest
            {
                Resolution = "Routine inquiry answered from authorized records/knowledge.",
                Close = false,
                ScheduleFollowUp = true,
                FollowUpHours = 24
            }, cancellationToken);
        }

        var latest = await _db.SupportTickets.AsNoTracking().FirstAsync(x => x.Id == ticket.Id, cancellationToken);
        return new SupportProcessResult(
            latest.Id,
            latest.TicketNumber,
            latest.Status,
            "Support agent handled the inbound message using authorized data only.",
            sent.DeliveryStatus is SupportDeliveryStatuses.Sent or SupportDeliveryStatuses.Delivered or SupportDeliveryStatuses.Simulated,
            sent.DeliveryStatus,
            false,
            sent.Id,
            new JsonObject
            {
                ["knowledgeHits"] = sources.Count,
                ["customerFound"] = customerFound,
                ["orderFound"] = ReadBool(context, "orderFound")
            });
    }

    private static bool RequiresExternalApproval(string channel) =>
        string.Equals(channel, SupportChannels.WhatsApp, StringComparison.OrdinalIgnoreCase)
        || string.Equals(channel, SupportChannels.Email, StringComparison.OrdinalIgnoreCase);

    private async Task AttachProductMatchAsync(JsonObject context, string body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Length < 3)
            return;

        var products = await _db.Products.AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.Id, x.Name, x.SKU, x.CurrentStock })
            .Take(200)
            .ToListAsync(cancellationToken);
        var match = products
            .Where(p => !string.IsNullOrWhiteSpace(p.Name) && body.Contains(p.Name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.Name.Length)
            .FirstOrDefault();
        if (match is null)
            return;

        context["productMatch"] = new JsonObject
        {
            ["productId"] = match.Id.ToString(),
            ["name"] = match.Name,
            ["sku"] = match.SKU,
            ["currentStock"] = match.CurrentStock
        };
    }

    private static string BuildAuthorizedReply(string rawBody, JsonObject context, JsonObject knowledge, bool customerFound)
    {
        var sources = knowledge["sources"] as JsonArray ?? [];
        if (context["productMatch"] is JsonObject product)
        {
            var name = product["name"]?.GetValue<string>() ?? "the product";
            var stock = product["currentStock"]?.GetValue<decimal>() ?? 0;
            return stock > 0
                ? $"Yes — {name} is available. Current stock on hand is {stock}."
                : $"{name} is in our catalog but currently shows 0 stock. I can check a restock timeline with the team.";
        }
        if (ReadBool(context, "orderFound"))
        {
            return $"I found order {context["orderNumber"]} with status {context["orderStatus"]} (payment: {context["paymentStatus"]}). " +
                   (string.IsNullOrWhiteSpace(context["shippingAddress"]?.ToString())
                       ? "No shipping address is on file."
                       : $"Shipping address on file: {context["shippingAddress"]}.");
        }

        if (sources.Count > 0)
        {
            var first = sources[0] as JsonObject;
            var title = first?["title"]?.ToString() ?? "our records";
            var excerpt = first?["excerpt"]?.ToString() ?? "";
            return $"Based on verified business knowledge ({title}): {excerpt}";
        }

        if (!customerFound && LooksLikeOrderQuestion(rawBody))
            return "I can help with that order update. Please share the order number and the email on the account so I can verify identity.";

        if (sources.Count == 0)
            return "I do not have verified information for that yet, so I will not invent a policy or promise. Could you share a bit more detail, or I can escalate to a teammate?";

        return "Thanks for contacting support. I am reviewing your request with the information on file.";
    }

    private static bool ShouldEscalate(string body, bool missingKnowledge) =>
        HumanRequest.IsMatch(body) || RefundWords.IsMatch(body) || CancelWords.IsMatch(body) || AngryWords.IsMatch(body) || missingKnowledge;

    private static bool LooksLikePolicyQuestion(string body) =>
        Regex.IsMatch(body, @"\b(return|refund|warranty|shipping|policy|price|pricing|hours|troubleshoot)\b", RegexOptions.IgnoreCase);

    private static bool LooksLikeOrderQuestion(string body) =>
        Regex.IsMatch(body, @"\b(order|tracking|delivery|shipped|package)\b", RegexOptions.IgnoreCase);

    private static bool LooksLikeRoutineResolved(string body, JsonObject context, bool hasKnowledge) =>
        (ReadBool(context, "orderFound") && LooksLikeOrderQuestion(body))
        || (hasKnowledge && !RefundWords.IsMatch(body) && !CancelWords.IsMatch(body) && !HumanRequest.IsMatch(body));

    private static string Classify(string body)
    {
        if (RefundWords.IsMatch(body) || Regex.IsMatch(body, @"\breturn\b", RegexOptions.IgnoreCase)) return "Returns";
        if (LooksLikeOrderQuestion(body)) return "Orders";
        if (Regex.IsMatch(body, @"\b(price|pricing|cost)\b", RegexOptions.IgnoreCase)) return "Billing";
        if (Regex.IsMatch(body, @"\b(bug|error|not working|broken)\b", RegexOptions.IgnoreCase)) return "Technical";
        if (AngryWords.IsMatch(body)) return "Complaint";
        return "General";
    }

    private static string Prioritize(string body)
    {
        if (AngryWords.IsMatch(body) || HumanRequest.IsMatch(body)) return SupportPriorities.Urgent;
        if (RefundWords.IsMatch(body) || CancelWords.IsMatch(body)) return SupportPriorities.High;
        if (LooksLikeOrderQuestion(body)) return SupportPriorities.Normal;
        return SupportPriorities.Normal;
    }

    private static string ExtractQuery(string body)
    {
        var words = Regex.Split(body.ToLowerInvariant(), @"[^a-z0-9]+")
            .Where(x => x.Length > 3)
            .Where(x => x is not ("about" or "please" or "could" or "would" or "their" or "there" or "have"))
            .Take(6);
        return string.Join(' ', words);
    }

    private async Task<string> NextTicketNumberAsync(CancellationToken ct)
    {
        var count = await _db.SupportTickets.IgnoreQueryFilters().CountAsync(x => x.TenantId == CompanyId, ct);
        return $"ST-{DateTime.UtcNow:yyyyMMdd}-{count + 1:D4}";
    }

    private async Task<SupportTicket> RequireTicketAsync(Guid ticketId, CancellationToken ct)
    {
        var ticket = await _db.SupportTickets.FirstOrDefaultAsync(x => x.Id == ticketId, ct);
        if (ticket is null || ticket.TenantId != CompanyId)
            throw new NotFoundException("Support ticket was not found in this company.");
        return ticket;
    }

    private async Task<SupportTicketDetailDto> MapDetailAsync(SupportTicket ticket, CancellationToken ct)
    {
        Customer? customer = null;
        if (ticket.CustomerId is Guid customerId)
            customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customerId, ct);

        Order? order = null;
        if (ticket.RelatedOrderId is Guid orderId)
            order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orderId, ct);

        var messages = await _db.SupportMessages.AsNoTracking()
            .Where(x => x.TicketId == ticket.Id)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);
        var followUps = await _db.SupportFollowUpJobs.AsNoTracking()
            .Where(x => x.TicketId == ticket.Id)
            .OrderByDescending(x => x.DueAt)
            .ToListAsync(ct);

        return new SupportTicketDetailDto(
            ticket.Id,
            ticket.TicketNumber,
            ticket.TenantId,
            ticket.CustomerId,
            customer is null ? null : $"{customer.FirstName} {customer.LastName}".Trim(),
            customer?.Email,
            ticket.Subject,
            ticket.Description,
            ticket.Category,
            ticket.Priority,
            ticket.Status,
            ticket.Channel,
            ticket.AssignedAgentKey,
            ticket.AssignedUserId,
            ticket.ExternalThreadId,
            ticket.AiSummary,
            ticket.RecommendedAction,
            ticket.Resolution,
            ticket.EscalationReason,
            ticket.EscalationSummary,
            ticket.RelatedOrderId,
            order?.OrderNumber,
            ticket.HumanTakeover,
            ticket.MarketingConsentForHandoff,
            ticket.ReopenCount,
            ticket.CreatedAt,
            ticket.UpdatedAt,
            ticket.EscalatedAt,
            ticket.ResolvedAt,
            ticket.ClosedAt,
            messages.Select(MapMessage).ToList(),
            followUps.Select(MapFollowUp).ToList());
    }

    private static SupportMessageDto MapMessage(SupportMessage message) =>
        new(message.Id, message.TicketId, message.Channel, message.Direction, message.Body, message.AuthorType, message.AuthorId,
            message.DeliveryStatus, message.IsInternalNote, message.CreatedAt, message.DeliveredAt, message.Error);

    private static SupportFollowUpDto MapFollowUp(SupportFollowUpJob job) =>
        new(job.Id, job.TicketId, job.Kind, job.Status, job.DueAt, job.Channel, job.MessageTemplate, job.SentAt, job.LastError);

    private async Task MirrorWorkItemAsync(SupportTicket ticket, string kind, string title, string body, CancellationToken ct, string? status = null)
    {
        _db.NalvaWorkItems.Add(new NalvaWorkItem
        {
            AgentKey = SupportAgentKeys.Support,
            Kind = kind,
            Title = Clip(title, 200),
            Body = Clip(body, 4000),
            Status = status ?? ticket.Status,
            RelatedEntityType = nameof(SupportTicket),
            RelatedEntityId = ticket.Id,
            ExecutionId = ticket.ExecutionId
        });
        await Task.CompletedTask;
    }

    private async Task SyncChannelInboundAsync(string channel, CancellationToken ct)
    {
        var account = await _db.SupportChannelAccounts.FirstOrDefaultAsync(x => x.Channel == channel, ct);
        if (account is null)
        {
            account = new SupportChannelAccount
            {
                Channel = channel,
                DisplayName = channel,
                Status = channel == SupportChannels.Sandbox
                    ? SupportChannelConnectionStatuses.Sandbox
                    : channel == SupportChannels.WebsiteChat
                        ? SupportChannelConnectionStatuses.Connected
                        : SupportChannelConnectionStatuses.Disconnected,
                AllowsInbound = true,
                AllowsOutbound = channel is SupportChannels.Sandbox or SupportChannels.WebsiteChat
            };
            _db.SupportChannelAccounts.Add(account);
        }

        account.LastInboundAt = DateTime.UtcNow;
        await Task.CompletedTask;
    }

    private static string NormalizeChannel(string? channel) =>
        string.IsNullOrWhiteSpace(channel) ? SupportChannels.Sandbox : channel.Trim().ToLowerInvariant();

    private static string SanitizeCustomerText(string body) =>
        Clip(body.Replace('\0', ' ').Trim(), 8000);

    private static string Clip(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static bool IsBusinessHours(DateTime utc)
    {
        // Simple UTC weekday 09:00-18:00 window; follow-ups outside the window wait.
        if (utc.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return false;
        return utc.Hour is >= 9 and < 18;
    }

    private static Guid? ReadGuid(JsonObject obj, string key)
    {
        if (!obj.TryGetPropertyValue(key, out var node) || node is null || node.GetValueKind() == System.Text.Json.JsonValueKind.Null)
            return null;
        if (node is JsonValue value && value.TryGetValue<Guid>(out var guid))
            return guid;
        return Guid.TryParse(node.ToString(), out var parsed) ? parsed : null;
    }

    private static bool ReadBool(JsonObject obj, string key)
    {
        if (!obj.TryGetPropertyValue(key, out var node) || node is null)
            return false;
        if (node is JsonValue value && value.TryGetValue<bool>(out var flag))
            return flag;
        return bool.TryParse(node.ToString(), out var parsed) && parsed;
    }
}
