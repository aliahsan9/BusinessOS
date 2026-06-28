using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Roles.DTOs;
using BusinessOS.Application.Features.Roles.Services;

namespace BusinessOS.Infrastructure.Services;

public sealed class PermissionService : IPermissionService
{
    private readonly IPermissionRepository _permissionRepository;

    public PermissionService(IPermissionRepository permissionRepository)
    {
        _permissionRepository = permissionRepository;
    }

    public async Task<IReadOnlyList<PermissionDto>> GetPermissionsAsync(
        CancellationToken cancellationToken = default)
    {
        var permissions = await _permissionRepository.GetPermissionsAsync(cancellationToken);
        return permissions.Select(MapPermission).ToList();
    }

    public async Task<PermissionDto> GetPermissionByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var permission = await _permissionRepository.GetPermissionByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Permission '{id}' was not found.");

        return MapPermission(permission);
    }

    private static PermissionDto MapPermission(Domain.Entities.Permission permission) =>
        new(
            permission.Id,
            permission.Name,
            permission.Code,
            permission.Description,
            permission.Category);
}
