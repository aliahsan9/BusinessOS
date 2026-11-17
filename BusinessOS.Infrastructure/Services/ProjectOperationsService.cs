using System.Security.Cryptography;
using System.Text;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Operations.DTOs;
using BusinessOS.Application.Features.Operations.Services;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Billing;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BusinessOS.Infrastructure.Services;

public sealed class ProjectOperationsService : IProjectOperationsService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".txt", ".csv", ".docx", ".xlsx", ".zip"
    };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly ITenantProvider _tenant;
    private readonly string _root;
    private readonly long _maxBytes;

    public ProjectOperationsService(
        IApplicationDbContext db,
        ICurrentUserService user,
        ITenantProvider tenant,
        IConfiguration configuration)
    {
        _db = db;
        _user = user;
        _tenant = tenant;
        _root = configuration["FileStorage:RootPath"] ?? Path.Combine(Path.GetTempPath(), "businessos-files");
        _maxBytes = long.TryParse(configuration["FileStorage:MaxBytes"], out var max) ? max : 10 * 1024 * 1024;
    }

    public async Task<PagedResult<ProjectDto>> GetProjectsAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.ProjectView, PermissionCodes.ProjectUpdate);
        var query = await VisibleProjects(cancellationToken);
        var paged = await query.OrderByDescending(x => x.CreatedAt).PageAsync(page, pageSize, cancellationToken);
        var showBudget = _user.HasPermission(PermissionCodes.FinanceView) || _user.HasPermission(PermissionCodes.ProjectUpdate);
        return new PagedResult<ProjectDto>
        {
            Items = paged.Items.Select(x => MapProject(x, showBudget)).ToList(),
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount
        };
    }

    public async Task<ProjectDto> SaveProjectAsync(Guid? id, ProjectRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, id is null ? PermissionCodes.ProjectCreate : PermissionCodes.ProjectUpdate);
        var project = id is null ? new Project() : await _db.Projects.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Project not found.");
        if (id is null)
            _db.Projects.Add(project);
        project.Name = Ops.Required(request.Name, "Name");
        project.Description = request.Description?.Trim();
        project.Code = request.Code?.Trim();
        project.CustomerId = request.CustomerId;
        project.ManagerUserId = request.ManagerUserId;
        project.AssignedUserId = request.ManagerUserId;
        project.StartDate = request.StartDate;
        project.Deadline = request.Deadline;
        project.Budget = request.Budget;
        project.Priority = request.Priority;
        project.Workflow = string.IsNullOrWhiteSpace(request.Workflow) ? "General" : request.Workflow.Trim();
        project.Visibility = string.IsNullOrWhiteSpace(request.Visibility) ? "Internal" : request.Visibility.Trim();
        project.DealId = request.DealId;
        project.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapProject(project, true);
    }

    public async Task AddProjectMemberAsync(Guid projectId, ProjectMemberRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.ProjectUpdate);
        await RequireProjectAsync(projectId, cancellationToken);
        var userId = Ops.Required(request.UserId, "User");
        var existing = await _db.ProjectMembers.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == userId, cancellationToken);
        if (existing is null)
        {
            _db.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = projectId,
                UserId = userId,
                RoleName = string.IsNullOrWhiteSpace(request.RoleName) ? "Contributor" : request.RoleName.Trim()
            });
        }
        else
        {
            existing.RoleName = string.IsNullOrWhiteSpace(request.RoleName) ? existing.RoleName : request.RoleName.Trim();
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<TaskDto>> GetTasksAsync(Guid? projectId, string? stage, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.TaskView, PermissionCodes.ProjectView);
        var query = _db.WorkTasks.AsNoTracking().AsQueryable();
        if (projectId is not null)
        {
            await RequireProjectAsync(projectId.Value, cancellationToken);
            query = query.Where(x => x.ProjectId == projectId);
        }
        if (!string.IsNullOrWhiteSpace(stage))
            query = query.Where(x => x.Stage == stage);
        var paged = await query.OrderBy(x => x.Priority).ThenBy(x => x.DueDate).PageAsync(page, pageSize, cancellationToken);
        return new PagedResult<TaskDto>
        {
            Items = paged.Items.Select(MapTask).ToList(),
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount
        };
    }

    public async Task<TaskDto> SaveTaskAsync(Guid? id, TaskRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, id is null ? PermissionCodes.TaskCreate : PermissionCodes.TaskUpdate);
        await RequireProjectAsync(request.ProjectId, cancellationToken);
        var task = id is null ? new WorkTask() : await _db.WorkTasks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Task not found.");
        if (id is null)
            _db.WorkTasks.Add(task);
        task.ProjectId = request.ProjectId;
        task.Title = Ops.Required(request.Title, "Title");
        if (id is null || request.Description is not null)
            task.Description = request.Description;
        task.AssignedUserId = request.AssignedUserId;
        task.Priority = request.Priority;
        task.DueDate = request.DueDate;
        task.Stage = request.Stage;
        task.TaskKind = string.IsNullOrWhiteSpace(request.TaskKind) ? "Task" : request.TaskKind.Trim();
        task.ParentTaskId = request.ParentTaskId;
        task.SprintId = request.SprintId;
        task.IsClientVisible = request.IsClientVisible;
        task.Status = MapStage(request.Stage);
        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapTask(task);
    }

    public async Task AddCommentAsync(Guid taskId, TaskCommentRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.TaskUpdate, PermissionCodes.TaskView);
        var task = await _db.WorkTasks.FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken)
            ?? throw new NotFoundException("Task not found.");
        await RequireProjectAsync(task.ProjectId, cancellationToken);
        _db.WorkTaskComments.Add(new WorkTaskComment
        {
            WorkTaskId = taskId,
            UserId = Ops.User(_user),
            Body = Ops.Required(request.Body, "Comment"),
            IsClientVisible = request.IsClientVisible
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<MilestoneDto> SaveMilestoneAsync(MilestoneRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.ProjectUpdate);
        await RequireProjectAsync(request.ProjectId, cancellationToken);
        var milestone = new ProjectMilestone
        {
            ProjectId = request.ProjectId,
            Name = Ops.Required(request.Name, "Name"),
            DueDate = request.DueDate,
            IsClientVisible = request.IsClientVisible
        };
        _db.ProjectMilestones.Add(milestone);
        await _db.SaveChangesAsync(cancellationToken);
        return MapMilestone(milestone);
    }

    public async Task<IReadOnlyList<MilestoneDto>> GetMilestonesAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.ProjectView);
        await RequireProjectAsync(projectId, cancellationToken);
        return await _db.ProjectMilestones.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.DueDate)
            .Select(x => new MilestoneDto(x.Id, x.ProjectId, x.Name, x.Status, x.DueDate, x.IsClientVisible))
            .ToListAsync(cancellationToken);
    }

    public async Task<MilestoneDto> UpdateMilestoneStatusAsync(Guid id, string status, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.ProjectUpdate);
        var milestone = await _db.ProjectMilestones.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Milestone not found.");
        milestone.Status = Ops.Required(status, "Status");
        milestone.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapMilestone(milestone);
    }

    public async Task<SprintDto> SaveSprintAsync(SprintRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.ProjectUpdate, PermissionCodes.TaskCreate);
        var sprint = new Sprint
        {
            ProjectId = request.ProjectId,
            Name = Ops.Required(request.Name, "Name"),
            StartDate = request.StartDate,
            EndDate = request.EndDate
        };
        _db.Sprints.Add(sprint);
        await _db.SaveChangesAsync(cancellationToken);
        return new SprintDto(sprint.Id, sprint.ProjectId, sprint.Name, sprint.Status, sprint.StartDate, sprint.EndDate);
    }

    public async Task<TimeEntryDto> StartTimerAsync(Guid? projectId, Guid? taskId, bool billable, decimal? hourlyRate, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.TimeView, PermissionCodes.TimeManage);
        var userId = Ops.User(_user);
        if (await _db.TimeEntries.AnyAsync(x => x.UserId == userId && x.Status == TimeEntryStatuses.Running, cancellationToken))
            throw new ConflictException("Stop the active timer before starting another one.");
        if (hourlyRate is not null)
            Ops.Require(_user, PermissionCodes.FinanceView, PermissionCodes.TimeManage);
        var entry = new TimeEntry
        {
            ProjectId = projectId,
            WorkTaskId = taskId,
            UserId = userId,
            StartedAt = DateTime.UtcNow,
            IsBillable = billable,
            HourlyRate = _user.HasPermission(PermissionCodes.FinanceView) || _user.HasPermission(PermissionCodes.TimeManage) ? hourlyRate : null,
            Status = TimeEntryStatuses.Running
        };
        _db.TimeEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
        return MapTime(entry, true);
    }

    public async Task<TimeEntryDto> StopTimerAsync(CancellationToken cancellationToken = default)
    {
        var userId = Ops.User(_user);
        var entry = await _db.TimeEntries.FirstOrDefaultAsync(x => x.UserId == userId && x.Status == TimeEntryStatuses.Running, cancellationToken)
            ?? throw new NotFoundException("No active timer.");
        entry.EndedAt = DateTime.UtcNow;
        entry.Hours = Math.Round((decimal)(entry.EndedAt.Value - (entry.StartedAt ?? entry.EndedAt.Value)).TotalHours, 2);
        entry.Status = TimeEntryStatuses.Submitted;
        await _db.SaveChangesAsync(cancellationToken);
        return MapTime(entry, _user.HasPermission(PermissionCodes.FinanceView));
    }

    public async Task<TimeEntryDto> SaveTimeAsync(TimeEntryRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.TimeView, PermissionCodes.TimeManage);
        if (request.Hours <= 0)
            throw new BadRequestException("Hours must be positive.");
        var entry = new TimeEntry
        {
            ProjectId = request.ProjectId,
            WorkTaskId = request.WorkTaskId,
            UserId = Ops.User(_user),
            Hours = request.Hours,
            IsBillable = request.IsBillable,
            HourlyRate = _user.HasPermission(PermissionCodes.FinanceView) ? request.HourlyRate : null,
            Notes = request.Notes,
            StartedAt = request.StartedAt,
            Status = TimeEntryStatuses.Submitted
        };
        _db.TimeEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
        return MapTime(entry, _user.HasPermission(PermissionCodes.FinanceView));
    }

    public async Task<TimeEntryDto> ApproveTimeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.TimeManage);
        var entry = await _db.TimeEntries.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Time entry not found.");
        if (entry.Status == TimeEntryStatuses.Running)
            throw new BadRequestException("Stop the timer before approving it.");
        entry.Status = TimeEntryStatuses.Approved;
        entry.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapTime(entry, true);
    }

    public async Task<PagedResult<TimeEntryDto>> GetTimeAsync(Guid? projectId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.TimeView, PermissionCodes.TimeManage);
        var query = _db.TimeEntries.AsNoTracking().AsQueryable();
        if (!_user.HasPermission(PermissionCodes.TimeManage))
            query = query.Where(x => x.UserId == _user.UserId);
        if (projectId is not null)
            query = query.Where(x => x.ProjectId == projectId);
        var paged = await query.OrderByDescending(x => x.CreatedAt).PageAsync(page, pageSize, cancellationToken);
        var showRate = _user.HasPermission(PermissionCodes.FinanceView) || _user.HasPermission(PermissionCodes.TimeManage);
        return new PagedResult<TimeEntryDto>
        {
            Items = paged.Items.Select(x => MapTime(x, showRate)).ToList(),
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount
        };
    }

    public async Task<ProjectProgressDto> GetProgressAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.ProjectView, PermissionCodes.TaskView);
        var project = await RequireProjectAsync(projectId, cancellationToken);
        var tasks = await _db.WorkTasks.AsNoTracking().Where(x => x.ProjectId == projectId).ToListAsync(cancellationToken);
        var milestones = await _db.ProjectMilestones.AsNoTracking().Where(x => x.ProjectId == projectId).ToListAsync(cancellationToken);
        var time = await _db.TimeEntries.AsNoTracking().Where(x => x.ProjectId == projectId && x.Status != TimeEntryStatuses.Running).ToListAsync(cancellationToken);
        var completed = tasks.Count(x => x.Status == WorkTaskStatus.Done);
        var overdue = tasks.Count(x => x.DueDate != null && x.DueDate < DateTime.UtcNow && x.Status != WorkTaskStatus.Done && x.Status != WorkTaskStatus.Cancelled);
        var showMoney = _user.HasPermission(PermissionCodes.FinanceView) || _user.HasPermission(PermissionCodes.ProjectUpdate);
        var billable = time.Where(x => x.IsBillable).Sum(x => x.Hours * (x.HourlyRate ?? 0));
        var total = tasks.Count;
        return new ProjectProgressDto(
            total,
            completed,
            tasks.Count(x => x.Status is WorkTaskStatus.Todo or WorkTaskStatus.InProgress),
            overdue,
            milestones.Count(x => x.Status != "Completed"),
            milestones.Count(x => x.Status == "Completed"),
            time.Sum(x => x.Hours),
            showMoney ? project.Budget : null,
            showMoney ? billable : 0,
            showMoney ? project.Budget - billable : 0,
            total == 0 ? 0 : Math.Round(completed * 100m / total, 2));
    }

    public async Task<ProjectFileDto> SaveFileAsync(Guid projectId, string fileName, string contentType, Stream content, bool clientVisible, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.ProjectUpdate, PermissionCodes.TaskUpdate);
        await RequireProjectAsync(projectId, cancellationToken);
        var safeName = Path.GetFileName(fileName);
        var extension = Path.GetExtension(safeName);
        if (!AllowedExtensions.Contains(extension))
            throw new BadRequestException("This file type is not allowed.");
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length == 0 || buffer.Length > _maxBytes)
            throw new BadRequestException("File is empty or larger than the allowed size.");
        var tenantId = Ops.Tenant(_user);
        var key = Path.Combine(tenantId.ToString("N"), projectId.ToString("N"), $"{Guid.NewGuid():N}{extension}");
        var fullPath = Path.Combine(_root, key);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, buffer.ToArray(), cancellationToken);
        var version = await _db.ProjectFiles.CountAsync(x => x.ProjectId == projectId && x.FileName == safeName, cancellationToken) + 1;
        var file = new ProjectFile
        {
            ProjectId = projectId,
            FileName = safeName,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            SizeBytes = buffer.Length,
            StorageKey = key,
            UploadedByUserId = Ops.User(_user),
            IsClientVisible = clientVisible,
            Version = version
        };
        _db.ProjectFiles.Add(file);
        await _db.SaveChangesAsync(cancellationToken);
        return MapFile(file);
    }

    public async Task<(ProjectFileDto Meta, Stream Content)> OpenFileAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.ProjectView, PermissionCodes.TaskView);
        var file = await _db.ProjectFiles.FirstOrDefaultAsync(x => x.Id == fileId, cancellationToken)
            ?? throw new NotFoundException("File not found.");
        await RequireProjectAsync(file.ProjectId, cancellationToken);
        var stream = OpenStored(file.StorageKey);
        return (MapFile(file), stream);
    }

    public async Task<ClientAccessDto> CreateClientAccessAsync(Guid projectId, string email, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.PortalManage, PermissionCodes.ProjectUpdate);
        if (_db is BusinessOSDbContext commercial && _tenant.HasTenant())
            await CommercialGate.EnsureFeatureAsync(commercial, _tenant.TenantId, FeatureFlags.ClientPortal, cancellationToken);
        var project = await RequireProjectAsync(projectId, cancellationToken);
        if (project.CustomerId is null)
            throw new BadRequestException("Assign a customer before inviting a client.");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var access = new ClientPortalAccess
        {
            CustomerId = project.CustomerId.Value,
            ProjectId = project.Id,
            Email = Ops.Required(email, "Email").ToLowerInvariant(),
            TokenHash = Hash(token),
            ExpiresAt = DateTime.UtcNow.AddDays(14)
        };
        _db.ClientPortalAccesses.Add(access);
        await _db.SaveChangesAsync(cancellationToken);
        return new ClientAccessDto(access.Id, access.Email, access.ExpiresAt, token);
    }

    public async Task<IReadOnlyList<PortalProjectDto>> GetPortalAsync(string token, CancellationToken cancellationToken = default)
    {
        var access = await RequirePortalAsync(token, cancellationToken);
        var projects = await _db.Projects.IgnoreQueryFilters()
            .Where(x => x.TenantId == access.TenantId && !x.IsDeleted && x.CustomerId == access.CustomerId && (access.ProjectId == null || x.Id == access.ProjectId))
            .ToListAsync(cancellationToken);
        var result = new List<PortalProjectDto>();
        foreach (var project in projects)
        {
            var milestones = await _db.ProjectMilestones.IgnoreQueryFilters()
                .Where(x => x.TenantId == access.TenantId && x.ProjectId == project.Id && !x.IsDeleted && x.IsClientVisible)
                .ToListAsync(cancellationToken);
            var tasks = await _db.WorkTasks.IgnoreQueryFilters()
                .Where(x => x.TenantId == access.TenantId && x.ProjectId == project.Id && !x.IsDeleted && x.IsClientVisible)
                .ToListAsync(cancellationToken);
            var files = await _db.ProjectFiles.IgnoreQueryFilters()
                .Where(x => x.TenantId == access.TenantId && x.ProjectId == project.Id && !x.IsDeleted && x.IsClientVisible)
                .ToListAsync(cancellationToken);
            result.Add(new PortalProjectDto(
                project.Id,
                project.Name,
                project.Status.ToString(),
                project.Deadline,
                milestones.Select(MapMilestone).ToList(),
                tasks.Select(MapTask).ToList(),
                files.Select(MapFile).ToList()));
        }
        access.LastUsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task AddPortalFeedbackAsync(string token, PortalFeedbackRequest request, CancellationToken cancellationToken = default)
    {
        var access = await RequirePortalAsync(token, cancellationToken);
        if (access.ProjectId is not null && access.ProjectId != request.ProjectId)
            throw new UnauthorizedException("This portal link cannot access that project.");
        _db.CustomerInteractions.Add(new CustomerInteraction
        {
            TenantId = access.TenantId,
            CustomerId = access.CustomerId,
            ProjectId = request.ProjectId,
            Channel = "Portal",
            Summary = Ops.Required(request.Message, "Message"),
            UserId = access.Email
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(ProjectFileDto Meta, Stream Content)> OpenPortalFileAsync(string token, Guid fileId, CancellationToken cancellationToken = default)
    {
        var access = await RequirePortalAsync(token, cancellationToken);
        var file = await _db.ProjectFiles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == fileId && x.TenantId == access.TenantId && !x.IsDeleted && x.IsClientVisible, cancellationToken)
            ?? throw new NotFoundException("File not found.");
        if (access.ProjectId is not null && file.ProjectId != access.ProjectId)
            throw new UnauthorizedException("This portal link cannot access that file.");
        return (MapFile(file), OpenStored(file.StorageKey));
    }

    private async Task<IQueryable<Project>> VisibleProjects(CancellationToken cancellationToken)
    {
        var query = _db.Projects.AsNoTracking();
        if (_user.HasPermission(PermissionCodes.ProjectUpdate))
            return query;
        var userId = _user.UserId ?? string.Empty;
        var memberIds = await _db.ProjectMembers.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.ProjectId).ToListAsync(cancellationToken);
        return query.Where(x => x.Visibility != "Restricted" || x.ManagerUserId == userId || x.AssignedUserId == userId || memberIds.Contains(x.Id));
    }

    private async Task<Project> RequireProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken)
            ?? throw new NotFoundException("Project not found.");
        if (project.Visibility != "Restricted" || _user.HasPermission(PermissionCodes.ProjectUpdate))
            return project;
        var userId = _user.UserId;
        var member = await _db.ProjectMembers.AnyAsync(x => x.ProjectId == projectId && x.UserId == userId, cancellationToken);
        if (!member && project.ManagerUserId != userId && project.AssignedUserId != userId)
            throw new UnauthorizedException("You do not have access to this project.");
        return project;
    }

    private async Task<ClientPortalAccess> RequirePortalAsync(string token, CancellationToken cancellationToken)
    {
        var hash = Hash(token);
        var access = await _db.ClientPortalAccesses.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TokenHash == hash && !x.IsDeleted, cancellationToken)
            ?? throw new UnauthorizedException("Portal link is invalid.");
        if (access.IsRevoked || access.ExpiresAt <= DateTime.UtcNow)
            throw new UnauthorizedException("Portal link has expired.");
        _tenant.SetTenantId(access.TenantId);
        return access;
    }

    private Stream OpenStored(string storageKey)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, storageKey));
        var root = Path.GetFullPath(_root);
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            throw new NotFoundException("File content is missing.");
        return File.OpenRead(fullPath);
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static WorkTaskStatus MapStage(string? stage)
    {
        if (string.IsNullOrWhiteSpace(stage))
            return WorkTaskStatus.Todo;
        if (stage.Equals("Deployed", StringComparison.OrdinalIgnoreCase) || stage.Equals("Done", StringComparison.OrdinalIgnoreCase) || stage.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            return WorkTaskStatus.Done;
        if (stage.Equals("Backlog", StringComparison.OrdinalIgnoreCase) || stage.Equals("To Do", StringComparison.OrdinalIgnoreCase))
            return WorkTaskStatus.Todo;
        return WorkTaskStatus.InProgress;
    }

    private static ProjectDto MapProject(Project project, bool showBudget) =>
        new(project.Id, project.Name, project.Code, project.Description, project.CustomerId, project.Status.ToString(),
            project.ManagerUserId, project.StartDate, project.Deadline, showBudget ? project.Budget : null,
            project.Priority, project.Workflow, project.Visibility, project.DealId);

    private static TaskDto MapTask(WorkTask task) =>
        new(task.Id, task.ProjectId, task.Title, task.Status.ToString(), task.Stage, task.TaskKind, task.Priority, task.DueDate, task.AssignedUserId, task.ParentTaskId, task.IsClientVisible);

    private static MilestoneDto MapMilestone(ProjectMilestone milestone) =>
        new(milestone.Id, milestone.ProjectId, milestone.Name, milestone.Status, milestone.DueDate, milestone.IsClientVisible);

    private static TimeEntryDto MapTime(TimeEntry entry, bool showRate) =>
        new(entry.Id, entry.ProjectId, entry.WorkTaskId, entry.UserId, entry.Hours, entry.IsBillable, showRate ? entry.HourlyRate : null, entry.Status, entry.StartedAt, entry.EndedAt);

    private static ProjectFileDto MapFile(ProjectFile file) =>
        new(file.Id, file.FileName, file.ContentType, file.SizeBytes, file.IsClientVisible, file.Version, file.CreatedAt);
}
