using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Notifications.Services;
using BusinessOS.Application.Features.Roles.Services;
using BusinessOS.Application.Features.Team.DTOs;
using BusinessOS.Application.Features.Team.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Services;

public sealed class TeamService : ITeamService
{
    private readonly IApplicationDbContext _context;
    private readonly IDbContextFactory<BusinessOSDbContext> _dbContextFactory;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRoleRepository _roleRepository;
    private readonly IRoleService _roleService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IRbacAuditService _auditService;
    private readonly IEmailNotificationService _emailService;
    private readonly ILogger<TeamService> _logger;

    public TeamService(
        IApplicationDbContext context,
        IDbContextFactory<BusinessOSDbContext> dbContextFactory,
        UserManager<ApplicationUser> userManager,
        IRoleRepository roleRepository,
        IRoleService roleService,
        ICurrentUserService currentUserService,
        IRbacAuditService auditService,
        IEmailNotificationService emailService,
        ILogger<TeamService> logger)
    {
        _context = context;
        _dbContextFactory = dbContextFactory;
        _userManager = userManager;
        _roleRepository = roleRepository;
        _roleService = roleService;
        _currentUserService = currentUserService;
        _auditService = auditService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<TeamDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var users = await _userManager.Users
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var members = new List<(ApplicationUser User, IReadOnlyList<string> Roles)>();
        foreach (var user in users)
        {
            var roles = await _roleRepository.GetUserRoleNamesAsync(user.Id, cancellationToken);
            if (roles.Count == 0)
            {
                roles = (await _userManager.GetRolesAsync(user)).ToList();
            }

            members.Add((user, roles));
        }

        var activeMembers = members.Count(x => x.User.IsActive);
        var roleDistribution = members
            .SelectMany(x => x.Roles.DefaultIfEmpty("Unassigned"))
            .GroupBy(x => x)
            .Select(g => new RoleDistributionDto { RoleName = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        var pendingInvitations = await _context.TeamInvitations
            .CountAsync(x => x.Status == InvitationStatus.Pending && x.ExpiresAt > DateTime.UtcNow, cancellationToken);

        var recentActivity = await _context.Activities
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .Select(x => new TeamActivityDto
            {
                Id = x.Id,
                UserId = x.UserId,
                UserName = x.UserName,
                Action = x.Action,
                EntityType = x.EntityType,
                EntityName = x.EntityName,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var assignedTasks = await _context.WorkTasks
            .AsNoTracking()
            .Where(x => x.AssignedUserId != null)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(10)
            .Select(x => new AssignedTaskSummaryDto
            {
                Id = x.Id,
                Title = x.Title,
                ProjectName = x.Project.Name,
                Status = x.Status.ToString(),
                DueDate = x.DueDate
            })
            .ToListAsync(cancellationToken);

        return new TeamDashboardDto
        {
            TotalMembers = members.Count,
            ActiveMembers = activeMembers,
            PendingInvitations = pendingInvitations,
            RoleDistribution = roleDistribution,
            RecentActivity = recentActivity,
            AssignedTasks = assignedTasks
        };
    }

    public async Task<PagedResult<TeamMemberDto>> GetMembersAsync(
        string? search,
        string? status,
        string? role,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var (normalizedPage, normalizedPageSize) = PaginationParams.Normalize(page, pageSize);

        var query = _userManager.Users
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Email!.ToLower().Contains(term) ||
                x.FirstName.ToLower().Contains(term) ||
                x.LastName.ToLower().Contains(term));
        }

        if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.IsActive);
        }
        else if (string.Equals(status, "inactive", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => !x.IsActive);
        }

        var users = await query
            .OrderBy(x => x.FirstName)
            .ThenBy(x => x.LastName)
            .ToListAsync(cancellationToken);

        var members = new List<TeamMemberDto>();
        foreach (var user in users)
        {
            var roles = (await _roleRepository.GetUserRoleNamesAsync(user.Id, cancellationToken)).ToList();
            if (roles.Count == 0)
            {
                roles = (await _userManager.GetRolesAsync(user)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(role) &&
                !roles.Any(r => r.Equals(role, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            members.Add(MapMember(user, roles));
        }

        var totalCount = members.Count;
        var items = members
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToList();

        return new PagedResult<TeamMemberDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }

    public async Task<TeamMemberDto> GetMemberByIdAsync(
        string memberId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var user = await _userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == memberId && x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Team member '{memberId}' was not found.");

        var roles = (await _roleRepository.GetUserRoleNamesAsync(user.Id, cancellationToken)).ToList();
        if (roles.Count == 0)
        {
            roles = (await _userManager.GetRolesAsync(user)).ToList();
        }

        return MapMember(user, roles);
    }

    public async Task<TeamInvitationDto> InviteMemberAsync(
        InviteTeamMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var currentUserId = _currentUserService.UserId
            ?? throw new UnauthorizedException("User context is required.");

        var email = request.Email.Trim().ToLowerInvariant();
        var role = await _roleRepository.GetRoleByIdAsync(request.RoleId, cancellationToken)
            ?? throw new NotFoundException($"Role '{request.RoleId}' was not found.");

        if (role.Name.Equals(RoleNames.Owner, StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException("Owner role cannot be assigned via invitation.");
        }

        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser is not null && existingUser.TenantId == tenantId)
        {
            throw new ConflictException("A team member with this email already exists.");
        }

        var pendingInvite = await _context.TeamInvitations
            .FirstOrDefaultAsync(x =>
                x.Email == email &&
                x.Status == InvitationStatus.Pending &&
                x.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

        if (pendingInvite is not null)
        {
            throw new ConflictException("An invitation for this email is already pending.");
        }

        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var invitation = new TeamInvitation
        {
            TenantId = tenantId,
            Email = email,
            RoleId = role.Id,
            Token = token,
            Status = InvitationStatus.Pending,
            InvitedByUserId = currentUserId,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TeamInvitations.Add(invitation);
        await _context.SaveChangesAsync(cancellationToken);

        var tenant = await _context.Tenants.AsNoTracking().FirstAsync(cancellationToken);
        var inviteUrl = $"/auth/accept-invitation?token={token}";

        await _emailService.SendAsync(
            email,
            $"You're invited to join {tenant.Name} on BusinessOS",
            $"You have been invited to join {tenant.Name} as {role.Name}. Accept your invitation: {inviteUrl}",
            cancellationToken);

        _logger.LogInformation("Team invitation created for {Email} to tenant {TenantId}", email, tenantId);

        await _auditService.LogAsync(
            "TeamMemberInvited",
            nameof(TeamInvitation),
            invitation.Id.ToString(),
            null,
            RbacAuditService.Serialize(new { email, role.Name }),
            cancellationToken);

        return new TeamInvitationDto
        {
            Id = invitation.Id,
            Email = invitation.Email,
            RoleName = role.Name,
            Status = invitation.Status.ToString(),
            ExpiresAt = invitation.ExpiresAt,
            CreatedAt = invitation.CreatedAt
        };
    }

    public async Task<TeamMemberDto> UpdateMemberAsync(
        string memberId,
        UpdateTeamMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var user = await _userManager.Users
            .FirstOrDefaultAsync(x => x.Id == memberId && x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Team member '{memberId}' was not found.");

        var tenant = await _context.Tenants.AsNoTracking().FirstAsync(cancellationToken);
        if (tenant.OwnerUserId == memberId && !request.IsActive)
        {
            throw new BadRequestException("Organization owner cannot be deactivated.");
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();
        user.IsActive = request.IsActive;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            throw new BadRequestException(string.Join(", ", updateResult.Errors.Select(x => x.Description)));
        }

        if (request.RoleId.HasValue)
        {
            var role = await _roleRepository.GetRoleByIdAsync(request.RoleId.Value, cancellationToken)
                ?? throw new NotFoundException($"Role '{request.RoleId}' was not found.");

            if (role.Name.Equals(RoleNames.Owner, StringComparison.OrdinalIgnoreCase) &&
                tenant.OwnerUserId != memberId)
            {
                throw new BadRequestException("Only the current owner can hold the Owner role.");
            }

            var currentRoles = await _roleRepository.GetUserRoleNamesAsync(user.Id, cancellationToken);
            foreach (var currentRole in currentRoles)
            {
                var currentRoleEntity = await _roleRepository.GetRoleByNameAsync(currentRole, cancellationToken);
                if (currentRoleEntity is not null)
                {
                    await _roleService.RemoveUserRoleAsync(user.Id, currentRoleEntity.Id, cancellationToken);
                }
            }

            await _roleService.AssignUserRoleAsync(user.Id, role.Id, cancellationToken);
        }

        await _auditService.LogAsync(
            "TeamMemberUpdated",
            nameof(ApplicationUser),
            user.Id,
            null,
            RbacAuditService.Serialize(new { user.Email, request.IsActive }),
            cancellationToken);

        var roles = (await _roleRepository.GetUserRoleNamesAsync(user.Id, cancellationToken)).ToList();
        return MapMember(user, roles);
    }

    public async Task RemoveMemberAsync(string memberId, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var user = await _userManager.Users
            .FirstOrDefaultAsync(x => x.Id == memberId && x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Team member '{memberId}' was not found.");

        var tenant = await _context.Tenants.AsNoTracking().FirstAsync(cancellationToken);
        if (tenant.OwnerUserId == memberId)
        {
            throw new BadRequestException("Organization owner cannot be removed.");
        }

        user.IsActive = false;
        await _userManager.UpdateAsync(user);

        await _auditService.LogAsync(
            "TeamMemberRemoved",
            nameof(ApplicationUser),
            user.Id,
            RbacAuditService.Serialize(new { user.Email, IsActive = true }),
            RbacAuditService.Serialize(new { user.Email, IsActive = false }),
            cancellationToken);
    }

    public async Task<IReadOnlyList<TeamActivityDto>> GetTeamActivityAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, 100);

        return await _context.Activities
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => new TeamActivityDto
            {
                Id = x.Id,
                UserId = x.UserId,
                UserName = x.UserName,
                Action = x.Action,
                EntityType = x.EntityType,
                EntityName = x.EntityName,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TeamInvitationDto>> GetPendingInvitationsAsync(
        CancellationToken cancellationToken = default)
    {
        var invitations = await _context.TeamInvitations
            .AsNoTracking()
            .Include(x => x.Role)
            .Where(x => x.Status == InvitationStatus.Pending && x.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return invitations.Select(x => new TeamInvitationDto
        {
            Id = x.Id,
            Email = x.Email,
            RoleName = x.Role.Name,
            Status = x.Status.ToString(),
            ExpiresAt = x.ExpiresAt,
            CreatedAt = x.CreatedAt
        }).ToList();
    }

    public async Task<InvitationPreviewDto> GetInvitationPreviewAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var invitation = await dbContext.TeamInvitations
            .IgnoreQueryFilters()
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Token == token, cancellationToken);

        if (invitation is null || invitation.Status != InvitationStatus.Pending || invitation.ExpiresAt <= DateTime.UtcNow)
        {
            return new InvitationPreviewDto(string.Empty, string.Empty, string.Empty, DateTime.UtcNow, false);
        }

        var tenant = await dbContext.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(x => x.Id == invitation.TenantId, cancellationToken);

        return new InvitationPreviewDto(
            invitation.Email,
            tenant.Name,
            invitation.Role.Name,
            invitation.ExpiresAt,
            true);
    }

    public async Task AcceptInvitationAsync(
        AcceptInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var invitation = await dbContext.TeamInvitations
            .IgnoreQueryFilters()
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Token == request.Token, cancellationToken)
            ?? throw new NotFoundException("Invitation not found.");

        if (invitation.Status != InvitationStatus.Pending)
        {
            throw new BadRequestException("This invitation is no longer valid.");
        }

        if (invitation.ExpiresAt <= DateTime.UtcNow)
        {
            invitation.Status = InvitationStatus.Expired;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new BadRequestException("This invitation has expired.");
        }

        var existingUser = await _userManager.FindByEmailAsync(invitation.Email);
        ApplicationUser user;

        if (existingUser is not null)
        {
            if (existingUser.TenantId != invitation.TenantId)
            {
                throw new ConflictException("This email is already registered with another organization.");
            }

            user = existingUser;
            user.FirstName = request.FirstName.Trim();
            user.LastName = request.LastName.Trim();
            user.IsActive = true;
            user.JoinedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
        }
        else
        {
            user = new ApplicationUser
            {
                UserName = invitation.Email,
                Email = invitation.Email,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                TenantId = invitation.TenantId,
                IsActive = true,
                JoinedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                throw new BadRequestException(string.Join(", ", createResult.Errors.Select(x => x.Description)));
            }
        }

        await _userManager.AddToRoleAsync(user, invitation.Role.Name);
        await _roleRepository.AssignRoleToUserAsync(user.Id, invitation.RoleId, cancellationToken);

        invitation.Status = InvitationStatus.Accepted;
        invitation.AcceptedAt = DateTime.UtcNow;
        invitation.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            "TeamMemberJoined",
            nameof(TeamInvitation),
            invitation.Id.ToString(),
            null,
            RbacAuditService.Serialize(new { invitation.Email, invitation.Role.Name }),
            cancellationToken);
    }

    private Guid RequireTenantId() =>
        _currentUserService.TenantId
        ?? throw new BadRequestException("Tenant context is required.");

    private static TeamMemberDto MapMember(ApplicationUser user, IReadOnlyList<string> roles) =>
        new()
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            AvatarUrl = user.AvatarUrl,
            Roles = roles,
            PrimaryRole = roles.FirstOrDefault(),
            JoinedAt = user.JoinedAt,
            IsActive = user.IsActive,
            LastActiveAt = user.LastActiveAt
        };
}
