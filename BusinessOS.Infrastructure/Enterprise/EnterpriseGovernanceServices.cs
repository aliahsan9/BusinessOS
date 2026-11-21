using System.Text;
using System.Text.Json;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Enterprise;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Enterprise;

public sealed class ApprovalGovernanceService : IApprovalGovernanceService
{
    private readonly BusinessOSDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly ICurrentUserService _currentUser;
    private readonly IEnterpriseEntitlementGuard _guard;
    private readonly IEnterpriseAuditService _audit;

    public ApprovalGovernanceService(
        BusinessOSDbContext db,
        ITenantProvider tenant,
        ICurrentUserService currentUser,
        IEnterpriseEntitlementGuard guard,
        IEnterpriseAuditService audit)
    {
        _db = db;
        _tenant = tenant;
        _currentUser = currentUser;
        _guard = guard;
        _audit = audit;
    }

    public async Task<IReadOnlyList<ApprovalPolicyDto>> GetPoliciesAsync(CancellationToken cancellationToken = default)
    {
        await _guard.EnsureAdvancedSecurityAsync(cancellationToken);
        var policies = await _db.ApprovalPolicies.AsNoTracking()
            .Include(x => x.Steps)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return policies.Select(MapPolicy).ToList();
    }

    public async Task<ApprovalPolicyDto> SavePolicyAsync(Guid? id, ApprovalPolicyWriteRequest request, CancellationToken cancellationToken = default)
    {
        await _guard.EnsureAdvancedSecurityAsync(cancellationToken);
        if (request.Steps.Count == 0)
            throw new BadRequestException("At least one approval step is required.");

        ApprovalPolicy policy;
        if (id is Guid existingId)
        {
            policy = await _db.ApprovalPolicies.Include(x => x.Steps)
                .FirstOrDefaultAsync(x => x.Id == existingId, cancellationToken)
                ?? throw new NotFoundException("Approval policy was not found.");
            _db.ApprovalPolicySteps.RemoveRange(policy.Steps);
            policy.Steps.Clear();
        }
        else
        {
            policy = new ApprovalPolicy { TenantId = _tenant.TenantId };
            _db.ApprovalPolicies.Add(policy);
        }

        policy.Name = request.Name.Trim();
        policy.Description = request.Description;
        policy.TriggerType = request.TriggerType.Trim();
        policy.AmountThreshold = request.AmountThreshold;
        policy.Mode = string.Equals(request.Mode, "Parallel", StringComparison.OrdinalIgnoreCase) ? "Parallel" : "Sequential";
        policy.DeadlineHours = Math.Clamp(request.DeadlineHours, 1, 720);
        policy.EscalateToUserId = request.EscalateToUserId;
        policy.EscalateToRoleName = request.EscalateToRoleName;
        policy.IsActive = request.IsActive;
        policy.BranchId = request.BranchId;
        policy.DepartmentId = request.DepartmentId;
        policy.ToolName = request.ToolName;
        policy.UpdatedAt = DateTime.UtcNow;

        foreach (var step in request.Steps.OrderBy(x => x.StepOrder))
        {
            policy.Steps.Add(new ApprovalPolicyStep
            {
                TenantId = _tenant.TenantId,
                StepOrder = step.StepOrder,
                ApproverType = step.ApproverType,
                ApproverRoleName = step.ApproverRoleName,
                ApproverPermissionCode = step.ApproverPermissionCode,
                ApproverUserId = step.ApproverUserId,
                RequireDepartmentHead = step.RequireDepartmentHead
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(new EnterpriseAuditWriteRequest
        {
            Category = EnterpriseAuditCategories.Approval,
            Action = id is null ? "ApprovalPolicyCreated" : "ApprovalPolicyUpdated",
            ActorUserId = _currentUser.UserId,
            ActorType = "User",
            EntityType = nameof(ApprovalPolicy),
            EntityId = policy.Id.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { policy.Name, policy.TriggerType, policy.Mode })
        }, cancellationToken);

        return MapPolicy(policy);
    }

    public async Task<ApprovalRequestDto?> EvaluateAndEnqueueAsync(ApprovalEvaluateRequest request, CancellationToken cancellationToken = default)
    {
        // Fail closed for agent/tool paths even when AdvancedSecurity is off if a matching Nalva approval exists.
        var policies = await _db.ApprovalPolicies.AsNoTracking()
            .Include(x => x.Steps)
            .Where(x => x.IsActive && x.TriggerType == request.TriggerType)
            .ToListAsync(cancellationToken);

        if (policies.Count == 0)
            return null;

        var policy = policies
            .Where(x => x.BranchId is null || x.BranchId == request.BranchId)
            .Where(x => x.DepartmentId is null || x.DepartmentId == request.DepartmentId)
            .Where(x => string.IsNullOrWhiteSpace(x.ToolName) || string.Equals(x.ToolName, request.ToolName, StringComparison.OrdinalIgnoreCase))
            .Where(x => x.AmountThreshold is null || (request.Amount ?? 0) >= x.AmountThreshold)
            .OrderByDescending(x => x.AmountThreshold ?? 0)
            .FirstOrDefault();

        if (policy is null)
            return null;

        if (policies.Count > 0)
            await _guard.EnsureAdvancedSecurityAsync(cancellationToken);

        var entity = new ApprovalRequest
        {
            TenantId = _tenant.TenantId,
            ApprovalPolicyId = policy.Id,
            TriggerType = request.TriggerType,
            SubjectType = request.SubjectType,
            SubjectId = request.SubjectId,
            Title = request.Title,
            PayloadJson = request.PayloadJson,
            Amount = request.Amount,
            Status = "Pending",
            Mode = policy.Mode,
            CurrentStepOrder = policy.Steps.Min(x => x.StepOrder),
            RequestedByUserId = _currentUser.UserId ?? "system",
            DeadlineAt = DateTime.UtcNow.AddHours(policy.DeadlineHours),
            BranchId = request.BranchId,
            DepartmentId = request.DepartmentId,
            NalvaApprovalId = request.NalvaApprovalId
        };
        _db.ApprovalRequests.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(new EnterpriseAuditWriteRequest
        {
            Category = EnterpriseAuditCategories.Approval,
            Action = "ApprovalRequested",
            ActorUserId = entity.RequestedByUserId,
            ActorType = entity.RequestedByUserId == "system" ? "Agent" : "User",
            EntityType = nameof(ApprovalRequest),
            EntityId = entity.Id.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { entity.TriggerType, entity.Title, entity.Amount })
        }, cancellationToken);

        return MapRequest(entity);
    }

    public async Task<IReadOnlyList<ApprovalRequestDto>> GetRequestsAsync(string? status = null, CancellationToken cancellationToken = default)
    {
        await _guard.EnsureAdvancedSecurityAsync(cancellationToken);
        var query = _db.ApprovalRequests.AsNoTracking().Include(x => x.Decisions).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);
        var items = await query.OrderByDescending(x => x.RequestedAt).Take(200).ToListAsync(cancellationToken);
        return items.Select(MapRequest).ToList();
    }

    public async Task<ApprovalRequestDto> DecideAsync(Guid requestId, ApprovalDecisionWriteRequest request, CancellationToken cancellationToken = default)
    {
        await _guard.EnsureAdvancedSecurityAsync(cancellationToken);
        var entity = await _db.ApprovalRequests.Include(x => x.Decisions)
            .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new NotFoundException("Approval request was not found.");

        if (entity.Status is not ("Pending" or "Escalated"))
            throw new BadRequestException("Only pending approval requests can be decided.");

        // Agents cannot approve.
        if (string.Equals(_currentUser.UserId, "agent", StringComparison.OrdinalIgnoreCase) ||
            _currentUser.Roles.Contains("Agent", StringComparer.OrdinalIgnoreCase))
            throw new ForbiddenException("AI agents cannot approve organizational requests.");

        if (!_currentUser.HasPermission(PermissionCodes.ApprovalDecide) &&
            !_currentUser.HasPermission(PermissionCodes.NalvaApprove) &&
            !_currentUser.Roles.Any(r => r is "Owner" or "Admin" or "Manager"))
            throw new ForbiddenException("You are not authorized to decide this approval.");

        var decision = new ApprovalDecision
        {
            TenantId = _tenant.TenantId,
            ApprovalRequestId = entity.Id,
            StepOrder = entity.CurrentStepOrder,
            Decision = request.Approve ? "Approved" : "Rejected",
            DecidedByUserId = _currentUser.UserId ?? throw new UnauthorizedException("Authentication required."),
            Note = request.Note,
            DecidedAt = DateTime.UtcNow
        };
        entity.Decisions.Add(decision);

        if (!request.Approve)
        {
            entity.Status = "Rejected";
            entity.RejectionReason = request.RejectionReason ?? request.Note;
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            await WriteDecisionAudit(entity, "Rejected");
            return MapRequest(entity);
        }

        var policy = entity.ApprovalPolicyId is Guid policyId
            ? await _db.ApprovalPolicies.AsNoTracking().Include(x => x.Steps).FirstOrDefaultAsync(x => x.Id == policyId, cancellationToken)
            : null;

        if (policy is null || string.Equals(policy.Mode, "Parallel", StringComparison.OrdinalIgnoreCase))
        {
            var required = policy?.Steps.Select(x => x.StepOrder).Distinct().Count() ?? 1;
            var approvedSteps = entity.Decisions.Count(x => x.Decision == "Approved");
            if (approvedSteps >= required)
                entity.Status = "Approved";
        }
        else
        {
            var next = policy.Steps.Where(x => x.StepOrder > entity.CurrentStepOrder).OrderBy(x => x.StepOrder).FirstOrDefault();
            if (next is null)
                entity.Status = "Approved";
            else
                entity.CurrentStepOrder = next.StepOrder;
        }

        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await WriteDecisionAudit(entity, entity.Status);
        return MapRequest(entity);
    }

    public async Task<ApprovalRequestDto> DelegateAsync(Guid requestId, ApprovalDelegateRequest request, CancellationToken cancellationToken = default)
    {
        await _guard.EnsureAdvancedSecurityAsync(cancellationToken);
        var entity = await _db.ApprovalRequests.Include(x => x.Decisions)
            .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new NotFoundException("Approval request was not found.");
        if (entity.Status != "Pending")
            throw new BadRequestException("Only pending requests can be delegated.");

        entity.Decisions.Add(new ApprovalDecision
        {
            TenantId = _tenant.TenantId,
            ApprovalRequestId = entity.Id,
            StepOrder = entity.CurrentStepOrder,
            Decision = "Delegated",
            DecidedByUserId = request.ToUserId,
            DelegatedFromUserId = _currentUser.UserId,
            Note = request.Note,
            DecidedAt = DateTime.UtcNow
        });
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await WriteDecisionAudit(entity, "Delegated");
        return MapRequest(entity);
    }

    public async Task EscalateOverdueAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var overdue = await _db.ApprovalRequests
            .Include(x => x.Decisions)
            .Where(x => x.Status == "Pending" && x.DeadlineAt != null && x.DeadlineAt < now)
            .ToListAsync(cancellationToken);

        foreach (var item in overdue)
        {
            var policy = item.ApprovalPolicyId is Guid id
                ? await _db.ApprovalPolicies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                : null;
            item.Status = "Escalated";
            item.EscalatedAt = now;
            item.EscalatedToUserId = policy?.EscalateToUserId;
            item.UpdatedAt = now;
        }

        if (overdue.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task WriteDecisionAudit(ApprovalRequest entity, string action) =>
        await _audit.LogAsync(new EnterpriseAuditWriteRequest
        {
            Category = EnterpriseAuditCategories.Approval,
            Action = action,
            ActorUserId = _currentUser.UserId,
            ActorType = "User",
            EntityType = nameof(ApprovalRequest),
            EntityId = entity.Id.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { entity.Status, entity.RejectionReason })
        });

    private static ApprovalPolicyDto MapPolicy(ApprovalPolicy policy) =>
        new(policy.Id, policy.Name, policy.Description, policy.TriggerType, policy.AmountThreshold, policy.Mode,
            policy.DeadlineHours, policy.EscalateToUserId, policy.EscalateToRoleName, policy.IsActive,
            policy.BranchId, policy.DepartmentId, policy.ToolName,
            policy.Steps.OrderBy(x => x.StepOrder)
                .Select(x => new ApprovalPolicyStepDto(x.StepOrder, x.ApproverType, x.ApproverRoleName, x.ApproverPermissionCode, x.ApproverUserId, x.RequireDepartmentHead))
                .ToList());

    private static ApprovalRequestDto MapRequest(ApprovalRequest request) =>
        new(request.Id, request.TriggerType, request.SubjectType, request.SubjectId, request.Title, request.Amount,
            request.Status, request.Mode, request.CurrentStepOrder, request.RequestedAt, request.RequestedByUserId,
            request.DeadlineAt, request.RejectionReason, request.NalvaApprovalId,
            request.Decisions.OrderBy(x => x.DecidedAt)
                .Select(x => new ApprovalDecisionDto(x.Id, x.StepOrder, x.Decision, x.DecidedByUserId, x.DelegatedFromUserId, x.Note, x.DecidedAt))
                .ToList());
}

public sealed class EnterpriseAuditService : IEnterpriseAuditService
{
    private readonly BusinessOSDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly IEnterpriseEntitlementGuard _guard;
    private readonly ICurrentUserService _currentUser;

    public EnterpriseAuditService(
        BusinessOSDbContext db,
        ITenantProvider tenant,
        IEnterpriseEntitlementGuard guard,
        ICurrentUserService currentUser)
    {
        _db = db;
        _tenant = tenant;
        _guard = guard;
        _currentUser = currentUser;
    }

    public async Task LogAsync(EnterpriseAuditWriteRequest request, CancellationToken cancellationToken = default)
    {
        _db.EnterpriseAuditEvents.Add(new EnterpriseAuditEvent
        {
            TenantId = _tenant.TenantId == Guid.Empty ? Guid.Empty : _tenant.TenantId,
            Category = request.Category,
            Action = request.Action,
            ActorUserId = request.ActorUserId ?? _currentUser.UserId,
            ActorType = request.ActorType ?? "User",
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            BranchId = request.BranchId,
            DepartmentId = request.DepartmentId,
            IpAddress = request.IpAddress,
            DetailsJson = request.DetailsJson,
            IsSensitive = request.IsSensitive,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedEnterpriseAuditDto> QueryAsync(EnterpriseAuditQuery query, CancellationToken cancellationToken = default)
    {
        await _guard.EnsureAdvancedSecurityAsync(cancellationToken);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var q = _db.EnterpriseAuditEvents.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Category))
            q = q.Where(x => x.Category == query.Category);
        if (!string.IsNullOrWhiteSpace(query.ActorUserId))
            q = q.Where(x => x.ActorUserId == query.ActorUserId);
        if (query.From is DateTime from)
            q = q.Where(x => x.CreatedAt >= from);
        if (query.To is DateTime to)
            q = q.Where(x => x.CreatedAt <= to);
        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(x => x.Action.Contains(query.Search) || (x.EntityType != null && x.EntityType.Contains(query.Search)));

        var total = await q.CountAsync(cancellationToken);
        var items = await q.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new EnterpriseAuditEventDto(x.Id, x.Category, x.Action, x.ActorUserId, x.ActorType, x.EntityType, x.EntityId, x.DetailsJson, x.IsSensitive, x.CreatedAt))
            .ToListAsync(cancellationToken);
        return new PagedEnterpriseAuditDto(items, total, page, pageSize);
    }

    public async Task<byte[]> ExportCsvAsync(EnterpriseAuditQuery query, CancellationToken cancellationToken = default)
    {
        await _guard.EnsureAdvancedSecurityAsync(cancellationToken);
        if (!_currentUser.HasPermission(PermissionCodes.AuditExport) &&
            !_currentUser.Roles.Any(r => r is "Owner" or "Admin"))
            throw new ForbiddenException("Audit export requires Audit.Export permission.");

        query.Page = 1;
        query.PageSize = 5000;
        var page = await QueryAsync(query, cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("Id,Category,Action,ActorUserId,ActorType,EntityType,EntityId,IsSensitive,CreatedAt");
        foreach (var row in page.Items)
        {
            sb.Append(row.Id).Append(',')
                .Append(Csv(row.Category)).Append(',')
                .Append(Csv(row.Action)).Append(',')
                .Append(Csv(row.ActorUserId)).Append(',')
                .Append(Csv(row.ActorType)).Append(',')
                .Append(Csv(row.EntityType)).Append(',')
                .Append(Csv(row.EntityId)).Append(',')
                .Append(row.IsSensitive).Append(',')
                .Append(row.CreatedAt.ToString("O"))
                .AppendLine();
        }

        await LogAsync(new EnterpriseAuditWriteRequest
        {
            Category = EnterpriseAuditCategories.DataExport,
            Action = "AuditExport",
            ActorUserId = _currentUser.UserId,
            ActorType = "User",
            DetailsJson = JsonSerializer.Serialize(new { query.Category, query.From, query.To, Count = page.Items.Count })
        }, cancellationToken);

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _db.TenantEnterpriseSecuritySettings.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var setting in settings)
        {
            var cutoff = DateTime.UtcNow.AddDays(-Math.Max(30, setting.AuditRetentionDays));
            var expired = await _db.EnterpriseAuditEvents.IgnoreQueryFilters()
                .Where(x => x.TenantId == setting.TenantId && x.CreatedAt < cutoff)
                .Take(5000)
                .ToListAsync(cancellationToken);
            if (expired.Count == 0)
                continue;
            _db.EnterpriseAuditEvents.RemoveRange(expired);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }
}

public sealed class EnterpriseReportingService : IEnterpriseReportingService
{
    private readonly BusinessOSDbContext _db;
    private readonly IEnterpriseEntitlementGuard _guard;
    private readonly IOrgScopeService _scope;
    private readonly ICurrentUserService _currentUser;

    public EnterpriseReportingService(
        BusinessOSDbContext db,
        IEnterpriseEntitlementGuard guard,
        IOrgScopeService scope,
        ICurrentUserService currentUser)
    {
        _db = db;
        _guard = guard;
        _scope = scope;
        _currentUser = currentUser;
    }

    public async Task<EnterpriseReportDto> GetBranchReportAsync(Guid? branchId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        await EnsureReportingAccess(cancellationToken);
        await _scope.EnsureCanAccessBranchAsync(branchId, cancellationToken);
        var (start, end) = Range(from, to);
        var scope = await _scope.GetCurrentScopeAsync(cancellationToken);

        var orders = await _db.Orders.AsNoTracking()
            .Where(x => x.OrderDate >= start && x.OrderDate <= end)
            .Where(x => branchId == null || x.BranchId == branchId)
            .ToListAsync(cancellationToken);
        orders = orders.Where(x => _scope.IsVisible(scope, x.BranchId, null)).ToList();

        var expenses = await _db.Expenses.AsNoTracking()
            .Where(x => x.ExpenseDate >= start && x.ExpenseDate <= end)
            .Where(x => branchId == null || x.BranchId == branchId)
            .ToListAsync(cancellationToken);
        expenses = expenses.Where(x => _scope.IsVisible(scope, x.BranchId, x.DepartmentId)).ToList();

        return new EnterpriseReportDto(
            "Branch performance",
            DateTime.UtcNow,
            [
                new("orders", "Orders", orders.Count),
                new("revenue", "Revenue", orders.Sum(x => x.GrandTotal), "money"),
                new("expenses", "Expenses", expenses.Sum(x => x.Amount), "money")
            ],
            orders.GroupBy(x => x.BranchId)
                .Select(g => new Dictionary<string, string>
                {
                    ["branchId"] = g.Key?.ToString() ?? "unassigned",
                    ["orders"] = g.Count().ToString(),
                    ["revenue"] = g.Sum(x => x.GrandTotal).ToString("0.00")
                } as IReadOnlyDictionary<string, string>)
                .ToList());
    }

    public async Task<EnterpriseReportDto> GetDepartmentReportAsync(Guid? departmentId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        await EnsureReportingAccess(cancellationToken);
        await _scope.EnsureCanAccessDepartmentAsync(departmentId, cancellationToken);
        var (start, end) = Range(from, to);
        var employees = await _db.Employees.AsNoTracking()
            .Where(x => departmentId == null || x.DepartmentId == departmentId)
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);
        var expenses = await _db.Expenses.AsNoTracking()
            .Where(x => x.ExpenseDate >= start && x.ExpenseDate <= end)
            .Where(x => departmentId == null || x.DepartmentId == departmentId)
            .ToListAsync(cancellationToken);

        return new EnterpriseReportDto(
            "Department analytics",
            DateTime.UtcNow,
            [
                new("employees", "Active employees", employees.Count),
                new("expenses", "Department expenses", expenses.Sum(x => x.Amount), "money")
            ],
            employees.Select(e => new Dictionary<string, string>
            {
                ["employeeId"] = e.Id.ToString(),
                ["name"] = $"{e.FirstName} {e.LastName}",
                ["departmentId"] = e.DepartmentId?.ToString() ?? "",
                ["branchId"] = e.PrimaryBranchId?.ToString() ?? ""
            } as IReadOnlyDictionary<string, string>).ToList());
    }

    public async Task<EnterpriseReportDto> GetApprovalAnalyticsAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        await EnsureReportingAccess(cancellationToken);
        var (start, end) = Range(from, to);
        var requests = await _db.ApprovalRequests.AsNoTracking()
            .Where(x => x.RequestedAt >= start && x.RequestedAt <= end)
            .ToListAsync(cancellationToken);
        var decided = requests.Where(x => x.Status is "Approved" or "Rejected").ToList();
        var avgHours = decided.Count == 0
            ? 0
            : decided.Average(x => ((x.UpdatedAt ?? x.RequestedAt) - x.RequestedAt).TotalHours);

        return new EnterpriseReportDto(
            "Approval analytics",
            DateTime.UtcNow,
            [
                new("total", "Requests", requests.Count),
                new("approved", "Approved", requests.Count(x => x.Status == "Approved")),
                new("rejected", "Rejected", requests.Count(x => x.Status == "Rejected")),
                new("pending", "Pending", requests.Count(x => x.Status == "Pending")),
                new("avgHours", "Avg decision hours", (decimal)avgHours)
            ],
            []);
    }

    public async Task<EnterpriseReportDto> GetAgentProductivityAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        await EnsureReportingAccess(cancellationToken);
        var (start, end) = Range(from, to);
        var executions = await _db.NalvaExecutions.AsNoTracking()
            .Where(x => x.CreatedAt >= start && x.CreatedAt <= end)
            .ToListAsync(cancellationToken);
        return new EnterpriseReportDto(
            "Agent productivity",
            DateTime.UtcNow,
            [
                new("executions", "Executions", executions.Count),
                new("tokens", "Tokens", executions.Sum(x => x.InputTokens + x.OutputTokens))
            ],
            executions.GroupBy(x => x.AgentKey)
                .Select(g => new Dictionary<string, string>
                {
                    ["agentKey"] = g.Key,
                    ["executions"] = g.Count().ToString(),
                    ["tokens"] = g.Sum(x => x.InputTokens + x.OutputTokens).ToString()
                } as IReadOnlyDictionary<string, string>)
                .ToList());
    }

    public async Task<EnterpriseReportDto> GetConsolidatedFinanceAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        await EnsureReportingAccess(cancellationToken);
        var (start, end) = Range(from, to);
        var revenue = await _db.Orders.AsNoTracking()
            .Where(x => x.OrderDate >= start && x.OrderDate <= end)
            .SumAsync(x => (decimal?)x.GrandTotal, cancellationToken) ?? 0;
        var expenses = await _db.Expenses.AsNoTracking()
            .Where(x => x.ExpenseDate >= start && x.ExpenseDate <= end)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
        return new EnterpriseReportDto(
            "Consolidated finance",
            DateTime.UtcNow,
            [
                new("revenue", "Revenue", revenue, "money"),
                new("expenses", "Expenses", expenses, "money"),
                new("net", "Net", revenue - expenses, "money")
            ],
            []);
    }

    private async Task EnsureReportingAccess(CancellationToken cancellationToken)
    {
        await _guard.EnsureAdvancedSecurityAsync(cancellationToken);
        await CommercialFeatureCheck.EnsureAdvancedReportsAsync(_db, _currentUser, cancellationToken);
    }

    private static (DateTime Start, DateTime End) Range(DateTime? from, DateTime? to)
    {
        var end = to?.ToUniversalTime() ?? DateTime.UtcNow;
        var start = from?.ToUniversalTime() ?? end.AddDays(-30);
        return (start, end);
    }
}

internal static class CommercialFeatureCheck
{
    public static async Task EnsureAdvancedReportsAsync(
        BusinessOSDbContext db,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return;
        await Billing.CommercialGate.EnsureFeatureAsync(db, tenantId, Domain.Billing.CommercialFeatureKeys.AdvancedReports, cancellationToken);
    }
}
