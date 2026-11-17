using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Operations.DTOs;
using BusinessOS.Application.Features.Operations.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Services;

public sealed class WorkforceService : IWorkforceService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly ILogger<WorkforceService> _logger;

    public WorkforceService(IApplicationDbContext db, ICurrentUserService user, ILogger<WorkforceService> logger)
    {
        _db = db;
        _user = user;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.EmployeeView, PermissionCodes.EmployeeManage);
        return await _db.Departments.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new DepartmentDto(x.Id, x.Name, x.Description, x.ManagerEmployeeId, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<DepartmentDto> SaveDepartmentAsync(Guid? id, DepartmentRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.EmployeeManage);
        var name = Ops.Required(request.Name, "Name");
        var department = id is null ? new Department() : await _db.Departments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Department not found.");
        if (id is null)
            _db.Departments.Add(department);
        department.Name = name;
        department.Description = request.Description?.Trim();
        department.ManagerEmployeeId = request.ManagerEmployeeId;
        department.UpdatedAt = DateTime.UtcNow;
        Ops.Track(_db, Ops.User(_user), id is null ? "Created" : "Updated", "Department", department.Id, name);
        await _db.SaveChangesAsync(cancellationToken);
        return new DepartmentDto(department.Id, department.Name, department.Description, department.ManagerEmployeeId, department.IsActive);
    }

    public async Task<IReadOnlyList<TeamDto>> GetTeamsAsync(CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.EmployeeView, PermissionCodes.EmployeeManage);
        var teams = await _db.CompanyTeams.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var counts = await _db.Employees.AsNoTracking()
            .Where(x => x.TeamId != null)
            .GroupBy(x => x.TeamId)
            .Select(x => new { TeamId = x.Key, Count = x.Count() })
            .ToListAsync(cancellationToken);
        return teams.Select(team => new TeamDto(
            team.Id, team.Name, team.Description, team.DepartmentId, team.LeaderEmployeeId, team.IsActive,
            counts.FirstOrDefault(x => x.TeamId == team.Id)?.Count ?? 0)).ToList();
    }

    public async Task<TeamDto> SaveTeamAsync(Guid? id, TeamRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.EmployeeManage);
        var name = Ops.Required(request.Name, "Name");
        var team = id is null ? new CompanyTeam() : await _db.CompanyTeams.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Team not found.");
        if (id is null)
            _db.CompanyTeams.Add(team);
        team.Name = name;
        team.Description = request.Description?.Trim();
        team.DepartmentId = request.DepartmentId;
        team.LeaderEmployeeId = request.LeaderEmployeeId;
        team.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return new TeamDto(team.Id, team.Name, team.Description, team.DepartmentId, team.LeaderEmployeeId, team.IsActive, 0);
    }

    public async Task AssignTeamMemberAsync(Guid teamId, Guid employeeId, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.EmployeeManage);
        var team = await _db.CompanyTeams.FirstOrDefaultAsync(x => x.Id == teamId, cancellationToken)
            ?? throw new NotFoundException("Team not found.");
        var employee = await _db.Employees.FirstOrDefaultAsync(x => x.Id == employeeId, cancellationToken)
            ?? throw new NotFoundException("Employee not found.");
        employee.TeamId = team.Id;
        if (team.DepartmentId is not null)
            employee.DepartmentId = team.DepartmentId;
        employee.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<EmployeeDto>> GetEmployeesAsync(string? search, string? status, Guid? departmentId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.EmployeeView, PermissionCodes.EmployeeManage);
        var query = _db.Employees.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.FirstName.Contains(term) || x.LastName.Contains(term) || x.Email.Contains(term) || (x.EmployeeCode != null && x.EmployeeCode.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.EmploymentStatus == status);
        if (departmentId is not null)
            query = query.Where(x => x.DepartmentId == departmentId);
        var paged = await query.OrderBy(x => x.LastName).PageAsync(page, pageSize, cancellationToken);
        var showSalary = _user.HasPermission(PermissionCodes.EmployeeSalary);
        var items = new List<EmployeeDto>();
        foreach (var employee in paged.Items)
            items.Add(await MapEmployeeAsync(employee, showSalary, cancellationToken));
        return new PagedResult<EmployeeDto>
        {
            Items = items,
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount
        };
    }

    public async Task<EmployeeDto> GetEmployeeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var self = await CurrentEmployeeAsync(cancellationToken);
        if (!_user.HasPermission(PermissionCodes.EmployeeView) && !_user.HasPermission(PermissionCodes.EmployeeManage) && self?.Id != id)
            throw new UnauthorizedException("You do not have permission to perform this action.");
        var employee = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Employee not found.");
        var showSalary = _user.HasPermission(PermissionCodes.EmployeeSalary);
        return await MapEmployeeAsync(employee, showSalary, cancellationToken);
    }

    public async Task<EmployeeDto> SaveEmployeeAsync(Guid? id, EmployeeRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.EmployeeManage);
        var employee = id is null ? new Employee() : await _db.Employees.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Employee not found.");
        var email = Ops.Required(request.Email, "Email").ToLowerInvariant();
        var duplicate = await _db.Employees.AnyAsync(x => x.Email == email && x.Id != employee.Id, cancellationToken);
        if (duplicate)
            throw new ConflictException("An employee with this email already exists.");
        if (id is null)
        {
            employee.EmployeeCode = Ops.Number("EMP");
            employee.IsActive = true;
            employee.EmploymentStatus = EmploymentStatuses.Active;
            _db.Employees.Add(employee);
        }
        employee.FirstName = Ops.Required(request.FirstName, "First name");
        employee.LastName = Ops.Required(request.LastName, "Last name");
        employee.Email = email;
        employee.Phone = Ops.Required(request.Phone, "Phone");
        employee.Designation = Ops.Required(request.JobTitle, "Job title");
        employee.JoiningDate = request.JoiningDate == default ? DateTime.UtcNow : request.JoiningDate;
        employee.DepartmentId = request.DepartmentId;
        employee.TeamId = request.TeamId;
        employee.ManagerEmployeeId = request.ManagerEmployeeId;
        employee.EmploymentType = string.IsNullOrWhiteSpace(request.EmploymentType) ? EmploymentTypes.FullTime : request.EmploymentType.Trim();
        employee.Skills = request.Skills?.Trim();
        employee.WorkLocation = request.WorkLocation?.Trim();
        employee.EmergencyContactName = request.EmergencyContactName?.Trim();
        employee.EmergencyContactPhone = request.EmergencyContactPhone?.Trim();
        employee.UserId = string.IsNullOrWhiteSpace(request.UserId) ? employee.UserId : request.UserId.Trim();
        employee.ProfilePictureUrl = request.ProfilePictureUrl?.Trim();
        if (request.Salary is not null && _user.HasPermission(PermissionCodes.EmployeeSalary))
            employee.Salary = request.Salary.Value;
        employee.UpdatedAt = DateTime.UtcNow;
        Ops.Track(_db, Ops.User(_user), id is null ? "Created" : "Updated", "Employee", employee.Id, employee.Email);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Employee {EmployeeId} saved", employee.Id);
        return await GetEmployeeAsync(employee.Id, cancellationToken);
    }

    public async Task SetEmployeeActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.EmployeeManage);
        var employee = await _db.Employees.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Employee not found.");
        employee.IsActive = active;
        employee.EmploymentStatus = active ? EmploymentStatuses.Active : EmploymentStatuses.Inactive;
        employee.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AttendanceDto> CheckInAsync(Guid? employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await ResolveAttendanceEmployeeAsync(employeeId, cancellationToken);
        var local = await LocalNowAsync(cancellationToken);
        var workDate = DateOnly.FromDateTime(local);
        var existing = await _db.AttendanceRecords.FirstOrDefaultAsync(x => x.EmployeeId == employee.Id && x.WorkDate == workDate, cancellationToken);
        if (existing?.CheckInAt is not null && existing.CheckOutAt is null)
            throw new ConflictException("This employee is already checked in.");
        if (existing?.CheckOutAt is not null)
            throw new ConflictException("Attendance for today is already complete.");
        var settings = await SettingsAsync(cancellationToken);
        var record = existing ?? new AttendanceRecord { EmployeeId = employee.Id, WorkDate = workDate };
        if (existing is null)
            _db.AttendanceRecords.Add(record);
        record.CheckInAt = DateTime.UtcNow;
        record.IsLate = local.TimeOfDay > settings.WorkdayStart.ToTimeSpan().Add(TimeSpan.FromMinutes(settings.LateGraceMinutes));
        record.Status = record.IsLate ? "Late" : "Present";
        await _db.SaveChangesAsync(cancellationToken);
        return MapAttendance(record, employee);
    }

    public async Task<AttendanceDto> CheckOutAsync(Guid? employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await ResolveAttendanceEmployeeAsync(employeeId, cancellationToken);
        var local = await LocalNowAsync(cancellationToken);
        var workDate = DateOnly.FromDateTime(local);
        var record = await _db.AttendanceRecords.FirstOrDefaultAsync(x => x.EmployeeId == employee.Id && x.WorkDate == workDate, cancellationToken)
            ?? throw new BadRequestException("Check in before checking out.");
        if (record.CheckInAt is null || record.CheckOutAt is not null)
            throw new ConflictException("There is no open check-in to close.");
        var settings = await SettingsAsync(cancellationToken);
        record.CheckOutAt = DateTime.UtcNow;
        record.WorkedHours = Math.Round((decimal)(record.CheckOutAt.Value - record.CheckInAt.Value).TotalHours, 2);
        record.IsEarlyDeparture = local.TimeOfDay < settings.WorkdayEnd.ToTimeSpan();
        await _db.SaveChangesAsync(cancellationToken);
        return MapAttendance(record, employee);
    }

    public async Task<PagedResult<AttendanceDto>> GetAttendanceAsync(Guid? employeeId, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var self = await CurrentEmployeeAsync(cancellationToken);
        if (!_user.HasPermission(PermissionCodes.AttendanceManage))
            employeeId = self?.Id ?? Guid.Empty;
        var query = from record in _db.AttendanceRecords.AsNoTracking()
                    join employee in _db.Employees.AsNoTracking() on record.EmployeeId equals employee.Id
                    select new { record, employee };
        if (employeeId is not null)
            query = query.Where(x => x.record.EmployeeId == employeeId);
        if (from is not null)
            query = query.Where(x => x.record.WorkDate >= from);
        if (to is not null)
            query = query.Where(x => x.record.WorkDate <= to);
        var paged = await query.OrderByDescending(x => x.record.WorkDate).PageAsync(page, pageSize, cancellationToken);
        return new PagedResult<AttendanceDto>
        {
            Items = paged.Items.Select(x => MapAttendance(x.record, x.employee)).ToList(),
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount
        };
    }

    public async Task<AttendanceDto> CorrectAttendanceAsync(Guid id, CorrectAttendanceRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.AttendanceManage);
        var reason = Ops.Required(request.Reason, "Reason");
        var record = await _db.AttendanceRecords.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Attendance record not found.");
        if (request.CheckInAt is not null)
            record.CheckInAt = request.CheckInAt;
        if (request.CheckOutAt is not null)
            record.CheckOutAt = request.CheckOutAt;
        if (record.CheckInAt is not null && record.CheckOutAt is not null && record.CheckOutAt < record.CheckInAt)
            throw new BadRequestException("Check-out cannot be earlier than check-in.");
        record.WorkedHours = record.CheckInAt is not null && record.CheckOutAt is not null
            ? Math.Round((decimal)(record.CheckOutAt.Value - record.CheckInAt.Value).TotalHours, 2)
            : record.WorkedHours;
        record.CorrectionReason = reason;
        record.CorrectedByUserId = Ops.User(_user);
        record.Status = "Corrected";
        record.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        var employee = await _db.Employees.AsNoTracking().FirstAsync(x => x.Id == record.EmployeeId, cancellationToken);
        return MapAttendance(record, employee);
    }

    public async Task<IReadOnlyList<LeaveTypeDto>> GetLeaveTypesAsync(CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.EmployeeView, PermissionCodes.LeaveManage);
        return await _db.LeaveTypes.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new LeaveTypeDto(x.Id, x.Name, x.DefaultDays, x.IsPaid, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<LeaveTypeDto> SaveLeaveTypeAsync(LeaveTypeRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.LeaveManage);
        var leaveType = new LeaveType
        {
            Name = Ops.Required(request.Name, "Name"),
            DefaultDays = request.DefaultDays,
            IsPaid = request.IsPaid
        };
        _db.LeaveTypes.Add(leaveType);
        await _db.SaveChangesAsync(cancellationToken);
        return new LeaveTypeDto(leaveType.Id, leaveType.Name, leaveType.DefaultDays, leaveType.IsPaid, leaveType.IsActive);
    }

    public async Task<LeaveRequestDto> ApplyLeaveAsync(LeaveRequestCreate request, CancellationToken cancellationToken = default)
    {
        var self = await CurrentEmployeeAsync(cancellationToken);
        var employeeId = request.EmployeeId ?? self?.Id ?? throw new BadRequestException("No employee profile is linked to this user.");
        if (self?.Id != employeeId)
            Ops.Require(_user, PermissionCodes.LeaveManage);
        else
            Ops.Require(_user, PermissionCodes.EmployeeView, PermissionCodes.LeaveManage);
        if (request.EndDate < request.StartDate)
            throw new BadRequestException("Leave end date cannot be before the start date.");
        var leaveType = await _db.LeaveTypes.FirstOrDefaultAsync(x => x.Id == request.LeaveTypeId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Leave type not found.");
        var overlap = await _db.LeaveRequests.AnyAsync(x =>
            x.EmployeeId == employeeId &&
            x.Status != LeaveStatuses.Rejected &&
            x.Status != LeaveStatuses.Cancelled &&
            x.StartDate <= request.EndDate &&
            request.StartDate <= x.EndDate, cancellationToken);
        if (overlap)
            throw new ConflictException("This employee already has leave covering those dates.");
        var days = request.EndDate.DayNumber - request.StartDate.DayNumber + 1;
        var leave = new LeaveRequest
        {
            EmployeeId = employeeId,
            LeaveTypeId = leaveType.Id,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Days = days,
            Reason = request.Reason?.Trim(),
            Status = LeaveStatuses.Pending
        };
        _db.LeaveRequests.Add(leave);
        await _db.SaveChangesAsync(cancellationToken);
        return await MapLeaveAsync(leave.Id, cancellationToken);
    }

    public async Task<PagedResult<LeaveRequestDto>> GetLeaveAsync(string? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = from leave in _db.LeaveRequests.AsNoTracking()
                    join employee in _db.Employees.AsNoTracking() on leave.EmployeeId equals employee.Id
                    join type in _db.LeaveTypes.AsNoTracking() on leave.LeaveTypeId equals type.Id
                    select new { leave, employee, type };
        if (!_user.HasPermission(PermissionCodes.LeaveManage))
        {
            var self = await CurrentEmployeeAsync(cancellationToken);
            var employeeId = self?.Id ?? Guid.Empty;
            query = query.Where(x => x.leave.EmployeeId == employeeId);
        }
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.leave.Status == status);
        var paged = await query.OrderByDescending(x => x.leave.CreatedAt).PageAsync(page, pageSize, cancellationToken);
        var items = new List<LeaveRequestDto>();
        foreach (var row in paged.Items)
            items.Add(await ToLeaveDto(row.leave, row.employee, row.type, cancellationToken));
        return new PagedResult<LeaveRequestDto> { Items = items, Page = paged.Page, PageSize = paged.PageSize, TotalCount = paged.TotalCount };
    }

    public async Task<LeaveRequestDto> DecideLeaveAsync(Guid id, bool approve, string? reason, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.LeaveManage);
        var leave = await _db.LeaveRequests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Leave request not found.");
        if (leave.Status != LeaveStatuses.Pending)
            throw new ConflictException("Only pending leave can be decided.");
        if (!approve && string.IsNullOrWhiteSpace(reason))
            throw new BadRequestException("A rejection reason is required.");
        if (approve)
        {
            var balance = await GetOrCreateBalanceAsync(leave, cancellationToken);
            if (balance.Used + leave.Days > balance.Allocated)
                throw new BadRequestException("This request exceeds the remaining leave balance.");
            balance.Used += leave.Days;
            leave.Status = LeaveStatuses.Approved;
        }
        else
        {
            leave.Status = LeaveStatuses.Rejected;
        }
        leave.DecisionReason = reason?.Trim();
        leave.DecidedByUserId = Ops.User(_user);
        leave.DecidedAt = DateTime.UtcNow;
        var employee = await _db.Employees.AsNoTracking().FirstAsync(x => x.Id == leave.EmployeeId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(employee.UserId))
        {
            _db.Notifications.Add(new Notification
            {
                UserId = employee.UserId,
                Title = approve ? "Leave approved" : "Leave rejected",
                Message = approve ? "Your leave request was approved." : $"Your leave request was rejected. {reason}",
                Type = "Leave",
                Link = "/people/leave"
            });
        }
        await _db.SaveChangesAsync(cancellationToken);
        return await MapLeaveAsync(leave.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<AnnouncementDto>> GetAnnouncementsAsync(CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.AnnouncementView, PermissionCodes.AnnouncementManage);
        var now = DateTime.UtcNow;
        var userId = Ops.User(_user);
        var self = await CurrentEmployeeAsync(cancellationToken);
        var items = await _db.Announcements.AsNoTracking()
            .Where(x => x.IsPublished && (x.ExpiresAt == null || x.ExpiresAt > now))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        if (!_user.HasPermission(PermissionCodes.AnnouncementManage))
        {
            items = items.Where(x =>
                x.Audience == "Company" ||
                (x.Audience == "Department" && self?.DepartmentId == x.DepartmentId) ||
                (x.Audience == "Team" && self?.TeamId == x.CompanyTeamId)).ToList();
        }
        var reads = await _db.AnnouncementReads.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.AnnouncementId)
            .ToListAsync(cancellationToken);
        var readSet = reads.ToHashSet();
        return items.Select(x => new AnnouncementDto(x.Id, x.Title, x.Body, x.Audience, x.Priority, x.ExpiresAt, readSet.Contains(x.Id), x.CreatedAt)).ToList();
    }

    public async Task<AnnouncementDto> SaveAnnouncementAsync(AnnouncementRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.AnnouncementManage);
        var announcement = new Announcement
        {
            Title = Ops.Required(request.Title, "Title"),
            Body = Ops.Required(request.Body, "Body"),
            Audience = string.IsNullOrWhiteSpace(request.Audience) ? "Company" : request.Audience.Trim(),
            DepartmentId = request.DepartmentId,
            CompanyTeamId = request.CompanyTeamId,
            Priority = string.IsNullOrWhiteSpace(request.Priority) ? "Normal" : request.Priority.Trim(),
            ExpiresAt = request.ExpiresAt,
            CreatedByUserId = Ops.User(_user),
            IsPublished = true
        };
        _db.Announcements.Add(announcement);
        await _db.SaveChangesAsync(cancellationToken);
        return new AnnouncementDto(announcement.Id, announcement.Title, announcement.Body, announcement.Audience, announcement.Priority, announcement.ExpiresAt, false, announcement.CreatedAt);
    }

    public async Task MarkAnnouncementReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.AnnouncementView, PermissionCodes.AnnouncementManage);
        var userId = Ops.User(_user);
        var exists = await _db.AnnouncementReads.AnyAsync(x => x.AnnouncementId == id && x.UserId == userId, cancellationToken);
        if (exists)
            return;
        _db.AnnouncementReads.Add(new AnnouncementRead { AnnouncementId = id, UserId = userId, ReadAt = DateTime.UtcNow });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GoalDto>> GetGoalsAsync(Guid? employeeId, CancellationToken cancellationToken = default)
    {
        employeeId = await ScopeEmployeeAsync(employeeId, PermissionCodes.PerformanceManage, PermissionCodes.PerformanceView, cancellationToken);
        var query = _db.PerformanceGoals.AsNoTracking().AsQueryable();
        if (employeeId is not null)
            query = query.Where(x => x.EmployeeId == employeeId);
        return await query.OrderByDescending(x => x.CreatedAt)
            .Select(x => new GoalDto(x.Id, x.EmployeeId, x.Title, x.KpiName, x.TargetValue, x.CurrentValue, x.Status, x.PeriodStart, x.PeriodEnd))
            .ToListAsync(cancellationToken);
    }

    public async Task<GoalDto> SaveGoalAsync(Guid? id, GoalRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.PerformanceManage);
        var goal = id is null ? new PerformanceGoal() : await _db.PerformanceGoals.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Goal not found.");
        if (id is null)
            _db.PerformanceGoals.Add(goal);
        goal.EmployeeId = request.EmployeeId;
        goal.Title = Ops.Required(request.Title, "Title");
        goal.Description = request.Description?.Trim();
        goal.KpiName = request.KpiName?.Trim();
        goal.TargetValue = request.TargetValue;
        goal.CurrentValue = request.CurrentValue;
        goal.PeriodStart = request.PeriodStart;
        goal.PeriodEnd = request.PeriodEnd;
        goal.Status = goal.TargetValue > 0 && goal.CurrentValue >= goal.TargetValue ? "Completed" : "Active";
        await _db.SaveChangesAsync(cancellationToken);
        return new GoalDto(goal.Id, goal.EmployeeId, goal.Title, goal.KpiName, goal.TargetValue, goal.CurrentValue, goal.Status, goal.PeriodStart, goal.PeriodEnd);
    }

    public async Task<IReadOnlyList<ReviewDto>> GetReviewsAsync(Guid? employeeId, CancellationToken cancellationToken = default)
    {
        employeeId = await ScopeEmployeeAsync(employeeId, PermissionCodes.PerformanceManage, PermissionCodes.PerformanceView, cancellationToken);
        var query = _db.PerformanceReviews.AsNoTracking().AsQueryable();
        if (employeeId is not null)
            query = query.Where(x => x.EmployeeId == employeeId);
        return await query.OrderByDescending(x => x.CreatedAt)
            .Select(x => new ReviewDto(x.Id, x.EmployeeId, x.PeriodLabel, x.Rating, x.Feedback, x.Status, x.CompletedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<ReviewDto> SaveReviewAsync(ReviewRequest request, CancellationToken cancellationToken = default)
    {
        Ops.Require(_user, PermissionCodes.PerformanceManage);
        if (request.Rating is < 1 or > 5)
            throw new BadRequestException("Rating must be between 1 and 5.");
        var review = new PerformanceReview
        {
            EmployeeId = request.EmployeeId,
            PeriodLabel = Ops.Required(request.PeriodLabel, "Period"),
            Rating = request.Rating,
            Feedback = request.Feedback?.Trim(),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Completed" : request.Status.Trim(),
            ManagerUserId = Ops.User(_user),
            CompletedAt = DateTime.UtcNow
        };
        _db.PerformanceReviews.Add(review);
        await _db.SaveChangesAsync(cancellationToken);
        return new ReviewDto(review.Id, review.EmployeeId, review.PeriodLabel, review.Rating, review.Feedback, review.Status, review.CompletedAt);
    }

    private async Task<Guid?> ScopeEmployeeAsync(Guid? employeeId, string manage, string view, CancellationToken cancellationToken)
    {
        if (_user.HasPermission(manage))
            return employeeId;
        Ops.Require(_user, view, manage);
        var self = await CurrentEmployeeAsync(cancellationToken);
        return self?.Id ?? Guid.Empty;
    }

    private async Task<Employee> ResolveAttendanceEmployeeAsync(Guid? employeeId, CancellationToken cancellationToken)
    {
        var self = await CurrentEmployeeAsync(cancellationToken);
        if (employeeId is null || employeeId == self?.Id)
        {
            Ops.Require(_user, PermissionCodes.EmployeeView, PermissionCodes.AttendanceManage);
            return self ?? throw new BadRequestException("No employee profile is linked to this user.");
        }
        Ops.Require(_user, PermissionCodes.AttendanceManage);
        return await _db.Employees.FirstOrDefaultAsync(x => x.Id == employeeId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Employee not found.");
    }

    private async Task<Employee?> CurrentEmployeeAsync(CancellationToken cancellationToken)
    {
        var userId = _user.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return null;
        return await _db.Employees.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    private async Task<TenantSettings> SettingsAsync(CancellationToken cancellationToken) =>
        await _db.TenantSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken) ?? new TenantSettings();

    private async Task<DateTime> LocalNowAsync(CancellationToken cancellationToken)
    {
        var zone = await _db.Tenants.AsNoTracking().Select(x => x.Timezone).FirstOrDefaultAsync(cancellationToken) ?? "UTC";
        try
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(zone));
        }
        catch (TimeZoneNotFoundException)
        {
            return DateTime.UtcNow;
        }
        catch (InvalidTimeZoneException)
        {
            return DateTime.UtcNow;
        }
    }

    private async Task<EmployeeDto> MapEmployeeAsync(Employee employee, bool showSalary, CancellationToken cancellationToken)
    {
        string? department = null;
        string? team = null;
        if (employee.DepartmentId is not null)
            department = await _db.Departments.AsNoTracking().Where(x => x.Id == employee.DepartmentId).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken);
        if (employee.TeamId is not null)
            team = await _db.CompanyTeams.AsNoTracking().Where(x => x.Id == employee.TeamId).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken);
        return new EmployeeDto(
            employee.Id, employee.EmployeeCode, employee.FirstName, employee.LastName, employee.Email, employee.Phone,
            employee.Designation, employee.DepartmentId, department, employee.TeamId, team,
            employee.ManagerEmployeeId, employee.JoiningDate, employee.EmploymentStatus, employee.EmploymentType,
            employee.IsActive, employee.Skills, employee.WorkLocation, employee.EmergencyContactName,
            employee.EmergencyContactPhone, employee.UserId, employee.ProfilePictureUrl, showSalary ? employee.Salary : null);
    }

    private static AttendanceDto MapAttendance(AttendanceRecord record, Employee employee) =>
        new(record.Id, employee.Id, $"{employee.FirstName} {employee.LastName}".Trim(), record.WorkDate,
            record.CheckInAt, record.CheckOutAt, record.WorkedHours, record.IsLate, record.IsEarlyDeparture,
            record.Status, record.CorrectionReason);

    private async Task<LeaveBalance> GetOrCreateBalanceAsync(LeaveRequest leave, CancellationToken cancellationToken)
    {
        var year = leave.StartDate.Year;
        var balance = await _db.LeaveBalances.FirstOrDefaultAsync(x =>
            x.EmployeeId == leave.EmployeeId && x.LeaveTypeId == leave.LeaveTypeId && x.Year == year, cancellationToken);
        if (balance is not null)
            return balance;
        var type = await _db.LeaveTypes.FirstAsync(x => x.Id == leave.LeaveTypeId, cancellationToken);
        balance = new LeaveBalance
        {
            EmployeeId = leave.EmployeeId,
            LeaveTypeId = leave.LeaveTypeId,
            Year = year,
            Allocated = type.DefaultDays
        };
        _db.LeaveBalances.Add(balance);
        return balance;
    }

    private async Task<LeaveRequestDto> MapLeaveAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await (from leave in _db.LeaveRequests.AsNoTracking()
                         join employee in _db.Employees.AsNoTracking() on leave.EmployeeId equals employee.Id
                         join type in _db.LeaveTypes.AsNoTracking() on leave.LeaveTypeId equals type.Id
                         where leave.Id == id
                         select new { leave, employee, type }).FirstAsync(cancellationToken);
        return await ToLeaveDto(row.leave, row.employee, row.type, cancellationToken);
    }

    private async Task<LeaveRequestDto> ToLeaveDto(LeaveRequest leave, Employee employee, LeaveType type, CancellationToken cancellationToken)
    {
        var balance = await _db.LeaveBalances.AsNoTracking().FirstOrDefaultAsync(x =>
            x.EmployeeId == leave.EmployeeId && x.LeaveTypeId == leave.LeaveTypeId && x.Year == leave.StartDate.Year, cancellationToken);
        decimal? remaining = balance is null ? type.DefaultDays : balance.Allocated - balance.Used;
        return new LeaveRequestDto(leave.Id, employee.Id, $"{employee.FirstName} {employee.LastName}".Trim(), type.Id, type.Name,
            leave.StartDate, leave.EndDate, leave.Days, leave.Status, leave.Reason, leave.DecisionReason, remaining);
    }
}
