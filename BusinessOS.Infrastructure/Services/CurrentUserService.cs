using System.Security.Claims;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace BusinessOS.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User =>
        _httpContextAccessor.HttpContext?.User;

    public string? UserId =>
        User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? Email =>
        User?.FindFirstValue(ClaimTypes.Email);

    public Guid? TenantId
    {
        get
        {
            var value = User?.FindFirst("TenantId")?.Value;
            return Guid.TryParse(value, out var tenantId) ? tenantId : null;
        }
    }

    public IReadOnlyList<string> Roles =>
        User?.FindAll(ClaimTypes.Role)
            .Select(x => x.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList()
        ?? [];

    public IReadOnlyList<string> Permissions
    {
        get
        {
            var permissionsClaim = User?.FindFirst(ClaimTypesConstants.Permissions)?.Value;
            if (string.IsNullOrWhiteSpace(permissionsClaim))
            {
                return [];
            }

            return permissionsClaim
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();
        }
    }

    public bool HasPermission(string permissionCode) =>
        Permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
}
