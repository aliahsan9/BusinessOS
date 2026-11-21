using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Application.Features.Enterprise;
using BusinessOS.Domain.Billing;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Billing;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Enterprise;

public sealed class EnterpriseEntitlementGuard : IEnterpriseEntitlementGuard
{
    private readonly BusinessOSDbContext _db;
    private readonly ITenantProvider _tenant;

    public EnterpriseEntitlementGuard(BusinessOSDbContext db, ITenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public Task EnsureAdvancedSecurityAsync(CancellationToken cancellationToken = default) =>
        CommercialGate.EnsureFeatureAsync(_db, _tenant.TenantId, CommercialFeatureKeys.AdvancedSecurity, cancellationToken);

    public Task EnsureMultiBranchAsync(CancellationToken cancellationToken = default) =>
        CommercialGate.EnsureFeatureAsync(_db, _tenant.TenantId, CommercialFeatureKeys.MultiBranch, cancellationToken);
}

public sealed class OrgScopeService : IOrgScopeService
{
    private readonly BusinessOSDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantProvider _tenant;

    public OrgScopeService(BusinessOSDbContext db, ICurrentUserService currentUser, ITenantProvider tenant)
    {
        _db = db;
        _currentUser = currentUser;
        _tenant = tenant;
    }

    public async Task<OrgScopeSnapshot> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser.Roles.Any(r => r is "Owner" or "Admin") ||
            _currentUser.HasPermission(PermissionCodes.EnterpriseManage) ||
            _currentUser.HasPermission(PermissionCodes.SystemAdminManage))
        {
            return new OrgScopeSnapshot(true, [], []);
        }

        var userId = _currentUser.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return new OrgScopeSnapshot(false, [], []);

        var employee = await _db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.IsActive, cancellationToken);

        var branchIds = new HashSet<Guid>();
        var departmentIds = new HashSet<Guid>();

        if (employee?.PrimaryBranchId is Guid branchId)
            branchIds.Add(branchId);
        if (employee?.DepartmentId is Guid deptId)
            departmentIds.Add(deptId);

        if (employee?.IsRegionalManager == true && employee.PrimaryBranchId is Guid regionalBranch)
        {
            var region = await _db.CompanyBranches.AsNoTracking()
                .Where(x => x.Id == regionalBranch)
                .Select(x => x.Region)
                .FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(region))
            {
                var regionBranches = await _db.CompanyBranches.AsNoTracking()
                    .Where(x => x.Region == region && x.IsActive)
                    .Select(x => x.Id)
                    .ToListAsync(cancellationToken);
                foreach (var id in regionBranches)
                    branchIds.Add(id);
            }
        }

        var scopedRoles = await _db.RbacUserRoles.AsNoTracking()
            .Where(x => x.UserId == userId && x.TenantId == _tenant.TenantId)
            .Select(x => x.Role)
            .Where(x => x.IsActive && x.ScopeKind != RoleScopeKinds.Tenant && x.ScopeId != null)
            .ToListAsync(cancellationToken);

        foreach (var role in scopedRoles)
        {
            if (role.ScopeKind == RoleScopeKinds.Branch && role.ScopeId is Guid b)
                branchIds.Add(b);
            if (role.ScopeKind == RoleScopeKinds.Department && role.ScopeId is Guid d)
                departmentIds.Add(d);
        }

        var settings = await _db.TenantEnterpriseSecuritySettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == _tenant.TenantId, cancellationToken);
        if (settings is { EnforceOrgScopeOnQueries: false })
            return new OrgScopeSnapshot(true, [], []);

        var hasBranches = await _db.CompanyBranches.AsNoTracking().AnyAsync(cancellationToken);
        if (!hasBranches && branchIds.Count == 0 && departmentIds.Count == 0)
            return new OrgScopeSnapshot(true, [], []);

        return new OrgScopeSnapshot(false, branchIds.ToList(), departmentIds.ToList());
    }

    public async Task EnsureCanAccessBranchAsync(Guid? branchId, CancellationToken cancellationToken = default)
    {
        if (branchId is null)
            return;
        var scope = await GetCurrentScopeAsync(cancellationToken);
        if (scope.IsTenantWide || scope.BranchIds.Contains(branchId.Value))
            return;
        throw new ForbiddenException("You do not have access to this branch.");
    }

    public async Task EnsureCanAccessDepartmentAsync(Guid? departmentId, CancellationToken cancellationToken = default)
    {
        if (departmentId is null)
            return;
        var scope = await GetCurrentScopeAsync(cancellationToken);
        if (scope.IsTenantWide || scope.DepartmentIds.Contains(departmentId.Value))
            return;
        throw new ForbiddenException("You do not have access to this department.");
    }

    public bool IsVisible(OrgScopeSnapshot scope, Guid? branchId, Guid? departmentId)
    {
        if (scope.IsTenantWide)
            return true;
        if (branchId is Guid b && scope.BranchIds.Contains(b))
            return true;
        if (departmentId is Guid d && scope.DepartmentIds.Contains(d))
            return true;
        if (branchId is null && departmentId is null)
            return true;
        return false;
    }
}

public sealed class EnterpriseOrganizationService : IEnterpriseOrganizationService
{
    private readonly BusinessOSDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly IEnterpriseEntitlementGuard _guard;
    private readonly IEnterpriseAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public EnterpriseOrganizationService(
        BusinessOSDbContext db,
        ITenantProvider tenant,
        IEnterpriseEntitlementGuard guard,
        IEnterpriseAuditService audit,
        ICurrentUserService currentUser)
    {
        _db = db;
        _tenant = tenant;
        _guard = guard;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<CompanyBranchDto>> GetBranchesAsync(CancellationToken cancellationToken = default)
    {
        await _guard.EnsureAdvancedSecurityAsync(cancellationToken);
        return await _db.CompanyBranches.AsNoTracking()
            .OrderByDescending(x => x.IsHeadquarters)
            .ThenBy(x => x.Name)
            .Select(x => new CompanyBranchDto(x.Id, x.Name, x.Code, x.Region, x.IsHeadquarters, x.ManagerEmployeeId, x.Address, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<CompanyBranchDto> SaveBranchAsync(Guid? id, CompanyBranchWriteRequest request, CancellationToken cancellationToken = default)
    {
        await _guard.EnsureAdvancedSecurityAsync(cancellationToken);

        var existingCount = await _db.CompanyBranches.CountAsync(cancellationToken);
        if (id is null && existingCount >= 1)
            await _guard.EnsureMultiBranchAsync(cancellationToken);

        CompanyBranch entity;
        if (id is Guid existingId)
        {
            entity = await _db.CompanyBranches.FirstOrDefaultAsync(x => x.Id == existingId, cancellationToken)
                ?? throw new NotFoundException("Branch was not found.");
        }
        else
        {
            entity = new CompanyBranch { TenantId = _tenant.TenantId };
            _db.CompanyBranches.Add(entity);
        }

        if (request.IsHeadquarters)
        {
            var others = await _db.CompanyBranches.Where(x => x.IsHeadquarters && (id == null || x.Id != id)).ToListAsync(cancellationToken);
            foreach (var other in others)
                other.IsHeadquarters = false;
        }

        entity.Name = request.Name.Trim();
        entity.Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim();
        entity.Region = request.Region;
        entity.IsHeadquarters = request.IsHeadquarters || existingCount == 0;
        entity.ManagerEmployeeId = request.ManagerEmployeeId;
        entity.Address = request.Address;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(new EnterpriseAuditWriteRequest
        {
            Category = EnterpriseAuditCategories.Administrative,
            Action = id is null ? "BranchCreated" : "BranchUpdated",
            ActorUserId = _currentUser.UserId,
            ActorType = "User",
            EntityType = nameof(CompanyBranch),
            EntityId = entity.Id.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { entity.Name, entity.Code, entity.IsHeadquarters })
        }, cancellationToken);

        return new CompanyBranchDto(entity.Id, entity.Name, entity.Code, entity.Region, entity.IsHeadquarters, entity.ManagerEmployeeId, entity.Address, entity.IsActive);
    }

    public async Task<OrgHierarchyDto> GetHierarchyAsync(CancellationToken cancellationToken = default)
    {
        await _guard.EnsureAdvancedSecurityAsync(cancellationToken);
        var branches = await GetBranchesAsync(cancellationToken);
        var departments = await _db.Departments.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new OrgDepartmentNodeDto(x.Id, x.Name, x.BranchId, x.ManagerEmployeeId, x.IsActive))
            .ToListAsync(cancellationToken);
        var teams = await _db.CompanyTeams.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new OrgTeamNodeDto(x.Id, x.Name, x.DepartmentId, x.LeaderEmployeeId, x.IsActive))
            .ToListAsync(cancellationToken);
        return new OrgHierarchyDto(branches, departments, teams);
    }
}

public sealed class EnterpriseRbacService : IEnterpriseRbacService
{
    private readonly BusinessOSDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly ICurrentUserService _currentUser;
    private readonly IEnterpriseEntitlementGuard _guard;
    private readonly IEnterpriseAuditService _audit;
    private readonly IAuthorizationVersionService _versions;
    private readonly UserManager<ApplicationUser> _userManager;

    public EnterpriseRbacService(
        BusinessOSDbContext db,
        ITenantProvider tenant,
        ICurrentUserService currentUser,
        IEnterpriseEntitlementGuard guard,
        IEnterpriseAuditService audit,
        IAuthorizationVersionService versions,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _tenant = tenant;
        _currentUser = currentUser;
        _guard = guard;
        _audit = audit;
        _versions = versions;
        _userManager = userManager;
    }

    public async Task<IReadOnlyList<PermissionGroupDto>> GetPermissionGroupsAsync(CancellationToken cancellationToken = default)
    {
        await _guard.EnsureAdvancedSecurityAsync(cancellationToken);
        var groups = await _db.PermissionGroups.AsNoTracking()
            .Include(x => x.Permissions).ThenInclude(x => x.Permission)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return groups.Select(MapGroup).ToList();
    }

    public async Task<PermissionGroupDto> SavePermissionGroupAsync(Guid? id, PermissionGroupWriteRequest request, CancellationToken cancellationToken = default)
    {
        await _guard.EnsureAdvancedSecurityAsync(cancellationToken);
        EnsureNoPrivilegeEscalation(request.PermissionCodes);

        PermissionGroup group;
        if (id is Guid existingId)
        {
            group = await _db.PermissionGroups.Include(x => x.Permissions)
                .FirstOrDefaultAsync(x => x.Id == existingId, cancellationToken)
                ?? throw new NotFoundException("Permission group was not found.");
            _db.PermissionGroupPermissions.RemoveRange(group.Permissions);
            group.Permissions.Clear();
        }
        else
        {
            group = new PermissionGroup { TenantId = _tenant.TenantId };
            _db.PermissionGroups.Add(group);
        }

        group.Name = request.Name.Trim();
        group.Description = request.Description;
        group.IsActive = request.IsActive;
        group.UpdatedAt = DateTime.UtcNow;

        var permissions = await _db.Permissions.AsNoTracking()
            .Where(x => request.PermissionCodes.Contains(x.Code))
            .ToListAsync(cancellationToken);
        foreach (var permission in permissions)
        {
            group.Permissions.Add(new PermissionGroupPermission
            {
                PermissionGroupId = group.Id,
                PermissionId = permission.Id,
                TenantId = _tenant.TenantId,
                Permission = permission
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(new EnterpriseAuditWriteRequest
        {
            Category = EnterpriseAuditCategories.Permission,
            Action = id is null ? "PermissionGroupCreated" : "PermissionGroupUpdated",
            ActorUserId = _currentUser.UserId,
            ActorType = "User",
            EntityType = nameof(PermissionGroup),
            EntityId = group.Id.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { group.Name, Codes = request.PermissionCodes })
        }, cancellationToken);

        return MapGroup(group);
    }

    public async Task<TemporaryPermissionGrantDto> GrantTemporaryAsync(TemporaryPermissionWriteRequest request, CancellationToken cancellationToken = default)
    {
        await _guard.EnsureAdvancedSecurityAsync(cancellationToken);
        if (request.ExpiresAt <= DateTime.UtcNow)
            throw new BadRequestException("Temporary permission expiry must be in the future.");
        EnsureNoPrivilegeEscalation([request.PermissionCode]);

        var user = await _userManager.FindByIdAsync(request.UserId)
            ?? throw new NotFoundException("User was not found.");
        if (user.TenantId != _tenant.TenantId)
            throw new NotFoundException("User was not found.");

        var grant = new TemporaryPermissionGrant
        {
            TenantId = _tenant.TenantId,
            UserId = request.UserId,
            PermissionCode = request.PermissionCode.Trim(),
            RoleId = request.RoleId,
            ExpiresAt = request.ExpiresAt.ToUniversalTime(),
            GrantedByUserId = _currentUser.UserId ?? throw new UnauthorizedException("Authentication required."),
            Reason = request.Reason
        };
        _db.TemporaryPermissionGrants.Add(grant);
        _db.RoleAssignmentHistories.Add(new RoleAssignmentHistory
        {
            TenantId = _tenant.TenantId,
            UserId = request.UserId,
            RoleId = request.RoleId,
            PermissionCode = grant.PermissionCode,
            Action = "TemporaryGrant",
            ActorUserId = grant.GrantedByUserId,
            Reason = request.Reason,
            ExpiresAt = grant.ExpiresAt
        });
        user.SecurityVersion++;
        await _userManager.UpdateAsync(user);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(new EnterpriseAuditWriteRequest
        {
            Category = EnterpriseAuditCategories.Permission,
            Action = "TemporaryPermissionGranted",
            ActorUserId = _currentUser.UserId,
            ActorType = "User",
            EntityType = nameof(TemporaryPermissionGrant),
            EntityId = grant.Id.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { grant.UserId, grant.PermissionCode, grant.ExpiresAt })
        }, cancellationToken);

        return MapGrant(grant);
    }

    public async Task RevokeTemporaryAsync(Guid grantId, CancellationToken cancellationToken = default)
    {
        await _guard.EnsureAdvancedSecurityAsync(cancellationToken);
        var grant = await _db.TemporaryPermissionGrants.FirstOrDefaultAsync(x => x.Id == grantId, cancellationToken)
            ?? throw new NotFoundException("Temporary grant was not found.");
        grant.IsRevoked = true;
        grant.RevokedAt = DateTime.UtcNow;
        grant.RevokedByUserId = _currentUser.UserId;
        grant.UpdatedAt = DateTime.UtcNow;
        _db.RoleAssignmentHistories.Add(new RoleAssignmentHistory
        {
            TenantId = _tenant.TenantId,
            UserId = grant.UserId,
            RoleId = grant.RoleId,
            PermissionCode = grant.PermissionCode,
            Action = "TemporaryRevoke",
            ActorUserId = _currentUser.UserId ?? "system",
            Reason = "Revoked"
        });
        await _versions.BumpUserAsync(grant.UserId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TemporaryPermissionGrantDto>> GetTemporaryGrantsAsync(string? userId = null, CancellationToken cancellationToken = default)
    {
        await _guard.EnsureAdvancedSecurityAsync(cancellationToken);
        var query = _db.TemporaryPermissionGrants.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(x => x.UserId == userId);
        return await query.OrderByDescending(x => x.CreatedAt).Select(x => MapGrant(x)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoleAssignmentHistoryDto>> GetAssignmentHistoryAsync(string? userId = null, CancellationToken cancellationToken = default)
    {
        await _guard.EnsureAdvancedSecurityAsync(cancellationToken);
        var query = _db.RoleAssignmentHistories.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(x => x.UserId == userId);
        return await query.OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .Select(x => new RoleAssignmentHistoryDto(x.Id, x.UserId, x.RoleId, x.RoleName, x.PermissionCode, x.Action, x.ActorUserId, x.Reason, x.ExpiresAt, x.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ResolveEffectivePermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenant.TenantId;
        var rolePermissions = await _db.RbacUserRoles.AsNoTracking()
            .Where(x => x.UserId == userId && x.TenantId == tenantId && x.Role.IsActive)
            .SelectMany(x => x.Role.RolePermissions)
            .Select(x => x.Permission.Code)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var temporary = await _db.TemporaryPermissionGrants.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.UserId == userId && !x.IsRevoked && !x.IsDeleted && x.ExpiresAt > now)
            .Select(x => x.PermissionCode)
            .ToListAsync(cancellationToken);
        return rolePermissions.Concat(temporary).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
    }

    private void EnsureNoPrivilegeEscalation(IReadOnlyList<string> codes)
    {
        if (_currentUser.Roles.Contains("Owner", StringComparer.OrdinalIgnoreCase))
        {
            if (codes.Any(c => string.Equals(c, PermissionCodes.SystemAdminManage, StringComparison.OrdinalIgnoreCase))
                && !_currentUser.HasPermission(PermissionCodes.SystemAdminManage))
            {
                throw new BadRequestException("Platform administration cannot be granted from a tenant account.");
            }
            return;
        }

        foreach (var code in codes)
        {
            if (string.Equals(code, PermissionCodes.SystemAdminManage, StringComparison.OrdinalIgnoreCase))
                throw new BadRequestException("Platform administration cannot be granted from a tenant account.");
            if (!_currentUser.HasPermission(code))
                throw new ForbiddenException($"You cannot grant permission '{code}' that you do not hold.");
        }
    }

    private static PermissionGroupDto MapGroup(PermissionGroup group) =>
        new(group.Id, group.Name, group.Description,
            group.Permissions.Select(x => x.Permission?.Code ?? string.Empty).Where(x => x.Length > 0).Distinct().ToList(),
            group.IsActive);

    private static TemporaryPermissionGrantDto MapGrant(TemporaryPermissionGrant grant) =>
        new(grant.Id, grant.UserId, grant.PermissionCode, grant.RoleId, grant.ExpiresAt, grant.GrantedByUserId, grant.Reason, grant.IsRevoked);
}
