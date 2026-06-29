using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Roles.DTOs;
using BusinessOS.Application.Features.Roles.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace BusinessOS.Infrastructure.Services;

public sealed class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IRbacAuditService _auditService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public RoleService(
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository,
        IRbacAuditService auditService,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager)
    {
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _auditService = auditService;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<RoleDto> CreateRoleAsync(
        CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _roleRepository.GetRoleByNameAsync(request.Name, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException($"Role '{request.Name}' already exists.");
        }

        var role = new Role
        {
            Name = request.Name,
            Description = request.Description,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        await _roleRepository.CreateRoleAsync(role, cancellationToken);
        await EnsureIdentityRoleExistsAsync(role.Name);

        await _auditService.LogAsync(
            "RoleCreated",
            nameof(Role),
            role.Id.ToString(),
            null,
            RbacAuditService.Serialize(new { role.Name, role.Description, role.IsActive }),
            cancellationToken);

        return MapRole(role);
    }

    public async Task<RoleDto> UpdateRoleAsync(
        Guid id,
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetRoleByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Role '{id}' was not found.");

        var duplicate = await _roleRepository.GetRoleByNameAsync(request.Name, cancellationToken);
        if (duplicate is not null && duplicate.Id != id)
        {
            throw new ConflictException($"Role '{request.Name}' already exists.");
        }

        var oldValue = RbacAuditService.Serialize(new { role.Name, role.Description, role.IsActive });

        role.Name = request.Name;
        role.Description = request.Description;
        role.IsActive = request.IsActive;

        await _roleRepository.UpdateRoleAsync(role, cancellationToken);

        await _auditService.LogAsync(
            "RoleUpdated",
            nameof(Role),
            role.Id.ToString(),
            oldValue,
            RbacAuditService.Serialize(new { role.Name, role.Description, role.IsActive }),
            cancellationToken);

        return MapRole(role);
    }

    public async Task DeleteRoleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetRoleByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Role '{id}' was not found.");

        if (RoleNames.Protected.Contains(role.Name))
        {
            throw new BadRequestException($"System role '{role.Name}' cannot be deleted.");
        }

        var oldValue = RbacAuditService.Serialize(new { role.Name, role.Description, role.IsActive });
        await _roleRepository.DeleteRoleAsync(role, cancellationToken);

        await _auditService.LogAsync(
            "RoleDeleted",
            nameof(Role),
            role.Id.ToString(),
            oldValue,
            null,
            cancellationToken);
    }

    public async Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _roleRepository.GetRolesAsync(cancellationToken);
        return roles.Select(MapRole).ToList();
    }

    public async Task<RoleDto> GetRoleByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetRoleByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Role '{id}' was not found.");

        return MapRole(role);
    }

    public async Task AssignPermissionAsync(
        Guid roleId,
        Guid permissionId,
        CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetRoleByIdAsync(roleId, cancellationToken)
            ?? throw new NotFoundException($"Role '{roleId}' was not found.");

        var permission = await _permissionRepository.GetPermissionByIdAsync(permissionId, cancellationToken)
            ?? throw new NotFoundException($"Permission '{permissionId}' was not found.");

        await _roleRepository.AssignPermissionAsync(roleId, permissionId, cancellationToken);

        await _auditService.LogAsync(
            "PermissionAssigned",
            nameof(RolePermission),
            $"{roleId}:{permissionId}",
            null,
            RbacAuditService.Serialize(new { role.Name, permission.Code }),
            cancellationToken);
    }

    public async Task RemovePermissionAsync(
        Guid roleId,
        Guid permissionId,
        CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetRoleByIdAsync(roleId, cancellationToken)
            ?? throw new NotFoundException($"Role '{roleId}' was not found.");

        var permission = await _permissionRepository.GetPermissionByIdAsync(permissionId, cancellationToken)
            ?? throw new NotFoundException($"Permission '{permissionId}' was not found.");

        await _roleRepository.RemovePermissionAsync(roleId, permissionId, cancellationToken);

        await _auditService.LogAsync(
            "PermissionRemoved",
            nameof(RolePermission),
            $"{roleId}:{permissionId}",
            RbacAuditService.Serialize(new { role.Name, permission.Code }),
            null,
            cancellationToken);
    }

    public async Task AssignUserRoleAsync(
        string userId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetRoleByIdAsync(roleId, cancellationToken)
            ?? throw new NotFoundException($"Role '{roleId}' was not found.");

        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException($"User '{userId}' was not found.");

        await _roleRepository.AssignRoleToUserAsync(userId, roleId, cancellationToken);
        await EnsureIdentityRoleExistsAsync(role.Name);
        await _userManager.AddToRoleAsync(user, role.Name);

        await _auditService.LogAsync(
            "UserRoleAssigned",
            nameof(UserRole),
            $"{userId}:{roleId}",
            null,
            RbacAuditService.Serialize(new { userId, role.Name }),
            cancellationToken);
    }

    public async Task RemoveUserRoleAsync(
        string userId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetRoleByIdAsync(roleId, cancellationToken)
            ?? throw new NotFoundException($"Role '{roleId}' was not found.");

        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException($"User '{userId}' was not found.");

        await _roleRepository.RemoveRoleFromUserAsync(userId, roleId, cancellationToken);
        await _userManager.RemoveFromRoleAsync(user, role.Name);

        await _auditService.LogAsync(
            "UserRoleRemoved",
            nameof(UserRole),
            $"{userId}:{roleId}",
            RbacAuditService.Serialize(new { userId, role.Name }),
            null,
            cancellationToken);
    }

    public Task<IReadOnlyList<string>> GetUserPermissionsAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        _roleRepository.GetUserPermissionCodesAsync(userId, cancellationToken);

    public Task<IReadOnlyList<string>> GetUserRolesAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        _roleRepository.GetUserRoleNamesAsync(userId, cancellationToken);

    private async Task EnsureIdentityRoleExistsAsync(string roleName)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            await _roleManager.CreateAsync(new ApplicationRole { Name = roleName });
        }
    }

    private static RoleDto MapRole(Role role) =>
        new(
            role.Id,
            role.Name,
            role.Description,
            role.IsActive,
            role.CreatedAt,
            role.RolePermissions
                .Select(x => x.Permission.Code)
                .OrderBy(x => x)
                .ToList());
}
