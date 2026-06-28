using BusinessOS.Application.Features.Roles.DTOs;

namespace BusinessOS.Application.Features.Roles.Services;

public interface IPermissionService
{
    Task<IReadOnlyList<PermissionDto>> GetPermissionsAsync(CancellationToken cancellationToken = default);

    Task<PermissionDto> GetPermissionByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
